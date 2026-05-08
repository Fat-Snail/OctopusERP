using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

public class WorkOrderTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public WorkOrderTests(MesTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetWorkOrderList_ReturnsSeedData()
    {
        var resp = await _client.GetAsync("/api/workorder");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetWorkOrderById_ReturnsWithProcesses()
    {
        var resp = await _client.GetAsync("/api/workorder/1");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("workOrderCode").GetString().Should().StartWith("WO-");
        doc.RootElement.GetProperty("data").GetProperty("processes").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateWorkOrder_ReturnsNewOrder()
    {
        var resp = await _client.PostAsJsonAsync("/api/workorder", new
        {
            plmProductId = (long?)1,
            productName = "测试产品",
            plannedQty = 100m,
            unit = "台",
            workshopName = "测试车间"
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("workOrderCode").GetString().Should().StartWith("WO-");
        data.GetProperty("status").GetString().Should().Be("draft");
    }

    [Fact]
    public async Task StartWorkOrder_ChangesStatusToInProgress()
    {
        var resp = await _client.PutAsJsonAsync("/api/workorder/3/start", new { });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("in_progress");
    }

    [Fact]
    public async Task CompleteWorkOrder_ChangesStatusToCompleted()
    {
        // Use workorder 1 which is in_progress
        var resp = await _client.PutAsJsonAsync("/api/workorder/1/complete", new { completedQty = 850m });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("completed");
        doc.RootElement.GetProperty("data").GetProperty("completedQty").GetDecimal().Should().Be(850m);
    }

    [Fact]
    public async Task UpdateProcessStatus_ReturnsUpdatedProcess()
    {
        var resp = await _client.PutAsJsonAsync("/api/workorder/process/4/status", new { status = "in_progress" });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("in_progress");
    }

    [Fact]
    public async Task FilterWorkOrderByStatus_ReturnsMatching()
    {
        var resp = await _client.GetAsync("/api/workorder?status=completed");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows");
        foreach (var row in rows.EnumerateArray())
            row.GetProperty("status").GetString().Should().Be("completed");
    }

    [Fact]
    public async Task GetStatsSummary_ReturnsExpectedFields()
    {
        var resp = await _client.GetAsync("/api/stats/summary");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("supplierCount", out _).Should().BeTrue();
        data.TryGetProperty("workOrderInProgress", out _).Should().BeTrue();
        data.TryGetProperty("totalPurchaseAmount", out _).Should().BeTrue();
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
