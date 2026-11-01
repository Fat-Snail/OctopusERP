using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OctopusRetail.Api.Persistence;
using OctopusRetail.Api.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// M3 — Keycloak 受控建号 + group 双写对账。
/// 用自定义 HttpMessageHandler 截获 Keycloak Admin REST 调用。
/// </summary>
public class KeycloakSyncTests
{
    // ── 基建 ──────────────────────────────────────────────

    private static IConfiguration Config(bool enabled = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:BaseUrl"] = "http://fake-kc",
            ["Keycloak:Realm"] = "octopus",
            ["Keycloak:BotClientId"] = "retail-invite-bot",
            ["Keycloak:BotClientSecret"] = "secret",
            ["Keycloak:SyncEnabled"] = enabled ? "true" : "false",
        }!).Build();

    private class FakeHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Requests.Add(req);
            Bodies.Add(req.Content is null ? "" : await req.Content.ReadAsStringAsync(ct));
            if (req.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token"))
                return Json(new { access_token = "fake", expires_in = 300 });
            return Responder?.Invoke(req) ?? new HttpResponseMessage(HttpStatusCode.OK);
        }

        public static HttpResponseMessage Json(object o, HttpStatusCode code = HttpStatusCode.OK) =>
            new(code) { Content = JsonContent.Create(o) };

        public static HttpResponseMessage Raw(string s, HttpStatusCode code = HttpStatusCode.OK) =>
            new(code) { Content = new StringContent(s, Encoding.UTF8, "application/json") };

        public int Count(HttpMethod m, Func<string, bool> match) =>
            Requests.Count(r => r.Method == m && match(r.RequestUri!.AbsolutePath));

        public bool Has(HttpMethod m, Func<string, bool> match) => Count(m, match) > 0;

        public string BodyOf(HttpMethod m, Func<string, bool> match)
        {
            var i = Requests.FindIndex(r => r.Method == m && match(r.RequestUri!.AbsolutePath));
            return i < 0 ? "" : Bodies[i];
        }
    }

    private class FakeFactory(HttpMessageHandler h) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(h) { Timeout = TimeSpan.FromSeconds(5) };
    }

    private static void ResetTokenCache()
    {
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var t = typeof(KeycloakAdminClient);
        t.GetField("_cachedToken", flags)?.SetValue(null, null);
        t.GetField("_cachedTokenExpiry", flags)?.SetValue(null, DateTime.MinValue);
    }

    private static KeycloakAdminClient NewClient(FakeHandler h, bool enabled = true) =>
        new(new FakeFactory(h), Config(enabled), NullLogger<KeycloakAdminClient>.Instance);

    private const string TenantsTree = """
    [{ "id":"g-tenants","name":"tenants","path":"/tenants","subGroups":[
        { "id":"g-sh","name":"t_shanghai_001","path":"/tenants/t_shanghai_001","subGroups":[] }
    ]}]
    """;

    private static (SqliteConnection conn, DbContextOptions<RetailDbContext> options) NewStore()
    {
        var conn = new SqliteConnection($"Data Source=m3_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        conn.Open();
        return (conn, new DbContextOptionsBuilder<RetailDbContext>().UseSqlite(conn).Options);
    }

    private static RetailDbContext NewDb(DbContextOptions<RetailDbContext> options, string? tenant = null) =>
        new(options, TestTenantContext.For(tenant));

    // ── 受控建号 ──────────────────────────────────────────

    [Fact]
    public async Task CreateUser_PostsWithForcedPasswordChange_NotPublicRegistration()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = req =>
                req.RequestUri!.AbsolutePath.EndsWith("/users") && req.Method == HttpMethod.Post
                    ? new HttpResponseMessage(HttpStatusCode.Created)
                    : FakeHandler.Raw("""[{"id":"kc-new-uuid","username":"lisi"}]""")
        };
        var client = NewClient(handler);

        var id = await client.CreateUserAsync("lisi", "lisi@shop.com", "李四");

        id.Should().Be("kc-new-uuid");

        var body = handler.BodyOf(HttpMethod.Post, p => p.EndsWith("/users"));
        body.Should().Contain("UPDATE_PASSWORD",
            "必须用 required action 强制首次改密，而不是打开 realm 的 registrationAllowed");
        body.Should().Contain("\"temporary\":true");
        body.Should().NotContain("\"emailVerified\":true");
    }

    [Fact]
    public async Task CreateUser_WhenDisabled_ReturnsNullWithoutHttp()
    {
        ResetTokenCache();
        var handler = new FakeHandler();
        var client = NewClient(handler, enabled: false);

        (await client.CreateUserAsync("x", "x@y.com", "X")).Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    // ── group 原语（按 sub，不回查用户名） ────────────────

    [Fact]
    public async Task AddUserToGroup_UsesSubDirectly_WithoutUsernameLookup()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = _ => FakeHandler.Raw(TenantsTree) };
        var client = NewClient(handler);

        var ok = await client.AddUserToGroupAsync("kc-sub-abc", "/tenants/t_shanghai_001");

        ok.Should().BeTrue();
        handler.Has(HttpMethod.Put, p => p.EndsWith("/users/kc-sub-abc/groups/g-sh")).Should().BeTrue();
        handler.Count(HttpMethod.Get, p => p.EndsWith("/users")).Should().Be(0,
            "sub 就是 Keycloak user id，无需按用户名回查");
    }

    [Fact]
    public async Task RemoveUserFromGroup_UsesSubDirectly()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = _ => FakeHandler.Raw(TenantsTree) };
        var client = NewClient(handler);

        (await client.RemoveUserFromGroupAsync("kc-sub-abc", "/tenants/t_shanghai_001")).Should().BeTrue();
        handler.Has(HttpMethod.Delete, p => p.EndsWith("/users/kc-sub-abc/groups/g-sh")).Should().BeTrue();
    }

    [Fact]
    public async Task EnsureGroup_CreatesMissingChildUnderTenantsRoot()
    {
        ResetTokenCache();
        var created = false;
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (req.Method == HttpMethod.Get && path.EndsWith("/groups"))
                    return FakeHandler.Raw(created
                        ? TenantsTree
                        : """[{"id":"g-tenants","name":"tenants","path":"/tenants","subGroups":[]}]""");
                if (req.Method == HttpMethod.Post && path.EndsWith("/children"))
                {
                    created = true;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        var client = NewClient(handler);

        (await client.EnsureGroupAsync(KeycloakAdminClient.TenantGroupPath("t_shanghai_001")))
            .Should().Be("g-sh");
        handler.Has(HttpMethod.Post, p => p.EndsWith("/groups/g-tenants/children")).Should().BeTrue();
    }

    [Fact]
    public async Task TenantGroupPath_IsUnderTenantsRoot()
    {
        KeycloakAdminClient.TenantGroupPath("t_shanghai_001").Should().Be("/tenants/t_shanghai_001");
    }

    // ── Keycloak 26 真实响应形态回归 ──────────────────────
    //
    // 背景：上面的 TenantsTree 把 subGroups 内联了，但 Keycloak 26 实测返回的是
    // {"path":"/tenants","subGroupCount":1,"subGroups":[]} —— 子组不内联，
    // 必须显式拉 /groups/{id}/children。第一版实现递归空 subGroups，
    // 导致租户组路径永远解析不出来（真实环境表现为反复创建已存在的组 → 409 → 同步失败）。

    private const string Tree26Root = """
    [{ "id":"g-tenants","name":"tenants","path":"/tenants","subGroupCount":1,"subGroups":[] }]
    """;

    private const string Tree26Children = """
    [{ "id":"g-sh","name":"t_shanghai_001","path":"/tenants/t_shanghai_001","subGroupCount":0,"subGroups":[] }]
    """;

    private static Func<HttpRequestMessage, HttpResponseMessage> Shape26Responder(bool created = true) =>
        req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Get && path.EndsWith("/groups")) return FakeHandler.Raw(Tree26Root);
            if (req.Method == HttpMethod.Get && path.EndsWith("/children"))
                return FakeHandler.Raw(created ? Tree26Children : "[]");
            if (req.Method == HttpMethod.Post) return new HttpResponseMessage(HttpStatusCode.Created);
            if (req.Method == HttpMethod.Put || req.Method == HttpMethod.Delete)
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

    [Fact]
    public async Task EnsureGroup_Keycloak26Shape_ExpandsViaChildrenEndpoint()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = Shape26Responder(created: true) };
        var client = NewClient(handler);

        var id = await client.EnsureGroupAsync("/tenants/t_shanghai_001");

        id.Should().Be("g-sh", "子组必须经 /groups/{id}/children 展开后才能解析");
        handler.Count(HttpMethod.Post, p => !p.Contains("/protocol/openid-connect/token"))
            .Should().Be(0, "组已存在时不应再创建（真实环境会 409）");
    }

    [Fact]
    public async Task Reconcile_Keycloak26Shape_AddsUserToNestedTenantGroup()
    {
        ResetTokenCache();
        var (conn, options) = NewStore();
        using var _ = conn;

        using (var db = NewDb(options))
        {
            db.Database.EnsureCreated();
            db.Memberships.Add(new RetailMembership
            {
                KcSub = "kc-sub-26", TenantId = "t_shanghai_001",
                RoleInTenant = "staff", Status = "active",
                JoinedAt = DateTime.UtcNow, KcSyncState = "pending",
            });
            await db.SaveChangesAsync();
        }

        var handler = new FakeHandler { Responder = Shape26Responder(created: true) };
        GroupSyncResult result;
        using (var db = NewDb(options))
        {
            var reconciler = new GroupSyncReconciler(db, NewClient(handler),
                NullLogger<GroupSyncReconciler>.Instance, new MutableTimeProvider());
            result = await reconciler.ReconcileAsync();
        }

        result.Synced.Should().Be(1);
        handler.Has(HttpMethod.Put, p => p.EndsWith("/users/kc-sub-26/groups/g-sh"))
            .Should().BeTrue("必须能解析出嵌套租户组的 id 才能完成入组");
    }

    [Fact]
    public async Task GroupOps_OnHttpFailure_DoNotThrow()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = _ => throw new HttpRequestException("down") };
        var client = NewClient(handler);

        await FluentActions.Awaiting(() => client.EnsureGroupAsync("/tenants/t_a")).Should().NotThrowAsync();
        await FluentActions.Awaiting(() => client.AddUserToGroupAsync("s", "/tenants/t_a")).Should().NotThrowAsync();
    }

    // ── 对账器 ────────────────────────────────────────────

    [Fact]
    public async Task Reconcile_ActivePendingMembership_PushesGroupAndMarksSynced()
    {
        ResetTokenCache();
        var (conn, options) = NewStore();
        using var _ = conn;

        long membershipId;
        using (var db = NewDb(options))
        {
            db.Database.EnsureCreated();
            var m = new RetailMembership
            {
                KcSub = "kc-sub-1", TenantId = "t_shanghai_001",
                RoleInTenant = "staff", Status = "active",
                JoinedAt = DateTime.UtcNow, KcSyncState = "pending",
            };
            db.Memberships.Add(m);
            await db.SaveChangesAsync();
            membershipId = m.MembershipId;
        }

        var handler = new FakeHandler { Responder = _ => FakeHandler.Raw(TenantsTree) };
        var client = NewClient(handler);

        int scanned, synced, failed;
        using (var db = NewDb(options))
        {
            var reconciler = new GroupSyncReconciler(db, client,
                NullLogger<GroupSyncReconciler>.Instance, new MutableTimeProvider());
            var result = await reconciler.ReconcileAsync();
            (scanned, synced, failed) = (result.Scanned, result.Synced, result.Failed);
        }

        scanned.Should().Be(1);
        synced.Should().Be(1);
        failed.Should().Be(0);

        handler.Has(HttpMethod.Put, p => p.EndsWith("/users/kc-sub-1/groups/g-sh"))
            .Should().BeTrue("对账应把用户加入对应租户 group");

        using (var db = NewDb(options))
        {
            var m = await db.Memberships.IgnoreQueryFilters().SingleAsync(x => x.MembershipId == membershipId);
            m.KcSyncState.Should().Be("synced");
            m.KcSyncedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Reconcile_NonActiveMembership_RemovesFromGroup()
    {
        ResetTokenCache();
        var (conn, options) = NewStore();
        using var _ = conn;

        using (var db = NewDb(options))
        {
            db.Database.EnsureCreated();
            db.Memberships.Add(new RetailMembership
            {
                KcSub = "kc-sub-left", TenantId = "t_shanghai_001",
                RoleInTenant = "staff", Status = "left",
                JoinedAt = DateTime.UtcNow, LeftAt = DateTime.UtcNow, KcSyncState = "pending",
            });
            await db.SaveChangesAsync();
        }

        var handler = new FakeHandler { Responder = _ => FakeHandler.Raw(TenantsTree) };
        using (var db = NewDb(options))
        {
            var reconciler = new GroupSyncReconciler(db, NewClient(handler),
                NullLogger<GroupSyncReconciler>.Instance, new MutableTimeProvider());
            await reconciler.ReconcileAsync();
        }

        handler.Has(HttpMethod.Delete, p => p.EndsWith("/users/kc-sub-left/groups/g-sh"))
            .Should().BeTrue("离职成员必须被移出租户 group，否则仍能进入该店");
    }

    [Fact]
    public async Task Reconcile_WhenKeycloakDisabled_SkipsAndLeavesStateUntouched()
    {
        ResetTokenCache();
        var (conn, options) = NewStore();
        using var _ = conn;

        using (var db = NewDb(options))
        {
            db.Database.EnsureCreated();
            db.Memberships.Add(new RetailMembership
            {
                KcSub = "kc-sub-skip", TenantId = "t_shanghai_001",
                RoleInTenant = "staff", Status = "active",
                JoinedAt = DateTime.UtcNow, KcSyncState = "pending",
            });
            await db.SaveChangesAsync();
        }

        var handler = new FakeHandler();
        GroupSyncResult result;
        using (var db = NewDb(options))
        {
            var reconciler = new GroupSyncReconciler(db, NewClient(handler, enabled: false),
                NullLogger<GroupSyncReconciler>.Instance, new MutableTimeProvider());
            result = await reconciler.ReconcileAsync();
        }

        result.Skipped.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
        using (var db = NewDb(options))
        {
            var m = await db.Memberships.IgnoreQueryFilters().SingleAsync(x => x.KcSub == "kc-sub-skip");
            m.KcSyncState.Should().Be("pending",
                "Keycloak 不可用时保持待同步，而不是误标为已同步");
        }
    }

    [Fact]
    public async Task Reconcile_WhenKeycloakFails_MarksFailedWithoutThrowing()
    {
        ResetTokenCache();
        var (conn, options) = NewStore();
        using var _ = conn;

        using (var db = NewDb(options))
        {
            db.Database.EnsureCreated();
            db.Memberships.Add(new RetailMembership
            {
                KcSub = "kc-sub-fail", TenantId = "t_shanghai_001",
                RoleInTenant = "staff", Status = "active",
                JoinedAt = DateTime.UtcNow, KcSyncState = "pending",
            });
            await db.SaveChangesAsync();
        }

        var handler = new FakeHandler { Responder = _ => throw new HttpRequestException("kc down") };
        GroupSyncResult result;
        using (var db = NewDb(options))
        {
            var reconciler = new GroupSyncReconciler(db, NewClient(handler),
                NullLogger<GroupSyncReconciler>.Instance, new MutableTimeProvider());
            result = await reconciler.ReconcileAsync();
        }

        result.Failed.Should().Be(1);
        using (var db = NewDb(options))
        {
            var m = await db.Memberships.IgnoreQueryFilters().SingleAsync(x => x.KcSub == "kc-sub-fail");
            m.KcSyncState.Should().Be("failed");
            m.KcSyncedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task Reconcile_AlreadySynced_IsNotRepushed()
    {
        ResetTokenCache();
        var (conn, options) = NewStore();
        using var _ = conn;

        using (var db = NewDb(options))
        {
            db.Database.EnsureCreated();
            db.Memberships.Add(new RetailMembership
            {
                KcSub = "kc-sub-done", TenantId = "t_shanghai_001",
                RoleInTenant = "staff", Status = "active",
                JoinedAt = DateTime.UtcNow, KcSyncState = "synced",
                KcSyncedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var handler = new FakeHandler { Responder = _ => FakeHandler.Raw(TenantsTree) };
        using (var db = NewDb(options))
        {
            var reconciler = new GroupSyncReconciler(db, NewClient(handler),
                NullLogger<GroupSyncReconciler>.Instance, new MutableTimeProvider());
            var result = await reconciler.ReconcileAsync();
            result.Scanned.Should().Be(0);
        }

        handler.Requests.Should().BeEmpty("已同步的成员关系不应被重复推送");
    }

    [Fact]
    public async Task Reconcile_ScansAcrossTenants_DespiteTenantFilter()
    {
        ResetTokenCache();
        var (conn, options) = NewStore();
        using var _ = conn;

        using (var db = NewDb(options))
        {
            db.Database.EnsureCreated();
            db.Memberships.AddRange(
                new RetailMembership
                {
                    KcSub = "kc-a", TenantId = "t_shanghai_001", RoleInTenant = "staff",
                    Status = "active", JoinedAt = DateTime.UtcNow, KcSyncState = "pending",
                },
                new RetailMembership
                {
                    KcSub = "kc-b", TenantId = "t_beijing_002", RoleInTenant = "staff",
                    Status = "active", JoinedAt = DateTime.UtcNow, KcSyncState = "pending",
                });
            await db.SaveChangesAsync();
        }

        var handler = new FakeHandler { Responder = _ => FakeHandler.Raw(TenantsTree) };
        using (var db = NewDb(options))
        {
            var reconciler = new GroupSyncReconciler(db, NewClient(handler),
                NullLogger<GroupSyncReconciler>.Instance, new MutableTimeProvider());
            var result = await reconciler.ReconcileAsync();
            result.Scanned.Should().Be(2, "后台对账无租户上下文，必须显式跨租户扫描");
        }
    }
}
