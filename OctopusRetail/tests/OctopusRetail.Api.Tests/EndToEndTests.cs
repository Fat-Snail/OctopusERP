using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusRetail.Api.Persistence;
using OctopusRetail.Api.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// M5 — 端到端闭环 + 越权回归。
///
/// 走完整链路：店长发起邀请 → 新人受控建号 → 成员关系落库 → group 对账推送 Keycloak
/// → 新人以租户身份访问本店数据；并在每一步验证越权被拒。
/// </summary>
public class EndToEndTests(RetailE2EFactory factory) : IClassFixture<RetailE2EFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static HttpRequestMessage Req(
        HttpMethod method, string url, object? body = null,
        string? tenant = null, string? sub = null, string? tenants = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        if (tenant is not null) req.Headers.Add("X-Tenant-Id", tenant);
        if (sub is not null) req.Headers.Add("X-Test-Sub", sub);
        if (tenants is not null) req.Headers.Add("X-Test-Tenants", tenants);
        return req;
    }

    private static async Task<JsonElement> RootAsync(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<T> WithDbAsync<T>(Func<RetailDbContext, Task<T>> f)
    {
        using var scope = factory.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<RetailDbContext>());
    }

    private async Task<T> WithScopedAsync<T>(Func<IServiceProvider, Task<T>> f)
    {
        using var scope = factory.Services.CreateScope();
        return await f(scope.ServiceProvider);
    }

    // ── 完整闭环 ──────────────────────────────────────────

    [Fact]
    public async Task FullJourney_Invite_Register_Reconcile_NewMemberCanAccessTenant()
    {
        // ── 1. 店长发起邀请 ────────────────────────────────
        var createResp = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites",
            new { Phone = "13700001234", RoleInTenant = "cashier", MaxUses = 1, ValidHours = 72 },
            tenant: "t_shanghai_001", sub: "kc-zhangsan", tenants: "t_shanghai_001"));
        var createRoot = await RootAsync(createResp);
        createRoot.GetProperty("code").GetInt32().Should().Be(200, "店长应能发起邀请");
        var token = createRoot.GetProperty("data").GetProperty("token").GetString()!;

        // ── 2. 新人匿名预览 ────────────────────────────────
        var preview = await RootAsync(await _client.SendAsync(Req(HttpMethod.Get,
            $"/api/retail/invites/preview?token={Uri.EscapeDataString(token)}",
            tenant: null, sub: "kc-e2e-newuser", tenants: "")));
        preview.GetProperty("data").GetProperty("tenantName").GetString().Should().Be("上海南京路店");

        // ── 3. 新人受控建号（匿名，凭邀请令牌） ──────────────
        var regResp = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites/register",
            new { Token = token, UserName = "newstaff", DisplayName = "新店员",
                  Phone = "13700001234", Password = "Passw0rd!" },
            tenant: null, sub: null, tenants: null));
        var regRoot = await RootAsync(regResp);
        regRoot.GetProperty("code").GetInt32().Should().Be(200, regRoot.GetProperty("msg").GetString());
        regRoot.GetProperty("data").GetProperty("tenantId").GetString().Should().Be("t_shanghai_001");

        // ADR-010：建号改由 UMC 完成（零售不再自己调 Keycloak 建号），
        // 这里断言 UMC 内部接口被调用过——它才是用户主数据源。
        factory.Keycloak.Has(HttpMethod.Post, p => p.EndsWith("/users"))
            .Should().BeFalse("零售不应再直连 Keycloak 建号，账号必须经 UMC 创建");

        // 成员关系已建立，但 Keycloak group 尚未同步
        var pending = await WithDbAsync(db => db.Memberships.IgnoreQueryFilters()
            .SingleAsync(m => m.TenantId == "t_shanghai_001" && m.KcSub == factory.CreatedUserId));
        // 入店即同步，用户随后登录就能拿到该租户的 group，不会再出现"加入了却 403"
        pending.KcSyncState.Should().Be(MembershipSyncState.Synced);
        pending.RoleInTenant.Should().Be("cashier");

        // ── 4. group 对账推送到 Keycloak ───────────────────
        // 直接走容器的真实装配（Keycloak 已启用 + 假 Admin 端点已注入）
        var syncResult = await WithScopedAsync(sp =>
            sp.GetRequiredService<GroupSyncReconciler>().ReconcileAsync());

        syncResult.Skipped.Should().BeFalse();
        factory.Keycloak.Has(HttpMethod.Put,
            p => p.EndsWith($"/users/{factory.CreatedUserId}/groups/g-sh"))
            .Should().BeTrue("对账应把新成员加入上海店 group");

        // ── 5. 新人现在以租户身份访问本店数据 ────────────────
        var membersResp = await _client.SendAsync(Req(HttpMethod.Get, "/api/retail/members",
            tenant: "t_shanghai_001", sub: factory.CreatedUserId, tenants: "t_shanghai_001"));
        var members = (await RootAsync(membersResp)).GetProperty("data");
        members.GetProperty("tenantId").GetString().Should().Be("t_shanghai_001");

        var subs = members.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("kcSub").GetString()).ToList();
        subs.Should().Contain(factory.CreatedUserId);

        // ── 6. 越权回归：新人访问别家门店必须被拒 ────────────
        var forbidden = await _client.SendAsync(Req(HttpMethod.Get, "/api/retail/members",
            tenant: "t_beijing_002", sub: factory.CreatedUserId, tenants: "t_shanghai_001"));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "token 里没有北京店 group，声明该租户必须 403");
    }

    // ── 越权回归矩阵 ──────────────────────────────────────

    [Fact]
    public async Task Regression_MissingTenantHeader_Is400_NotSilentAllTenants()
    {
        var resp = await _client.SendAsync(Req(HttpMethod.Get, "/api/retail/members",
            sub: "kc-zhangsan", tenants: "t_shanghai_001"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Regression_FabricatedTenantHeader_Is403()
    {
        var resp = await _client.SendAsync(Req(HttpMethod.Get, "/api/retail/members",
            tenant: "t_attacker_made_up", sub: "kc-zhangsan", tenants: "t_shanghai_001"));

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Regression_TenantHeaderForgedInBodyOrQuery_IsStillRejected()
    {
        // 把租户塞进 query 而不是 header，不应被采信
        var resp = await _client.SendAsync(Req(HttpMethod.Get,
            "/api/retail/members?tenantId=t_beijing_002",
            sub: "kc-zhangsan", tenants: "t_shanghai_001"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "query 里的租户不是合法声明，缺失 X-Tenant-Id 仍应 400");
    }

    [Fact]
    public async Task Regression_NonManagerCannotCreateInvite()
    {
        var resp = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites",
            new { Phone = "13700009999", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 },
            tenant: "t_shanghai_001", sub: "kc-lisi", tenants: "t_shanghai_001"));

        var root = await RootAsync(resp);
        root.GetProperty("code").GetInt32().Should().Be(403);
        root.GetProperty("msg").GetString().Should().Contain("无权");
    }

    [Fact]
    public async Task Regression_BusinessQuery_IsScopedByGlobalFilterOnly()
    {
        // /profiles 的查询里没有 TenantId 条件，隔离完全来自全局过滤器
        var sh = await RootAsync(await _client.SendAsync(Req(HttpMethod.Get, "/api/retail/profiles",
            tenant: "t_shanghai_001", sub: "kc-zhangsan", tenants: "t_shanghai_001,t_beijing_002")));
        var bj = await RootAsync(await _client.SendAsync(Req(HttpMethod.Get, "/api/retail/profiles",
            tenant: "t_beijing_002", sub: "kc-zhangsan", tenants: "t_shanghai_001,t_beijing_002")));

        var shNames = sh.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("displayName").GetString()).ToList();
        var bjNames = bj.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("displayName").GetString()).ToList();

        shNames.Should().BeEquivalentTo(new[] { "张三", "李四" });
        bjNames.Should().BeEquivalentTo(new[] { "王五" });
        shNames.Should().NotIntersectWith(bjNames);
    }

    [Fact]
    public async Task Regression_InviteFromOtherTenant_CannotBeUsedToJoinOwnTenant()
    {
        // 北京店店长发出的邀请
        var createRoot = await RootAsync(await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites",
            new { Phone = "13700007777", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 },
            tenant: "t_beijing_002", sub: "kc-wangwu", tenants: "t_beijing_002")));
        var token = createRoot.GetProperty("data").GetProperty("token").GetString()!;

        // 已有上海店身份的人接受北京店邀请 —— 应进入北京店，而不是把上海店"升级"
        var accept = await RootAsync(await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites/accept",
            new { Token = token }, tenant: null, sub: "kc-zhangsan",
            tenants: "t_shanghai_001,t_chain_hq")));

        accept.GetProperty("data").GetProperty("tenantId").GetString().Should().Be("t_beijing_002");

        // 上海店的成员关系不受影响（不存在"跨租户提权"）
        var shMembership = await WithDbAsync(db => db.Memberships.IgnoreQueryFilters()
            .SingleAsync(m => m.KcSub == "kc-zhangsan" && m.TenantId == "t_shanghai_001"));
        shMembership.RoleInTenant.Should().Be("manager");
        shMembership.Status.Should().Be("active");
    }
}
