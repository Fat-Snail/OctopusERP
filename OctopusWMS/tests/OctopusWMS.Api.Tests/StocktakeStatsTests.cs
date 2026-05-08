using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusWMS.Api.Tests;

public class StocktakeStatsTests : IClassFixture<WmsTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public StocktakeStatsTests(WmsTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetStocktakeList_ReturnsSeedData()
    {
        var resp = await _client.GetAsync("/api/stocktake");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetStocktakeById_ReturnsWithItems()
    {
        var resp = await _client.GetAsync("/api/stocktake/1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateStocktake_GeneratesItemsFromInventory()
    {
        var resp = await _client.PostAsJsonAsync("/api/stocktake", new { warehouseId = 2, remark = "测试盘点" });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("stocktakeCode").GetString().Should().StartWith("ST-");
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("in_progress");
    }

    [Fact]
    public async Task FilterStocktakeByWarehouse_ReturnsMatching()
    {
        var resp = await _client.GetAsync("/api/stocktake?warehouseId=1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows");
        foreach (var row in rows.EnumerateArray())
            row.GetProperty("warehouseId").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task GetStatsSummary_ReturnsExpectedFields()
    {
        var resp = await _client.GetAsync("/api/stats/summary");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("warehouseCount", out _).Should().BeTrue();
        data.TryGetProperty("lowStockCount", out _).Should().BeTrue();
        data.TryGetProperty("totalInventoryItems", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetMeEndpoint_ReturnsUserInfo()
    {
        var resp = await _client.GetAsync("/api/me");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("userName").GetString().Should().Be("admin");
    }
}
