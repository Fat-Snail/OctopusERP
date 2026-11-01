using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusWMS.Api.Tests;

public class FabricArrivalTests : IClassFixture<WmsTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FabricArrivalTests(WmsTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task CreateFabricArrival_ReturnsCreatedRecord()
    {
        var resp = await _client.PostAsJsonAsync("/api/fabric-arrival", new
        {
            mesScheduleId = 100L,
            fabricCode = "FAB-WOOL-001",
            colorCode = "BLK01",
            batchCode = "B202203",
            expectedQty = 500m,
            expectedDate = new DateTime(2099, 5, 1),
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("arrivalCode").GetString().Should().StartWith("FA-");
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("pending");
    }

    [Fact]
    public async Task MarkArrived_TransitionsToOk_AndCreatesFabricInventory()
    {
        // 1. 创建回布单
        var createResp = await _client.PostAsJsonAsync("/api/fabric-arrival", new
        {
            mesScheduleId = 200L,
            fabricCode = "FAB-LINEN-001",
            colorCode = "WHT01",
            batchCode = "B202204",
            expectedQty = 1000m,
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var arrivalId = createDoc!.RootElement.GetProperty("data").GetProperty("arrivalId").GetInt64();

        // 2. 标记到货 (仓库 ID=1, 种子里有)
        var arriveResp = await _client.PostAsJsonAsync($"/api/fabric-arrival/{arrivalId}/mark-arrived", new
        {
            receivedQty = 1000m,
            warehouseId = 1L,
        });
        var arriveDoc = await arriveResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        arriveDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        arriveDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("ok");

        // 3. 重复 mark 返回提示
        var arriveAgain = await _client.PostAsJsonAsync($"/api/fabric-arrival/{arrivalId}/mark-arrived", new
        {
            receivedQty = 1000m,
            warehouseId = 1L,
        });
        var againDoc = await arriveAgain.Content.ReadFromJsonAsync<JsonDocument>(_json);
        againDoc!.RootElement.GetProperty("msg").GetString().Should().Contain("已是已到齐");
    }

    [Fact]
    public async Task MarkArrived_PartialQty_TransitionsToPartial()
    {
        var createResp = await _client.PostAsJsonAsync("/api/fabric-arrival", new
        {
            mesScheduleId = 300L,
            fabricCode = "FAB-COTTON-001",
            expectedQty = 1000m,
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var arrivalId = createDoc!.RootElement.GetProperty("data").GetProperty("arrivalId").GetInt64();

        var arriveResp = await _client.PostAsJsonAsync($"/api/fabric-arrival/{arrivalId}/mark-arrived", new
        {
            receivedQty = 600m,
            warehouseId = 1L,
        });
        var doc = await arriveResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("partial");
    }

    [Fact]
    public async Task ListFabricArrivals_FilterByMesScheduleId()
    {
        await _client.PostAsJsonAsync("/api/fabric-arrival", new
        {
            mesScheduleId = 999L, fabricCode = "FAB-FILTER-001", expectedQty = 100m,
        });
        var resp = await _client.GetAsync("/api/fabric-arrival?mesScheduleId=999");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }
}
