using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OctopusRetail.Api.Dtos;
using OctopusRetail.Api.Persistence;
using OctopusRetail.Api.Services;
using System.Net;
using System.Net.Http.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// 自助建店 —— 解决「引导第一个店长」的鸡生蛋问题。
///
/// 此前真实环境必须手工写 SQL 建成员关系 + 手动把用户加进 Keycloak 组，
/// 否则新用户登录后 availableTenants 恒为空、什么都做不了。
/// </summary>
public class TenantCreationTests
{
    // ── 基建 ──────────────────────────────────────────────

    private const string TenantsRoot = """[{ "id":"g-tenants","name":"tenants","path":"/tenants","subGroupCount":0,"subGroups":[] }]""";

    private const string TenantsRootWithChild = """
    [{ "id":"g-tenants","name":"tenants","path":"/tenants","subGroupCount":1,"subGroups":[] }]
    """;

    private static string ChildItem(string name) =>
        $$"""{ "id":"g-{{name}}","name":"{{name}}","path":"/tenants/{{name}}","subGroupCount":0,"subGroups":[] }""";

    private static IConfiguration Config(bool enabled = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:BaseUrl"] = "https://fake-kc",
            ["Keycloak:Realm"] = "octopus",
            ["Keycloak:BotClientId"] = "retail-invite-bot",
            ["Keycloak:BotClientSecret"] = "secret",
            ["Keycloak:SyncEnabled"] = enabled ? "true" : "false",
        }!).Build();

    /// <summary>
    /// 默认 Responder：/tenants 下已有 t_shanghai_001；
    /// 关键——POST 建组后要把**新建的组**反映到后续 /children 响应里，
    /// 否则客户端"创建后再拉一次"仍找不到该组，会误判失败。
    /// </summary>
    private static Func<HttpRequestMessage, HttpResponseMessage> Responder()
    {
        var created = new List<string>();

        return req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Get && path.EndsWith("/groups"))
                return RecordingHttpHandler.Raw(TenantsRootWithChild);

            if (req.Method == HttpMethod.Get && path.EndsWith("/children"))
            {
                var items = new List<string> { ChildItem("t_shanghai_001") };
                items.AddRange(created.Select(ChildItem));
                return RecordingHttpHandler.Raw("[" + string.Join(",", items) + "]");
            }

            if (req.Method == HttpMethod.Post && path.EndsWith("/children"))
            {
                var body = req.Content?.ReadAsStringAsync().Result ?? "";
                var m = System.Text.RegularExpressions.Regex.Match(body, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) created.Add(m.Groups[1].Value);
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (req.Method == HttpMethod.Put) return new HttpResponseMessage(HttpStatusCode.NoContent);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
    }

    private static KeycloakAdminClient NewKeycloak(RecordingHttpHandler handler, bool enabled = true) =>
        new(new RecordingHttpClientFactory(handler), Config(enabled),
            NullLogger<KeycloakAdminClient>.Instance);

    private static (SqliteConnectionHolder holder, RetailDbContext db) NewDb(string? tenant = null)
    {
        var c = new SqliteConnectionHolder();
        var db = new RetailDbContext(c.Options, TestTenantContext.For(tenant));
        db.Database.EnsureCreated();
        RetailDbSeeder.Seed(db);
        return (c, db);
    }

    private sealed class SqliteConnectionHolder
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        public DbContextOptions<RetailDbContext> Options { get; }

        public SqliteConnectionHolder()
        {
            _conn = new Microsoft.Data.Sqlite.SqliteConnection(
                $"Data Source=tenant_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            _conn.Open();
            Options = new DbContextOptionsBuilder<RetailDbContext>().UseSqlite(_conn).Options;
        }

        /// <summary>用同一份库开一个租户已解析的上下文（模拟租户内请求）</summary>
        public RetailDbContext NewContext(string? tenant) =>
            new(Options, TestTenantContext.For(tenant));
    }

    private static TenantService NewService(
        RetailDbContext db, RecordingHttpHandler handler, bool enabled = true) =>
        new(db, NewKeycloak(handler, enabled),
            new MutableTimeProvider(), NullLogger<TenantService>.Instance);

    // ── 正常路径 ──────────────────────────────────────────

    [Fact]
    public async Task Create_ValidInput_CreatesTenantAndCallerBecomesOwner()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var handler = new RecordingHttpHandler { Responder = Responder() };
        var svc = NewService(db, handler);

        var r = await svc.CreateAsync("kc-brand-new-user",
            new CreateTenantInput("SH002", "上海徐家汇店", "store", null));

        r.Success.Should().BeTrue(r.Error);
        r.TenantId.Should().Be("t_sh002");
        r.TenantName.Should().Be("上海徐家汇店");
        r.GroupSynced.Should().BeTrue("建店时应立即把 owner 加入 Keycloak 组");

        var membership = await db.Memberships.IgnoreQueryFilters()
            .SingleAsync(m => m.KcSub == "kc-brand-new-user");
        membership.TenantId.Should().Be("t_sh002");
        membership.RoleInTenant.Should().Be("owner");
        membership.Status.Should().Be("active");
        membership.KcSyncState.Should().Be(MembershipSyncState.Synced);

        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.TenantId == "t_sh002");
        tenant.TenantCode.Should().Be("SH002");
        tenant.Status.Should().Be("active");
    }

    [Fact]
    public async Task Create_PushesOwnerIntoKeycloakGroup()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var handler = new RecordingHttpHandler { Responder = Responder() };
        var svc = NewService(db, handler);

        await svc.CreateAsync("kc-owner", new CreateTenantInput("BJ003", "北京西单店", "store", null));

        // /tenants/t_bj003 尚不存在 → 先创建，再按 sub 入组
        handler.Has(HttpMethod.Get, p => p.EndsWith("/children")).Should().BeTrue(
            "应先确认 /tenants 下的子组是否存在");
        handler.Has(HttpMethod.Post, p => p.EndsWith("/groups/g-tenants/children")).Should().BeTrue(
            "新门店的组应被创建");
        handler.Has(HttpMethod.Put, p => p.EndsWith("/users/kc-owner/groups/g-t_bj003")).Should().BeTrue(
            "owner 应被加入 /tenants/t_bj003");
    }

    [Fact]
    public async Task Create_WhenKeycloakDisabled_StillCreatesTenantButNotSynced()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var handler = new RecordingHttpHandler { Responder = Responder() };
        var svc = NewService(db, handler, enabled: false);

        var r = await svc.CreateAsync("kc-owner",
            new CreateTenantInput("GZ004", "广州天河店", "store", null));

        r.Success.Should().BeTrue("Keycloak 不可用不应阻塞建店");
        r.GroupSynced.Should().BeFalse();
        handler.Requests.Should().BeEmpty();

        var m = await db.Memberships.IgnoreQueryFilters().SingleAsync(x => x.KcSub == "kc-owner");
        m.KcSyncState.Should().Be(MembershipSyncState.Pending, "留给对账器重试");
    }

    // ── 校验与防护 ────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("有中文")]
    [InlineData("has space")]
    [InlineData("bad/code")]
    [InlineData("-leading")]
    public async Task Create_InvalidTenantCode_IsRejected(string code)
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var svc = NewService(db, new RecordingHttpHandler { Responder = Responder() });

        var r = await svc.CreateAsync("kc-x", new CreateTenantInput(code, "门店", "store", null));

        r.Success.Should().BeFalse();
        r.Error.Should().Contain("租户编码");
    }

    [Fact]
    public async Task Create_DuplicateTenantCode_IsRejected()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var svc = NewService(db, new RecordingHttpHandler { Responder = Responder() });

        // 种子里已有 SH001 → t_shanghai_001
        var r = await svc.CreateAsync("kc-x", new CreateTenantInput("SH001", "重名店", "store", null));

        r.Success.Should().BeFalse();
        r.Error.Should().Contain("已被占用");
    }

    [Fact]
    public async Task Create_EmptyTenantName_IsRejected()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var svc = NewService(db, new RecordingHttpHandler { Responder = Responder() });

        var r = await svc.CreateAsync("kc-x", new CreateTenantInput("SZ005", "  ", "store", null));

        r.Success.Should().BeFalse();
        r.Error.Should().Contain("名称");
    }

    [Fact]
    public async Task Create_UnknownTenantType_IsRejected()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var svc = NewService(db, new RecordingHttpHandler { Responder = Responder() });

        var r = await svc.CreateAsync("kc-x", new CreateTenantInput("SZ006", "店", "warehouse", null));

        r.Success.Should().BeFalse();
        r.Error.Should().Contain("类型", "文案应中性化为「组织类型」，不能写死门店");
    }

    [Fact]
    public async Task Create_NonExistentParent_IsRejected()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var svc = NewService(db, new RecordingHttpHandler { Responder = Responder() });

        var r = await svc.CreateAsync("kc-x",
            new CreateTenantInput("SZ007", "加盟店", "franchise", "t_not_exist"));

        r.Success.Should().BeFalse();
        r.Error.Should().Contain("上级租户");
    }

    [Fact]
    public async Task Create_ExceedingOwnedTenantCap_IsRejected()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var svc = NewService(db, new RecordingHttpHandler { Responder = Responder() });

        for (var i = 0; i < TenantService.MaxOwnedTenants; i++)
        {
            var ok = await svc.CreateAsync("kc-greedy",
                new CreateTenantInput($"CAP{i:00}", $"店{i}", "store", null));
            ok.Success.Should().BeTrue(ok.Error);
        }

        var over = await svc.CreateAsync("kc-greedy",
            new CreateTenantInput("CAPOVER", "超额店", "store", null));

        over.Success.Should().BeFalse();
        over.Error.Should().Contain("上限");
    }

    [Fact]
    public async Task Create_DifferentUsers_CanUseSameCodeOnceEach()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var svc = NewService(db, new RecordingHttpHandler { Responder = Responder() });

        (await svc.CreateAsync("kc-u1", new CreateTenantInput("SZ008", "店A", "store", null)))
            .Success.Should().BeTrue();

        var second = await svc.CreateAsync("kc-u2",
            new CreateTenantInput("SZ008", "店B", "store", null));
        second.Success.Should().BeFalse("编码全局唯一，与创建者无关");
    }

    // ── 与既有能力的衔接 ──────────────────────────────────

    [Fact]
    public async Task CreatedTenant_AppearsInWorkspaces_AndCallerCanInvite()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var createSvc = NewService(db, new RecordingHttpHandler { Responder = Responder() });

        await createSvc.CreateAsync("kc-new-owner",
            new CreateTenantInput("HZ009", "杭州西湖店", "store", null));

        // 建店后，同一用户在该租户内应有 owner 权限（能邀请）
        using var scoped = holder.NewContext("t_hz009");
        var auth = new TenantAuthorization(scoped);
        (await auth.CanInviteAsync("kc-new-owner", "t_hz009")).Should().BeTrue();
        (await auth.GetRoleAsync("kc-new-owner", "t_hz009")).Should().Be(TenantAuthorization.RoleOwner);

        // 并且只影响这个租户——别的租户无权限
        (await auth.CanInviteAsync("kc-new-owner", "t_shanghai_001")).Should().BeFalse();
    }

    // ── 种子噪声消除 ──────────────────────────────────────

    [Fact]
    public async Task SeededPlaceholderMemberships_AreMarkedUnmanaged()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();

        var demo = await db.Memberships.IgnoreQueryFilters()
            .Where(m => new[] { "kc-zhangsan", "kc-lisi", "kc-wangwu" }.Contains(m.KcSub))
            .ToListAsync();

        demo.Should().NotBeEmpty("种子数据仍保留演示用成员关系");
        demo.Should().OnlyContain(m => m.KcSyncState == MembershipSyncState.Unmanaged,
            "占位 sub 在真实 Keycloak 中不存在，不能让对账器每轮重试");
    }

    [Fact]
    public async Task Reconciler_SkipsUnmanagedMemberships()
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var handler = new RecordingHttpHandler { Responder = Responder() };

        var reconciler = new GroupSyncReconciler(db, NewKeycloak(handler),
            NullLogger<GroupSyncReconciler>.Instance, new MutableTimeProvider());

        var result = await reconciler.ReconcileAsync();

        result.Scanned.Should().Be(0,
            "种子里全是 unmanaged 占位 sub，不应产生任何对账请求");
        handler.Requests.Should().BeEmpty("没有待同步项时不应触碰 Keycloak");
    }

    [Theory]
    [InlineData("company", "华东销售公司")]
    [InlineData("subsidiary", "华南子公司")]
    [InlineData("franchise", "西南加盟店")]
    public async Task Create_NonStoreTenantTypes_AreAccepted(string type, string name)
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        var (holder, db) = NewDb();
        var svc = NewService(db, new RecordingHttpHandler { Responder = Responder() });

        var r = await svc.CreateAsync("kc-org-owner",
            new CreateTenantInput($"ORG{type.ToUpperInvariant()}", name, type, null));

        r.Success.Should().BeTrue(r.Error);
        var t = await db.Tenants.IgnoreQueryFilters()
            .SingleAsync(x => x.TenantId == r.TenantId);
        t.TenantType.Should().Be(type,
            "子公司/企业的销售也会用邀请机制邀请客户，不能只支持 store");
    }
}
