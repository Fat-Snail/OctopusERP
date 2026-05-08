using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusWMS.Api.Tests;

public class WarehouseTests : IClassFixture<WmsTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public WarehouseTests(WmsTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetWarehouseList_ReturnsSeedData()
    {
        var resp = await _client.GetAsync("/api/warehouse");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var total = doc.RootElement.GetProperty("data").GetProperty("total").GetInt32();
        total.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetWarehouseById_ReturnsWarehouseWithLocations()
    {
        var resp = await _client.GetAsync("/api/warehouse/1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("warehouseCode").GetString().Should().Be("WH-001");
        data.GetProperty("locations").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateWarehouse_ReturnsNewWarehouseWithCode()
    {
        var resp = await _client.PostAsJsonAsync("/api/warehouse", new
        {
            warehouseName = "测试新仓库",
            address = "北京市朝阳区",
            manager = "测试经理",
            status = "active"
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("warehouseCode").GetString().Should().StartWith("WH-");
        data.GetProperty("warehouseName").GetString().Should().Be("测试新仓库");
    }

    [Fact]
    public async Task UpdateWarehouse_ReturnsUpdatedWarehouse()
    {
        var resp = await _client.PutAsJsonAsync("/api/warehouse", new
        {
            warehouseId = 1,
            warehouseName = "华南主仓（更新）",
            address = "广东省深圳市",
            manager = "张三",
            phone = "13800138001",
            status = "active"
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("warehouseName").GetString().Should().Contain("更新");
    }

    [Fact]
    public async Task FilterByStatus_ReturnsMatchingWarehouses()
    {
        var resp = await _client.GetAsync("/api/warehouse?status=active");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows");
        foreach (var row in rows.EnumerateArray())
            row.GetProperty("status").GetString().Should().Be("active");
    }
}
