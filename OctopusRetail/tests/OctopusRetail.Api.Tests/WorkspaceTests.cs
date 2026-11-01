using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// M1 — workspace（workspace = membership 的数据 + activeTenant 的运行时）。
/// 「列出我的 workspace」必须跨租户，因此是 [AllowCrossTenant] 端点。
/// </summary>
public class WorkspaceTests(RetailTestFactory factory) : IClassFixture<RetailTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage resp) =>
        JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();

    private static HttpRequestMessage Get(string url, string? sub = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (sub is not null) req.Headers.Add("X-Test-Sub", sub);
        return req;
    }

    [Fact]
    public async Task Workspaces_MultiTenantUser_ReturnsAllMemberships()
    {
        // kc-zhangsan 在种子里同时属于 上海南京路店(manager) 与 连锁总部(staff)
        var resp = await _client.SendAsync(Get("/api/retail/workspaces"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await BodyAsync(resp)).GetProperty("data");
        data.GetProperty("total").GetInt32().Should().Be(2,
            "一人多租户正是需要 workspace 切换的场景");

        var items = data.GetProperty("items").EnumerateArray().ToList();
        items.Select(x => x.GetProperty("tenantId").GetString())
            .Should().BeEquivalentTo(new[] { "t_shanghai_001", "t_chain_hq" });
        items.Single(x => x.GetProperty("tenantId").GetString() == "t_shanghai_001")
            .GetProperty("role").GetString().Should().Be("manager",
                "同一人在不同租户可以拥有不同角色——这是 Keycloak 无法表达、必须由业务库判定的部分");
    }

    [Fact]
    public async Task Workspaces_DoesNotRequireTenantHeader()
    {
        // 跨租户端点：不应被租户中间件拦下
        var resp = await _client.SendAsync(Get("/api/retail/workspaces"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Workspaces_OnlyReturnsOwnMemberships()
    {
        var resp = await _client.SendAsync(Get("/api/retail/workspaces", sub: "kc-lisi"));

        var data = (await BodyAsync(resp)).GetProperty("data");
        data.GetProperty("total").GetInt32().Should().Be(1);
        data.GetProperty("items").EnumerateArray().Single()
            .GetProperty("tenantId").GetString().Should().Be("t_shanghai_001");
    }

    [Fact]
    public async Task Workspaces_UnknownUser_ReturnsEmpty()
    {
        var resp = await _client.SendAsync(Get("/api/retail/workspaces", sub: "kc-nobody"));

        var data = (await BodyAsync(resp)).GetProperty("data");
        data.GetProperty("total").GetInt32().Should().Be(0);
        data.GetProperty("items").GetArrayLength().Should().Be(0);
    }
}
