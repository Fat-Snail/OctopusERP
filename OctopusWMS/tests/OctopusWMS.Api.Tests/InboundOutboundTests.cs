using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusWMS.Api.Tests;

public class InboundOutboundTests : IClassFixture<WmsTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public InboundOutboundTests(WmsTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetInboundList_ReturnsSeedData()
    {
        var resp = await _client.GetAsync("/api/inbound");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetInboundById_ReturnsOrderWithItems()
    {
        var resp = await _client.GetAsync("/api/inbound/1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("inboundCode").GetString().Should().StartWith("IN-");
        doc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateInbound_ReturnsNewOrder()
    {
        var resp = await _client.PostAsJsonAsync("/api/inbound", new
        {
            warehouseId = 1,
            inboundType = "purchase",
            supplier = "测试供应商",
            items = new[] { new { plmProductId = (long?)1, productName = "测试商品", unit = "台", expectedQty = 100m } }
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("inboundCode").GetString().Should().StartWith("IN-");
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("pending");
    }

    [Fact]
    public async Task FilterInboundByStatus_ReturnsMatching()
    {
        var resp = await _client.GetAsync("/api/inbound?status=completed");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows");
        foreach (var row in rows.EnumerateArray())
            row.GetProperty("status").GetString().Should().Be("completed");
    }

    [Fact]
    public async Task GetOutboundList_ReturnsSeedData()
    {
        var resp = await _client.GetAsync("/api/outbound");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CreateOutbound_ReturnsNewOrder()
    {
        var resp = await _client.PostAsJsonAsync("/api/outbound", new
        {
            warehouseId = 1,
            outboundType = "sales",
            recipient = "测试客户",
            shipAddress = "测试地址",
            items = new[] { new { plmProductId = (long?)2, productName = "小米 14", unit = "台", requestedQty = 50m } }
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("outboundCode").GetString().Should().StartWith("OUT-");
    }

    [Fact]
    public async Task ShipOutbound_ChangesStatusToShipped()
    {
        // Create a new outbound first to avoid state pollution
        var create = await _client.PostAsJsonAsync("/api/outbound", new
        {
            warehouseId = 1,
            outboundType = "sales",
            recipient = "测试客户2",
            items = new[] { new { productName = "测试商品", requestedQty = 10m } }
        });
        var createDoc = await create.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var newId = createDoc!.RootElement.GetProperty("data").GetProperty("outboundId").GetInt64();

        var resp = await _client.PutAsJsonAsync($"/api/outbound/{newId}/ship", new { });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("shipped");
    }
}
