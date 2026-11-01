using FluentAssertions;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusPLM.Api.Tests;

/// <summary>P10：PLM 跨系统标准化 API 测试</summary>
public class CrossSystemTests : IClassFixture<PlmTestFactory>
{
    private readonly HttpClient _client;

    public CrossSystemTests(PlmTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ByIds_ReturnsBriefRowsForExistingProducts()
    {
        // 种子至少有 ProductId=1 / ProductId=2
        var resp = await _client.PostAsJsonAsync("/api/product/by-ids",
            new { productIds = new[] { 1L, 2L, 99999L } });
        resp.IsSuccessStatusCode.Should().BeTrue();

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        rows.Should().NotBeEmpty();
        rows.All(r => r.GetProperty("productName").GetString() != null).Should().BeTrue();
    }

    [Fact]
    public async Task ByIds_EmptyList_ReturnsEmpty()
    {
        var resp = await _client.PostAsJsonAsync("/api/product/by-ids",
            new { productIds = Array.Empty<long>() });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ByCodes_ReturnsEmptyWhenNoSourceIdMatches()
    {
        var resp = await _client.PostAsJsonAsync("/api/product/by-codes",
            new { codes = new[] { "non-existent-code-xyz" } });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task CrossSystem_ReturnsBaseInfoEvenWhenWmsCrmUnavailable()
    {
        // 测试环境无 Webhooks:WmsBaseUrl/CrmBaseUrl 配置 → CrossSystemService 静默返回空，主信息仍可用
        var resp = await _client.GetAsync("/api/product/1/cross-system");
        resp.IsSuccessStatusCode.Should().BeTrue();

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("productId").GetInt64().Should().Be(1);
        data.TryGetProperty("productName", out _).Should().BeTrue();
        // stock / sales 字段存在但可能为 null（下游不可达）
        data.TryGetProperty("stock", out _).Should().BeTrue();
        data.TryGetProperty("sales", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CrossSystem_NonExistentProduct_Returns404Code()
    {
        var resp = await _client.GetAsync("/api/product/9999999/cross-system");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(404);
    }
}
