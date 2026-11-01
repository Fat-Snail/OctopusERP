using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusMES.Api.Persistence;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

/// <summary>P12 测试：工单时效 + 设备 + 报工 + 瓶颈/排名统计</summary>
public class EfficiencyAndReportingTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private readonly MesTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public EfficiencyAndReportingTests(MesTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // ── P12-2：设备档案 ─────────────────────────────────────────────────

    [Fact]
    public async Task GetEquipments_ReturnsSeedEquipments()
    {
        var resp = await _client.GetAsync("/api/equipment");
        resp.IsSuccessStatusCode.Should().BeTrue();
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task CreateEquipment_AddsAndReturnsRow()
    {
        var resp = await _client.PostAsJsonAsync("/api/equipment", new
        {
            equipmentCode = $"EQ-TEST-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            equipmentName = "测试机床",
            workCenter = "测试车间",
            status = "idle",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("equipmentId").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateEquipmentStatus_SetsStatus()
    {
        var resp = await _client.PutAsJsonAsync("/api/equipment/1/status", new { status = "maintenance" });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("maintenance");
    }

    // ── P12-2：报工 ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReportProcess_AccumulatesQuantities()
    {
        // ProcessId=4（pending） → 报工后应变为 in_progress
        var resp = await _client.PostAsJsonAsync("/api/workorder/process/4/report", new
        {
            qualifiedQty = 100m,
            defectQty = 5m,
            reworkQty = 2m,
            downtimeMinutes = 15,
            downtimeReason = "换料",
            operatorId = 2L,
            operatorName = "zhangsan",
            equipmentId = 4L,
            actualCycleSec = 50,
            finish = false,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("in_progress");
        data.GetProperty("qualifiedQty").GetDecimal().Should().Be(100m);
        data.GetProperty("defectQty").GetDecimal().Should().Be(5m);
        data.GetProperty("downtimeMinutes").GetInt32().Should().Be(15);
        data.GetProperty("operatorName").GetString().Should().Be("zhangsan");
    }

    [Fact]
    public async Task ReportProcess_FinishFlag_SetsCompleted()
    {
        // 在已有的进行中工序 ProcessId=4 上 finish=true
        await _client.PostAsJsonAsync("/api/workorder/process/4/report", new { qualifiedQty = 50m, defectQty = 0m, reworkQty = 0m, downtimeMinutes = 0, downtimeReason = (string?)null, operatorId = 2L, operatorName = "zhangsan", equipmentId = 4L, actualCycleSec = (int?)null, finish = false });

        var resp = await _client.PostAsJsonAsync("/api/workorder/process/4/report", new
        {
            qualifiedQty = 0m, defectQty = 0m, reworkQty = 0m,
            downtimeMinutes = 0, downtimeReason = (string?)null,
            operatorId = (long?)null, operatorName = (string?)null,
            equipmentId = (long?)null, actualCycleSec = (int?)null,
            finish = true,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("completed");
        doc.RootElement.GetProperty("data").TryGetProperty("finishedAt", out var fin).Should().BeTrue();
        fin.ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    // ── P12-1：完工时计算 LeadTimeHr / ScheduleVarianceHr ───────────────

    [Fact]
    public async Task CompleteWorkOrder_ComputesLeadTimeAndVariance()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();

        // 注入一个 in_progress 工单（有 ActualStart + PlannedEnd）
        var wo = new MesWorkOrder
        {
            WorkOrderCode = $"WO-LT-{Guid.NewGuid().ToString("N")[..6]}",
            ProductName = "时效测试品",
            PlannedQty = 50, CompletedQty = 0, Unit = "台",
            Status = "in_progress",
            ActualStart = DateTime.UtcNow.AddHours(-10),
            PlannedEnd = DateTime.UtcNow.AddHours(-2),
            CreatedBy = 1, CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow,
        };
        db.WorkOrders.Add(wo);
        await db.SaveChangesAsync();

        var resp = await _client.PutAsJsonAsync($"/api/workorder/{wo.WorkOrderId}/complete", new { completedQty = 50m });
        resp.IsSuccessStatusCode.Should().BeTrue();

        await db.Entry(wo).ReloadAsync();
        wo.Status.Should().Be("completed");
        wo.LeadTimeHr.Should().NotBeNull();
        wo.LeadTimeHr!.Value.Should().BeGreaterThan(9);  // 大约 10 小时
        wo.ScheduleVarianceHr.Should().NotBeNull();
        wo.ScheduleVarianceHr!.Value.Should().BeGreaterThan(0); // 逾期
    }

    // ── P12-3：统计 API ─────────────────────────────────────────────────

    [Fact]
    public async Task WoEfficiency_ReturnsRates()
    {
        var resp = await _client.GetAsync("/api/stats/wo-efficiency");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("onTimeRate", out _).Should().BeTrue();
        data.TryGetProperty("avgLeadTimeHr", out _).Should().BeTrue();
        data.TryGetProperty("qualifyRate", out _).Should().BeTrue();
        data.GetProperty("completedCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task WoTrend_ReturnsArrayWithMonthKeys()
    {
        var resp = await _client.GetAsync("/api/stats/wo-trend?months=12");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        // 至少包含种子完工工单（约 11 天前完工）的月度记录
        rows.Should().NotBeEmpty();
        rows.All(r => r.GetProperty("month").GetString()!.Length == 7).Should().BeTrue(); // YYYY-MM
    }

    [Fact]
    public async Task CapacityLoad_GroupsByEquipment()
    {
        var resp = await _client.GetAsync("/api/stats/capacity-load");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").EnumerateArray().ToList();
        // 种子有 4 台设备 + 多个工序
        rows.Should().NotBeEmpty();
        rows.All(r => r.GetProperty("equipmentCode").GetString() != null).Should().BeTrue();
    }

    [Fact]
    public async Task Bottleneck_OrdersByCycleDeviationRatio()
    {
        var resp = await _client.GetAsync("/api/stats/bottleneck");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").EnumerateArray().ToList();
        rows.Should().NotBeEmpty();

        // 应按 CycleDeviationRatio DESC 排序
        var ratios = rows.Select(r => r.GetProperty("cycleDeviationRatio").GetDouble()).ToList();
        ratios.Should().BeInDescendingOrder();

        // 种子中"整机装配"应该出现（实际节拍 75 / 标准 60 = 1.25）
        rows.Any(r => r.GetProperty("processName").GetString() == "整机装配").Should().BeTrue();
    }

    [Fact]
    public async Task OperatorRanking_OrdersByQualifyRateDesc()
    {
        var resp = await _client.GetAsync("/api/stats/operator-ranking");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").EnumerateArray().ToList();
        rows.Should().NotBeEmpty();

        var rates = rows.Select(r => r.GetProperty("qualifyRate").GetDouble()).ToList();
        rates.Should().BeInDescendingOrder();
        rows.All(r => r.GetProperty("operatorName").GetString() != null).Should().BeTrue();
    }

    [Fact]
    public async Task DefectPareto_OrdersByTotalLossDesc_AndCumulativeShareCorrect()
    {
        var resp = await _client.GetAsync("/api/stats/defect-pareto");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

        if (rows.Count == 0) return; // 无 defect 数据时跳过

        // 按 totalLoss 降序
        var losses = rows.Select(r => r.GetProperty("totalLoss").GetDecimal()).ToList();
        losses.Should().BeInDescendingOrder();

        // share 之和约等于 1（最后一条的 cumulativeShare ≈ 1.0）
        var lastCum = rows[^1].GetProperty("cumulativeShare").GetDouble();
        lastCum.Should().BeApproximately(1.0, 0.001);

        // 每条都有 processName / share / sampleCount
        foreach (var r in rows)
        {
            r.GetProperty("processName").GetString().Should().NotBeNullOrEmpty();
            r.GetProperty("share").GetDouble().Should().BeGreaterThanOrEqualTo(0);
            r.GetProperty("sampleCount").GetInt32().Should().BeGreaterThan(0);
        }
    }
}
