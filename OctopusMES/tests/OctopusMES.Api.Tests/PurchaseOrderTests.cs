using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

public class PurchaseOrderTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PurchaseOrderTests(MesTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetPurchaseList_ReturnsSeedData()
    {
        var resp = await _client.GetAsync("/api/purchase");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetPurchaseById_ReturnsWithItems()
    {
        var resp = await _client.GetAsync("/api/purchase/1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("purchaseCode").GetString().Should().StartWith("PO-");
        doc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreatePurchaseOrder_ReturnsNewOrder()
    {
        var resp = await _client.PostAsJsonAsync("/api/purchase", new
        {
            supplierId = 1,
            currency = "CNY",
            items = new[]
            {
                new { productName = "测试零件", quantity = 100m, unitPrice = 50m, amount = 5000m }
            }
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("purchaseCode").GetString().Should().StartWith("PO-");
        data.GetProperty("status").GetString().Should().Be("draft");
    }

    [Fact]
    public async Task SubmitPurchaseOrder_ChangesStatusToSubmitted()
    {
        // Create then submit
        var create = await _client.PostAsJsonAsync("/api/purchase", new
        {
            supplierId = 2,
            items = new[] { new { productName = "测试零件2", quantity = 50m, unitPrice = 100m, amount = 5000m } }
        });
        var createDoc = await create.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var newId = createDoc!.RootElement.GetProperty("data").GetProperty("purchaseId").GetInt64();

        var resp = await _client.PutAsJsonAsync($"/api/purchase/{newId}/submit", new { });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("submitted");
    }

    [Fact]
    public async Task ApprovePurchaseOrder_ChangesStatusToApproved()
    {
        var resp = await _client.PutAsJsonAsync("/api/purchase/2/approve", new { });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("approved");
    }

    [Fact]
    public async Task FilterBySupplier_ReturnsMatchingOrders()
    {
        var resp = await _client.GetAsync("/api/purchase?supplierId=1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows");
        foreach (var row in rows.EnumerateArray())
            row.GetProperty("supplierId").GetInt64().Should().Be(1);
    }
}
