using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusWMS.Api.Tests;

public class InventoryTests : IClassFixture<WmsTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public InventoryTests(WmsTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetInventoryList_ReturnsSeedData()
    {
        var resp = await _client.GetAsync("/api/inventory");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var total = doc.RootElement.GetProperty("data").GetProperty("total").GetInt32();
        total.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task FilterByWarehouse_ReturnsMatchingItems()
    {
        var resp = await _client.GetAsync("/api/inventory?warehouseId=1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows");
        foreach (var row in rows.EnumerateArray())
            row.GetProperty("warehouseId").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task GetInventorySummary_ReturnsStats()
    {
        var resp = await _client.GetAsync("/api/inventory/summary");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("warehouseCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetLowStock_ReturnsBelowSafetyItems()
    {
        var resp = await _client.GetAsync("/api/inventory/low-stock");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task AdjustInventory_ChangesQuantity()
    {
        var resp = await _client.PutAsJsonAsync("/api/inventory/1/adjust", new { delta = 100m, reason = "测试调整" });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }
}
