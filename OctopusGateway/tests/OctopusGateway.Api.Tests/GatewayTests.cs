using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OctopusGateway.Api.Tests;

/// <summary>P16 网关验收：路由转发 / 强制鉴权 / 统一限流 / 自身健康</summary>
public class GatewayTests : IClassFixture<GatewayTestFactory>
{
    private readonly GatewayTestFactory _factory;
    private static readonly JsonSerializerOptions _json = GatewayTestJson.Options;

    public GatewayTests(GatewayTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_Anonymous_Returns200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("service")
            .GetString().Should().Be("octopus-gateway");
    }

    [Fact]
    public async Task Forward_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var resp = await client.GetAsync("/gw/self/api/echo");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forward_WithAuth_ReachesDownstream_StrippingPrefix()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        var resp = await client.GetAsync("/gw/self/api/echo");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("msg").GetString().Should().Be("echo from stub");
        doc.RootElement.GetProperty("data").GetProperty("downstream")
            .GetString().Should().Be("stub");
    }

    [Fact]
    public async Task RateLimit_ExceedsPermitLimit_Returns429()
    {
        // 专属用户，避免与其它测试共享限流 key（factory 在 xUnit 类内共享）
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "rl-user-a");

        // PermitLimit=3（工厂覆盖），第 4 次应 429
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var resp = await client.GetAsync("/gw/self/api/echo");
            codes.Add(resp.StatusCode);
        }

        codes.Take(3).Should().OnlyContain(c => c == HttpStatusCode.OK);
        codes[3].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RateLimit_IsPerUser_NotGlobal()
    {
        // 用户 A 打满配额后，用户 B（不同 sub）仍然可用
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        clientA.DefaultRequestHeaders.Add("X-Test-UserId", "rl-user-b");

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        clientB.DefaultRequestHeaders.Add("X-Test-UserId", "rl-user-c");

        for (var i = 0; i < 3; i++)
            (await clientA.GetAsync("/gw/self/api/echo")).StatusCode
                .Should().Be(HttpStatusCode.OK);
        (await clientA.GetAsync("/gw/self/api/echo")).StatusCode
            .Should().Be(HttpStatusCode.TooManyRequests);

        var respB = await clientB.GetAsync("/gw/self/api/echo");
        respB.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
