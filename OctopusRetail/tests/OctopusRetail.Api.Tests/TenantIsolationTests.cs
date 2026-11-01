using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// M1 — 多租户隔离与越权回归（design §7 三道防线）。
///
/// 默认测试身份 kc-zhangsan：可进入 t_shanghai_001 + t_chain_hq（不含 t_beijing_002）。
/// </summary>
public class TenantIsolationTests(RetailTestFactory factory) : IClassFixture<RetailTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static HttpRequestMessage Get(string url, string? tenant = null, string? sub = null, string? tenants = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (tenant is not null) req.Headers.Add("X-Tenant-Id", tenant);
        if (sub is not null) req.Headers.Add("X-Test-Sub", sub);
        if (tenants is not null) req.Headers.Add("X-Test-Tenants", tenants);
        return req;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage resp) =>
        JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();

    // ── 第一道防线：缺租户 → 400 ──────────────────────────

    [Fact]
    public async Task Members_WithoutTenantHeader_Returns400()
    {
        var resp = await _client.SendAsync(Get("/api/retail/members"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await BodyAsync(resp);
        body.GetProperty("code").GetInt32().Should().Be(400);
        body.GetProperty("msg").GetString().Should().Contain("X-Tenant-Id");
    }

    [Fact]
    public async Task Profiles_WithoutTenantHeader_Returns400()
    {
        var resp = await _client.SendAsync(Get("/api/retail/profiles"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── 第二道防线：伪造租户 → 403 ────────────────────────

    [Fact]
    public async Task Members_WithTenantNotInToken_Returns403()
    {
        // token 只授予 t_shanghai_001 / t_chain_hq，却声称要访问北京店
        var resp = await _client.SendAsync(Get("/api/retail/members", tenant: "t_beijing_002"));

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await BodyAsync(resp);
        body.GetProperty("code").GetInt32().Should().Be(403);
    }

    [Fact]
    public async Task Members_WithFabricatedTenant_Returns403()
    {
        var resp = await _client.SendAsync(Get("/api/retail/members", tenant: "t_does_not_exist"));

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── 正常路径 ──────────────────────────────────────────

    [Fact]
    public async Task Members_WithValidTenant_ReturnsOnlyThatTenant()
    {
        var resp = await _client.SendAsync(Get("/api/retail/members", tenant: "t_shanghai_001"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await BodyAsync(resp);
        var data = body.GetProperty("data");
        data.GetProperty("tenantId").GetString().Should().Be("t_shanghai_001");

        // 上海店有 zhangsan(manager) + lisi(staff)
        var subs = data.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("kcSub").GetString()).ToList();
        subs.Should().BeEquivalentTo(new[] { "kc-zhangsan", "kc-lisi" });
        subs.Should().NotContain("kc-wangwu", "王五属于北京店，绝不能出现在上海店的结果里");
    }

    // ── 第三道防线：全局过滤器 fail-closed ─────────────────

    [Fact]
    public async Task Profiles_ScopedToActiveTenant_DespiteNoWhereClauseInQuery()
    {
        var resp = await _client.SendAsync(Get("/api/retail/profiles", tenant: "t_shanghai_001"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await BodyAsync(resp)).GetProperty("data");
        var items = data.GetProperty("items").EnumerateArray().ToList();

        // Controller 里没有任何 TenantId 条件，隔离完全来自全局过滤器
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(x => x.GetProperty("tenantId").GetString() == "t_shanghai_001");
        items.Select(x => x.GetProperty("displayName").GetString())
            .Should().BeEquivalentTo(new[] { "张三", "李四" });
    }

    [Fact]
    public async Task Profiles_SwitchingTenantHeader_ReturnsDifferentDataSet()
    {
        // 同一个人、同一段代码，仅切换 X-Tenant-Id —— 这就是 workspace 切换的实质
        var sh = await BodyAsync(await _client.SendAsync(
            Get("/api/retail/profiles", tenant: "t_shanghai_001")));
        var bj = await BodyAsync(await _client.SendAsync(
            Get("/api/retail/profiles", tenant: "t_chain_hq")));

        var shNames = sh.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("displayName").GetString()).ToList();
        var bjNames = bj.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("displayName").GetString()).ToList();

        shNames.Should().BeEquivalentTo(new[] { "张三", "李四" });
        bjNames.Should().BeEmpty("总部租户下没有成员档案，绝不能回落到其它租户的数据");
    }

    // ── 跨租户端点 ────────────────────────────────────────

    [Fact]
    public async Task Me_CrossTenantEndpoint_WorksWithoutTenantHeader()
    {
        var resp = await _client.SendAsync(Get("/api/retail/me"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await BodyAsync(resp)).GetProperty("data");
        data.GetProperty("sub").GetString().Should().Be("kc-zhangsan");
        data.GetProperty("availableTenants").EnumerateArray()
            .Select(x => x.GetString())
            .Should().BeEquivalentTo(new[] { "t_chain_hq", "t_shanghai_001" });
        data.GetProperty("activeTenant").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Me_AfterTenantResolution_ReflectsActiveTenant()
    {
        // 跨租户端点本身不解析租户；解析结果体现在租户内端点上
        var resp = await _client.SendAsync(Get("/api/retail/members", tenant: "t_chain_hq"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BodyAsync(resp)).GetProperty("data").GetProperty("tenantId").GetString()
            .Should().Be("t_chain_hq");
    }

    [Fact]
    public async Task TenantExtraction_IgnoresGroupsOutsideTenantsPrefix()
    {
        // 同时带上 ERP 组织组与业务组：只有 /tenants/* 应被识别为租户
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/retail/me");
        req.Headers.Add("X-Test-Tenants", "t_shanghai_001");
        req.Headers.Add("X-Test-Groups", "/depts/tech,/oa/team,/tenantsX/evil");
        var resp = await _client.SendAsync(req);

        var data = (await BodyAsync(resp)).GetProperty("data");
        data.GetProperty("availableTenants").EnumerateArray()
            .Select(x => x.GetString())
            .Should().BeEquivalentTo(new[] { "t_shanghai_001" },
                "/depts 与 /oa 组不是租户；/tenantsX 也不得被前缀混同");
    }
}
