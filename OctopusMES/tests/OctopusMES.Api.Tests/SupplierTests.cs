using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

public class SupplierTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public SupplierTests(MesTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetSupplierList_ReturnsSeedData()
    {
        var resp = await _client.GetAsync("/api/supplier");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task GetSupplierById_ReturnsSupplier()
    {
        var resp = await _client.GetAsync("/api/supplier/1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("supplierCode").GetString().Should().Be("SUP-001");
    }

    [Fact]
    public async Task CreateSupplier_ReturnsNewSupplierWithCode()
    {
        var resp = await _client.PostAsJsonAsync("/api/supplier", new
        {
            supplierName = "新测试供应商",
            contactName = "测试联系人",
            phone = "13900139000",
            status = "active",
            level = 3
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("supplierCode").GetString().Should().StartWith("SUP-");
        doc.RootElement.GetProperty("data").GetProperty("supplierName").GetString().Should().Be("新测试供应商");
    }

    [Fact]
    public async Task UpdateSupplier_ReturnsUpdated()
    {
        var resp = await _client.PutAsJsonAsync("/api/supplier", new
        {
            supplierId = 1,
            supplierName = "富士康（更新）",
            contactName = "郭明",
            phone = "13800138001",
            status = "active",
            level = 1
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("supplierName").GetString().Should().Contain("更新");
    }

    [Fact]
    public async Task FilterSupplierByKeyword_ReturnsMatching()
    {
        var resp = await _client.GetAsync("/api/supplier?keyword=富士康");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var row in rows.EnumerateArray())
            row.GetProperty("supplierName").GetString()!.Should().Contain("富士康");
    }
}
