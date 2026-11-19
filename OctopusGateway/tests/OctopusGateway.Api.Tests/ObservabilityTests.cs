using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OctopusGateway.Api.Tests;

/// <summary>v1.5.5 可观测性验收：/health/full 聚合三种状态 + /octopus/diagnostics 诊断快照。</summary>
public class ObservabilityTests : IClassFixture<GatewayTestFactory>
{
    private readonly GatewayTestFactory _factory;
    private static readonly JsonSerializerOptions _json = GatewayTestJson.Options;

    public ObservabilityTests(GatewayTestFactory factory) => _factory = factory;

    [Fact]
    public async Task HealthFull_AllDownstreamsUp_Returns200Healthy()
    {
        // 所有集群指向健康的 stub，且探测路径命中 stub 的 /health
        var client = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:crm:Destinations:d1:Address"] = _factory.StubUrl + "/",
                ["ReverseProxy:Clusters:wms:Destinations:d1:Address"] = _factory.StubUrl + "/",
                ["HealthCheck:TimeoutMs"] = "1500",
            }))).CreateClient();

        var resp = await client.GetAsync("/health/full");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("msg").GetString().Should().Be("healthy");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("overall").GetString().Should().Be("healthy");
        data.GetProperty("unhealthy").GetInt32().Should().Be(0);
        data.GetProperty("services").EnumerateArray()
            .All(s => s.GetProperty("status").GetString() == "healthy").Should().BeTrue();
    }

    [Fact]
    public async Task HealthFull_PartialDown_Returns503Degraded()
    {
        // wms 指向必然连接失败的地址，crm 仍健康 → degraded
        var client = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:crm:Destinations:d1:Address"] = _factory.StubUrl + "/",
                ["ReverseProxy:Clusters:wms:Destinations:d1:Address"] = "http://127.0.0.1:1/",
                ["HealthCheck:TimeoutMs"] = "200",
            }))).CreateClient();

        var resp = await client.GetAsync("/health/full");
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("msg").GetString().Should().Be("degraded");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("healthy").GetInt32().Should().BeGreaterThan(0);
        data.GetProperty("unhealthy").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HealthFull_AllDown_Returns503Unhealthy()
    {
        var client = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:crm:Destinations:d1:Address"] = "http://127.0.0.1:1/",
                ["ReverseProxy:Clusters:wms:Destinations:d1:Address"] = "http://127.0.0.1:2/",
                ["ReverseProxy:Clusters:mes:Destinations:d1:Address"] = "http://127.0.0.1:3/",
                ["ReverseProxy:Clusters:fin:Destinations:d1:Address"] = "http://127.0.0.1:4/",
                ["ReverseProxy:Clusters:self:Destinations:d1:Address"] = "http://127.0.0.1:5/",
                ["HealthCheck:TimeoutMs"] = "200",
            }))).CreateClient();

        var resp = await client.GetAsync("/health/full");
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("msg").GetString().Should().Be("unhealthy");
        doc.RootElement.GetProperty("data").GetProperty("healthy").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Diagnostics_Anonymous_ReturnsRuntimeSnapshotWithoutSecrets()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/octopus/diagnostics");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("service").GetString().Should().Be("octopus-gateway");
        data.GetProperty("routes").EnumerateArray().Should().NotBeEmpty();
        data.GetProperty("clusters").EnumerateArray().Should().NotBeEmpty();
        data.GetProperty("rateLimit").GetProperty("permitLimit").GetInt32().Should().Be(3);

        // 不得泄露任何密钥/凭据字段
        body.Should().NotContain("SharedSecret");
        body.Should().NotContain("authority");
    }
}
