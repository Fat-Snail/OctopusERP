using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OctopusRetail.Api.Persistence;
using OctopusRetail.Api.Services;
using System.Net;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// M3 — 受控自助建号（design §5.2）。
///
/// 验证核心安全主张：**不打开 realm 的 registrationAllowed**，建号只能由持有
/// retail-invite-bot 服务账号的服务端发起，且必须凭有效的邀请令牌。
/// </summary>
public class InviteRegisterTests
{
    private static IConfiguration Config(bool keycloakEnabled = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:BaseUrl"] = "http://fake-kc",
            ["Keycloak:Realm"] = "octopus",
            ["Keycloak:BotClientId"] = "retail-invite-bot",
            ["Keycloak:BotClientSecret"] = "secret",
            ["Keycloak:SyncEnabled"] = keycloakEnabled ? "true" : "false",
        }!).Build();

    private const string TenantsTree = """
    [{ "id":"g-tenants","name":"tenants","path":"/tenants","subGroups":[
        { "id":"g-sh","name":"t_shanghai_001","path":"/tenants/t_shanghai_001","subGroups":[] }
    ]}]
    """;

    private static (SqliteConnection conn, DbContextOptions<RetailDbContext> options) NewStore()
    {
        var conn = new SqliteConnection($"Data Source=reg_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        conn.Open();
        return (conn, new DbContextOptionsBuilder<RetailDbContext>().UseSqlite(conn).Options);
    }

    /// <summary>装配 InviteService + 假 Keycloak，返回服务与已创建的邀请明文令牌</summary>
    private static async Task<(InviteService svc, string rawToken, long inviteId)> Arrange(
        DbContextOptions<RetailDbContext> options,
        RecordingHttpHandler handler,
        MutableTimeProvider clock,
        RecordingHttpHandler? umcHandler = null,
        bool keycloakEnabled = true,
        string? phone = "13900001111",
        int validHours = 72)
    {
        RecordingHttpHandler.ResetKeycloakTokenCache();
        umcHandler ??= new RecordingHttpHandler();

        var kc = new KeycloakAdminClient(
            new RecordingHttpClientFactory(handler), Config(keycloakEnabled),
            NullLogger<KeycloakAdminClient>.Instance);

        // 建号响应：POST /users → 201；GET /users?username → 返回一个 id
        // 假 Keycloak：建号 + 组树 + 入组 三条路径都要支持，
        // 因为入店后会**立即**尝试同步 group（不再只写 pending 等对账）
        handler.Responder ??= req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Post && path.EndsWith("/users"))
                return new HttpResponseMessage(HttpStatusCode.Created);
            if (req.Method == HttpMethod.Get && path.EndsWith("/groups"))
                return RecordingHttpHandler.Raw(
                    """[{"id":"g-tenants","name":"tenants","path":"/tenants","subGroupCount":1,"subGroups":[]}]""");
            if (req.Method == HttpMethod.Get && path.EndsWith("/children"))
                return RecordingHttpHandler.Raw(
                    """[{"id":"g-sh","name":"t_shanghai_001","path":"/tenants/t_shanghai_001","subGroupCount":0,"subGroups":[]}]""");
            if (req.Method == HttpMethod.Put)
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            return RecordingHttpHandler.Raw("""[{"id":"kc-new-sub","username":"newstaff"}]""");
        };

        using var db = new RetailDbContext(options, TestTenantContext.For("t_shanghai_001"));
        db.Database.EnsureCreated();
        RetailDbSeeder.Seed(db);

        var tenantCtx = TestTenantContext.For("t_shanghai_001");
        var db2 = new RetailDbContext(options, tenantCtx);
        var auth = new TenantAuthorization(db2);
        // ADR-010：建号走 UMC。UMC 的桩由调用方通过 umcHandler 定制，
        // 默认返回"建号成功 + kcSub"。
        umcHandler.Responder ??= _ => RecordingHttpHandler.Raw(
            """{"code":200,"msg":"ok","data":{"userId":999,"kcSub":"kc-umbuilt","userName":"newstaff"}}""");

        var umc = new UmcUserClient(
            new RecordingHttpClientFactory(umcHandler),
            new ConfigurationBuilder().Build(),
            NullLogger<UmcUserClient>.Instance);

        var svc = new InviteService(db2, auth, tenantCtx, kc, umc, clock);

        var created = await svc.CreateAsync("kc-zhangsan", new CreateInviteInput(
            phone, null, "staff", 1, TimeSpan.FromHours(validHours)));

        created.Success.Should().BeTrue();
        return (svc, created.RawToken!, created.Invite!.InviteId);
    }

    // ── 正常路径 ──────────────────────────────────────────

    [Fact]
    public async Task Register_ValidToken_CreatesAccountAndMembership()
    {
        var (conn, options) = NewStore();
        using var _ = conn;
        var handler = new RecordingHttpHandler();
        var umcHandler = new RecordingHttpHandler();
        var (svc, token, inviteId) = await Arrange(
            options, handler, new MutableTimeProvider(), umcHandler);

        var result = await svc.RegisterAsync(token, "newstaff", "新店员", "13900001111", "Passw0rd!");

        result.Success.Should().BeTrue(result.Error);
        result.UserName.Should().Be("newstaff");
        result.TenantId.Should().Be("t_shanghai_001");

        umcHandler.Has(HttpMethod.Post, p => p.Contains("/api/internal/users/retail"))
            .Should().BeTrue("ADR-010：账号必须由 UMC 创建，而不是零售直连 Keycloak 建号");

        // 成员关系应立即建立，并**立即**同步到 Keycloak（不等后台对账）——
        // 否则用户登录后 token 里没有该租户 group，任何租户内接口都会 403
        using var db = new RetailDbContext(options, TestTenantContext.For(null));
        var m = await db.Memberships.IgnoreQueryFilters()
            .SingleAsync(x => x.KcSub == "kc-umbuilt" && x.TenantId == "t_shanghai_001");
        m.Status.Should().Be("active");
        m.KcSyncState.Should().Be(MembershipSyncState.Synced);

        // 手机号必须落库。旧实现只把它当成一次性校验值后丢弃，
        // 导致事后在 UMC 和零售库两头都查不到（实测 feiwoniu001 踩过）。
        m.Phone.Should().Be("13900001111", "注册时填的手机号要作为成员快照保存");
        m.UserName.Should().Be("newstaff");

        // 使用额度被消耗
        var invite = await db.Invites.IgnoreQueryFilters().SingleAsync(i => i.InviteId == inviteId);
        invite.UsedCount.Should().Be(1);
        invite.State.Should().Be(InviteState.Accepted);
        invite.AcceptedBySub.Should().Be("kc-umbuilt",
            "成员关系用的 sub 应当来自 UMC 建号结果，而不是零售自己猜的");
    }

    [Fact]
    public async Task Register_UsesChosenPassword_WithoutForcedChange()
    {
        var (conn, options) = NewStore();
        using var _ = conn;
        var handler = new RecordingHttpHandler();
        var umcHandler = new RecordingHttpHandler();
        var (svc, token, _) = await Arrange(
            options, handler, new MutableTimeProvider(), umcHandler);

        await svc.RegisterAsync(token, "newstaff", "新店员", "13900001111", "Passw0rd!");

        umcHandler.Bodies.Should().Contain(b => b.Contains("Passw0rd!"),
            "ADR-010：被邀请人设定的密码要交给 UMC，由 UMC 写入 Keycloak");

        // 零售侧不再直接改 Keycloak 密码（改由 UMC 内部完成）
        handler.Has(HttpMethod.Put, p => p.Contains("/reset-password"))
            .Should().BeFalse("密码下发已收口到 UMC，零售不应再直连 Keycloak 改密");
    }

    // ── 安全边界 ──────────────────────────────────────────

    [Fact]
    public async Task Register_PhoneMismatch_IsRejected()
    {
        var (conn, options) = NewStore();
        using var _ = conn;
        var handler = new RecordingHttpHandler();
        var (svc, token, _) = await Arrange(options, handler, new MutableTimeProvider(),
            phone: "13900001111");

        // 定向邀请被转发给他人：手机号对不上必须拒绝
        var result = await svc.RegisterAsync(token, "attacker", "路人", "13800009999", "Passw0rd!");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("手机号");
        handler.Has(HttpMethod.Post, p => p.EndsWith("/users"))
            .Should().BeFalse("手机号不匹配时绝不能建号");
    }

    [Fact]
    public async Task Register_PhoneMatchToleratesFormatting()
    {
        var (conn, options) = NewStore();
        using var _ = conn;
        var handler = new RecordingHttpHandler();
        var (svc, token, _) = await Arrange(options, handler, new MutableTimeProvider(),
            phone: "+86 139-0000-1111");

        var result = await svc.RegisterAsync(token, "newstaff", "新店员", "13900001111", "Passw0rd!");

        result.Success.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task Register_ShortPassword_IsRejected()
    {
        var (conn, options) = NewStore();
        using var _ = conn;
        var handler = new RecordingHttpHandler();
        var (svc, token, _) = await Arrange(options, handler, new MutableTimeProvider());

        var result = await svc.RegisterAsync(token, "newstaff", "新店员", "13900001111", "123");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("密码");
        handler.Has(HttpMethod.Post, p => p.EndsWith("/users")).Should().BeFalse();
    }

    [Fact]
    public async Task Register_WhenKeycloakDisabled_IsRejectedWithoutHttp()
    {
        var (conn, options) = NewStore();
        using var _ = conn;
        var handler = new RecordingHttpHandler();
        var (svc, token, _) = await Arrange(options, handler, new MutableTimeProvider(),
            keycloakEnabled: false);

        var result = await svc.RegisterAsync(token, "newstaff", "新店员", "13900001111", "Passw0rd!");

        result.Success.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_ExpiredInvite_IsRejected()
    {
        var (conn, options) = NewStore();
        using var _ = conn;
        var handler = new RecordingHttpHandler();
        var clock = new MutableTimeProvider();
        var (svc, token, _) = await Arrange(options, handler, clock, validHours: 1);

        clock.Advance(TimeSpan.FromHours(2));

        var result = await svc.RegisterAsync(token, "newstaff", "新店员", "13900001111", "Passw0rd!");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("过期");
        handler.Has(HttpMethod.Post, p => p.EndsWith("/users")).Should().BeFalse();
    }

    [Fact]
    public async Task Register_UnknownToken_IsRejected()
    {
        var (conn, options) = NewStore();
        using var _ = conn;
        var handler = new RecordingHttpHandler();
        var (svc, _, _) = await Arrange(options, handler, new MutableTimeProvider());

        var result = await svc.RegisterAsync("bogus-token", "newstaff", "新店员", "13900001111", "Passw0rd!");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("不存在");
    }

    [Fact]
    public async Task Register_WhenUmcRejectsCreation_ReportsFailure()
    {
        var (conn, options) = NewStore();
        using var _ = conn;
        // ADR-010 之后建号在 UMC，所以失败场景要由 UMC 返回（这里模拟用户名被占用 409）
        var handler = new RecordingHttpHandler();
        var umcHandler = new RecordingHttpHandler
        {
            Responder = _ => RecordingHttpHandler.Raw(
                """{"code":409,"msg":"用户名已被占用","data":null}""", HttpStatusCode.Conflict),
        };
        var (svc, token, _) = await Arrange(
            options, handler, new MutableTimeProvider(), umcHandler);

        var result = await svc.RegisterAsync(token, "taken", "重名", "13900001111", "Passw0rd!");

        result.Success.Should().BeFalse();
        // ADR-010 之后建号在 UMC，UMC 会给出确定原因（这里 409 用户名占用）。
        // 比过去零售自己猜"可能重名"更准确——过去曾把 TLS 故障误导成重名。
        result.Error.Should().Contain("用户名已被占用");

        // 建号失败不得建立成员关系，也不应白白消耗额度
        using var db = new RetailDbContext(options, TestTenantContext.For(null));
        (await db.Memberships.IgnoreQueryFilters().CountAsync(m => m.TenantId == "t_shanghai_001"))
            .Should().Be(2, "种子里上海店已有 zhangsan 与 lisi 两人");
    }
}
