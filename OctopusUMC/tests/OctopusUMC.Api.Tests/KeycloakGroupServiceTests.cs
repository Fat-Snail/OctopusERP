using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OctopusUMC.Api.Services;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// M0 — Keycloak Group 同步（弥补 MINIAPP-PLATFORM.md M2 的欠债）
///
/// 用自定义 HttpMessageHandler 截获 Keycloak Admin REST 调用，验证：
/// - 组树缺失时按层级创建（顶层 POST /groups，子级 POST /groups/{parentId}/children）
/// - 已存在的组不重复创建
/// - 用户加入 / 移出组（PUT / DELETE /users/{uid}/groups/{gid}）
/// - 读取用户全部组路径
/// - reconcile 同步：只增删「受管前缀」内的组，不动前缀外的组（安全边界）
/// - SyncEnabled=false 时静默 skip
/// </summary>
public class KeycloakGroupServiceTests
{
    // ── 测试基建 ─────────────────────────────────────────

    private static IConfiguration BuildConfig(bool syncEnabled = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:BaseUrl"] = "http://fake-kc",
            ["Keycloak:Realm"] = "octopus",
            ["Keycloak:SyncBotClientId"] = "umc-sync-bot",
            ["Keycloak:SyncBotClientSecret"] = "secret",
            ["Keycloak:SyncEnabled"] = syncEnabled ? "true" : "false",
        }!).Build();

    private class FakeHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> RequestBodies { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));

            if (request.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token"))
                return Json(new { access_token = "fake-jwt", expires_in = 300 });

            return Responder?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.OK);
        }

        public static HttpResponseMessage Json(object o, HttpStatusCode code = HttpStatusCode.OK) =>
            new(code) { Content = JsonContent.Create(o) };

        public static HttpResponseMessage Raw(string json, HttpStatusCode code = HttpStatusCode.OK) =>
            new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        public bool HasRequest(HttpMethod method, Func<string, bool> pathMatch) =>
            Requests.Any(r => r.Method == method && pathMatch(r.RequestUri!.AbsolutePath));

        public int Count(HttpMethod method, Func<string, bool> pathMatch) =>
            Requests.Count(r => r.Method == method && pathMatch(r.RequestUri!.AbsolutePath));
    }

    private class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    private static void ResetTokenCache()
    {
        var t = typeof(KeycloakGroupService);
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        t.GetField("_cachedToken", flags)?.SetValue(null, null);
        t.GetField("_cachedTokenExpiry", flags)?.SetValue(null, DateTime.MinValue);
    }

    private static KeycloakGroupService NewService(FakeHandler handler, bool enabled = true) =>
        new(new FakeFactory(handler), BuildConfig(enabled), NullLogger<KeycloakGroupService>.Instance);

    /// <summary>组树 JSON：tenants / tenants/t_a / tenants/t_b</summary>
    private const string TreeWithTenantsAndChildren = """
    [
      { "id":"g-tenants","name":"tenants","path":"/tenants","subGroups":[
          { "id":"g-ta","name":"t_a","path":"/tenants/t_a","subGroups":[] },
          { "id":"g-tb","name":"t_b","path":"/tenants/t_b","subGroups":[] }
      ]}
    ]
    """;

    private const string TreeWithTenantsOnly = """
    [
      { "id":"g-tenants","name":"tenants","path":"/tenants","subGroups":[] }
    ]
    """;

    /// <summary>标准 Responder：用户查询返回 u1，组树按 tree 参数返回</summary>
    private static Func<HttpRequestMessage, HttpResponseMessage> StandardResponder(
        string groupTree,
        string? userGroups = null,
        Action<HttpRequestMessage>? onCreateGroup = null)
    {
        return req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            // 用户查询
            if (req.Method == HttpMethod.Get && path.EndsWith("/users"))
                return FakeHandler.Raw("""[{"id":"u1","username":"zhangsan"}]""");

            // 用户已有的组
            if (req.Method == HttpMethod.Get && path.EndsWith("/u1/groups"))
                return FakeHandler.Raw(userGroups ?? "[]");

            // 组树
            if (req.Method == HttpMethod.Get && path.EndsWith("/groups"))
                return FakeHandler.Raw(groupTree);

            // 创建组
            if (req.Method == HttpMethod.Post && (path.EndsWith("/groups") || path.EndsWith("/children")))
            {
                onCreateGroup?.Invoke(req);
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            // 加入组
            if (req.Method == HttpMethod.Put && path.Contains("/users/u1/groups/"))
                return new HttpResponseMessage(HttpStatusCode.NoContent);

            // 移出组
            if (req.Method == HttpMethod.Delete && path.Contains("/users/u1/groups/"))
                return new HttpResponseMessage(HttpStatusCode.NoContent);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
    }

    // ── EnsureGroupAsync ────────────────────────────────

    [Fact]
    public async Task EnsureGroupAsync_ExistingPath_ReturnsIdWithoutCreating()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = StandardResponder(TreeWithTenantsAndChildren) };
        var svc = NewService(handler);

        var id = await svc.EnsureGroupAsync("tenants/t_a");

        id.Should().Be("g-ta");
        handler.HasRequest(HttpMethod.Post, p => !p.Contains("/protocol/openid-connect/token"))
            .Should().BeFalse("组已存在，不应创建（token 端点除外）");
    }

    [Fact]
    public async Task EnsureGroupAsync_MissingChild_CreatesUnderExistingParent()
    {
        ResetTokenCache();
        string? parentPath = null;
        string? createBody = null;
        // 组树在创建动作发生前不含 t_a，发生后含 t_a（模拟 Keycloak 真实状态）
        var created = false;
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (req.Method == HttpMethod.Get && path.EndsWith("/groups"))
                    return FakeHandler.Raw(created ? TreeWithTenantsAndChildren : TreeWithTenantsOnly);
                if (req.Method == HttpMethod.Post && path.EndsWith("/children"))
                {
                    parentPath = path;
                    createBody = req.Content!.ReadAsStringAsync().Result;
                    created = true;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        var svc = NewService(handler);

        var id = await svc.EnsureGroupAsync("tenants/t_a");

        id.Should().Be("g-ta");
        parentPath.Should().NotBeNull().And.EndWith("/groups/g-tenants/children",
            "子组应创建在父组之下");
        createBody.Should().Contain("\"name\":\"t_a\"");
    }

    [Fact]
    public async Task EnsureGroupAsync_MissingTopLevel_CreatesRootGroup()
    {
        ResetTokenCache();
        string? createPath = null;
        string? createBody = null;
        var created = false;
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (req.Method == HttpMethod.Get && path.EndsWith("/groups"))
                    return FakeHandler.Raw(created ? TreeWithTenantsOnly : "[]");
                if (req.Method == HttpMethod.Post && path.EndsWith("/groups"))
                {
                    createPath = path;
                    createBody = req.Content!.ReadAsStringAsync().Result;
                    created = true;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        var svc = NewService(handler);

        var id = await svc.EnsureGroupAsync("tenants");

        id.Should().Be("g-tenants");
        createPath.Should().NotBeNull().And.EndWith("/admin/realms/octopus/groups");
        createBody.Should().Contain("\"name\":\"tenants\"");
    }

    [Fact]
    public async Task EnsureGroupAsync_AcceptsPathWithOrWithoutLeadingSlash()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = StandardResponder(TreeWithTenantsAndChildren) };
        var svc = NewService(handler);

        (await svc.EnsureGroupAsync("/tenants/t_b")).Should().Be("g-tb");
        (await svc.EnsureGroupAsync("tenants/t_b")).Should().Be("g-tb");
    }

    // ── 成员关系 ────────────────────────────────────────

    [Fact]
    public async Task AddUserToGroupAsync_PutsMembership()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = StandardResponder(TreeWithTenantsAndChildren) };
        var svc = NewService(handler);

        var ok = await svc.AddUserToGroupAsync("zhangsan", "tenants/t_a");

        ok.Should().BeTrue();
        handler.HasRequest(HttpMethod.Put, p => p.EndsWith("/users/u1/groups/g-ta"))
            .Should().BeTrue("应以 kc user id + group id 建立成员关系");
    }

    [Fact]
    public async Task RemoveUserFromGroupAsync_DeletesMembership()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = StandardResponder(TreeWithTenantsAndChildren) };
        var svc = NewService(handler);

        var ok = await svc.RemoveUserFromGroupAsync("zhangsan", "tenants/t_b");

        ok.Should().BeTrue();
        handler.HasRequest(HttpMethod.Delete, p => p.EndsWith("/users/u1/groups/g-tb"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task AddUserToGroupAsync_UnknownGroup_ReturnsFalse()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = StandardResponder(TreeWithTenantsOnly) };
        var svc = NewService(handler);

        var ok = await svc.AddUserToGroupAsync("zhangsan", "tenants/not_exist");

        ok.Should().BeFalse("组不存在时不应静默创建（创建是 EnsureGroupAsync 的职责）");
        handler.HasRequest(HttpMethod.Put, p => p.Contains("/groups/")).Should().BeFalse();
    }

    [Fact]
    public async Task GetUserGroupPathsAsync_ReturnsAllPaths()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = StandardResponder(TreeWithTenantsAndChildren,
                userGroups: """[{"id":"g-ta","path":"/tenants/t_a"},{"id":"g-oa","path":"/oa/team"}]""")
        };
        var svc = NewService(handler);

        var paths = await svc.GetUserGroupPathsAsync("zhangsan");

        paths.Should().BeEquivalentTo(new[] { "/tenants/t_a", "/oa/team" });
    }

    // ── reconcile 同步（安全边界是核心） ─────────────────

    [Fact]
    public async Task SyncUserGroupsAsync_AddsMissingAndRemovesStale()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = StandardResponder(TreeWithTenantsAndChildren,
                userGroups: """[{"id":"g-ta","path":"/tenants/t_a"},{"id":"g-tb","path":"/tenants/t_b"}]""")
        };
        var svc = NewService(handler);

        var result = await svc.SyncUserGroupsAsync(
            "zhangsan", "/tenants", new[] { "/tenants/t_b", "/tenants/t_a" });

        result.Success.Should().BeTrue();
        result.Added.Should().BeEmpty("目标组与现有组一致");
        result.Removed.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncUserGroupsAsync_RemovesGroupNoLongerDesired()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = StandardResponder(TreeWithTenantsAndChildren,
                userGroups: """[{"id":"g-ta","path":"/tenants/t_a"}]""")
        };
        var svc = NewService(handler);

        var result = await svc.SyncUserGroupsAsync("zhangsan", "/tenants", Array.Empty<string>());

        result.Removed.Should().BeEquivalentTo(new[] { "/tenants/t_a" });
        handler.HasRequest(HttpMethod.Delete, p => p.EndsWith("/users/u1/groups/g-ta"))
            .Should().BeTrue("离职/调店后应移出原租户组");
    }

    [Fact]
    public async Task SyncUserGroupsAsync_DoesNotTouchGroupsOutsideManagedPrefix()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = StandardResponder(TreeWithTenantsAndChildren,
                userGroups: """[{"id":"g-ta","path":"/tenants/t_a"},{"id":"g-oa","path":"/oa/team"}]""")
        };
        var svc = NewService(handler);

        // 受管前缀 /tenants，目标为空 → 只能动 /tenants 下的组
        var result = await svc.SyncUserGroupsAsync("zhangsan", "/tenants", Array.Empty<string>());

        result.Removed.Should().BeEquivalentTo(new[] { "/tenants/t_a" });
        handler.HasRequest(HttpMethod.Delete, p => p.EndsWith("/groups/g-oa"))
            .Should().BeFalse("前缀外的组（/oa/team）绝不能被本次同步移除");
    }

    [Fact]
    public async Task SyncUserGroupsAsync_PrefixIsBoundaryAware()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = StandardResponder(TreeWithTenantsAndChildren,
                // /tenantsX 与受管前缀 /tenants 只差后缀，不应被误判为受管
                userGroups: """[{"id":"g-x","path":"/tenantsX/t_a"}]""")
        };
        var svc = NewService(handler);

        var result = await svc.SyncUserGroupsAsync("zhangsan", "/tenants", Array.Empty<string>());

        result.Removed.Should().BeEmpty();
        handler.HasRequest(HttpMethod.Delete, p => p.EndsWith("/groups/g-x"))
            .Should().BeFalse("/tenants 不应匹配 /tenantsX（前缀必须按路径边界匹配）");
    }

    // ── 开关 ────────────────────────────────────────────

    [Fact]
    public async Task GroupOps_WhenDisabled_SkipsAllHttpCalls()
    {
        ResetTokenCache();
        var handler = new FakeHandler();
        var svc = NewService(handler, enabled: false);

        await svc.EnsureGroupAsync("tenants/t_a");
        await svc.AddUserToGroupAsync("zhangsan", "tenants/t_a");
        await svc.SyncUserGroupsAsync("zhangsan", "/tenants", new[] { "/tenants/t_a" });

        handler.Requests.Should().BeEmpty("SyncEnabled=false 时不应发起任何请求");
    }

    [Fact]
    public async Task GroupOps_OnHttpFailure_DoesNotThrow()
    {
        ResetTokenCache();
        var handler = new FakeHandler { Responder = _ => throw new HttpRequestException("network down") };
        var svc = NewService(handler);

        var ensure = async () => await svc.EnsureGroupAsync("tenants/t_a");
        var sync = async () => await svc.SyncUserGroupsAsync("zhangsan", "/tenants", new[] { "/tenants/t_a" });

        await ensure.Should().NotThrowAsync();
        await sync.Should().NotThrowAsync();
        (await svc.EnsureGroupAsync("tenants/t_a")).Should().BeNull();
    }

    // ── Keycloak 26 真实响应形态回归 ─────────────────────
    //
    // 背景：上面的 TreeWithTenantsAndChildren 把 subGroups 内联了，但 Keycloak 26 实测
    // 返回的是 {"path":"/tenants","subGroupCount":1,"subGroups":[]} —— 子组不内联。
    // 第一版实现递归的是空 subGroups，导致多级组路径永远解析不出来。
    // 下面这组用例锁定真实形态。

    /// <summary>Keycloak 26 形态：只给 subGroupCount，子组要单独拉 /children</summary>
    private const string Tree26Root = """
    [{ "id":"g-tenants","name":"tenants","path":"/tenants","subGroupCount":1,"subGroups":[] }]
    """;

    private const string Tree26Children = """
    [{ "id":"g-ta","name":"t_a","path":"/tenants/t_a","subGroupCount":0,"subGroups":[] }]
    """;

    [Fact]
    public async Task EnsureGroupAsync_Keycloak26Shape_ExpandsViaChildrenEndpoint()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (req.Method == HttpMethod.Get && path.EndsWith("/groups")) return FakeHandler.Raw(Tree26Root);
                if (req.Method == HttpMethod.Get && path.EndsWith("/children")) return FakeHandler.Raw(Tree26Children);
                if (req.Method == HttpMethod.Post) return new HttpResponseMessage(HttpStatusCode.Created);
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        var svc = NewService(handler);

        var id = await svc.EnsureGroupAsync("tenants/t_a");

        id.Should().Be("g-ta", "子组必须通过 /groups/{id}/children 展开后才能解析");
        handler.HasRequest(HttpMethod.Post, p => !p.Contains("/protocol/openid-connect/token"))
            .Should().BeFalse("组已存在，不应重复创建（真实环境会返回 409 并导致解析失败）");
    }

    [Fact]
    public async Task EnsureGroupAsync_Keycloak26Shape_CreatesThenResolvesChild()
    {
        ResetTokenCache();
        var created = false;
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (req.Method == HttpMethod.Get && path.EndsWith("/groups")) return FakeHandler.Raw(Tree26Root);
                if (req.Method == HttpMethod.Get && path.EndsWith("/children"))
                    return FakeHandler.Raw(created ? Tree26Children : "[]");
                if (req.Method == HttpMethod.Post && path.EndsWith("/children"))
                {
                    created = true;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        var svc = NewService(handler);

        (await svc.EnsureGroupAsync("tenants/t_a")).Should().Be("g-ta");
        handler.HasRequest(HttpMethod.Post, p => p.EndsWith("/groups/g-tenants/children")).Should().BeTrue();
    }

    [Fact]
    public async Task AddUserToGroupAsync_Keycloak26Shape_ResolvesNestedGroupId()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (req.Method == HttpMethod.Get && path.EndsWith("/users"))
                    return FakeHandler.Raw("""[{"id":"u1","username":"zhangsan"}]""");
                if (req.Method == HttpMethod.Get && path.EndsWith("/groups")) return FakeHandler.Raw(Tree26Root);
                if (req.Method == HttpMethod.Get && path.EndsWith("/children")) return FakeHandler.Raw(Tree26Children);
                if (req.Method == HttpMethod.Put) return new HttpResponseMessage(HttpStatusCode.NoContent);
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        var svc = NewService(handler);

        (await svc.AddUserToGroupAsync("zhangsan", "tenants/t_a")).Should().BeTrue();
        handler.HasRequest(HttpMethod.Put, p => p.EndsWith("/users/u1/groups/g-ta")).Should().BeTrue();
    }
}
