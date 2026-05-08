using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusCRM.Api.Tests;

public class StatsTests : IClassFixture<CrmTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public StatsTests(CrmTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetSummary_ReturnsSalesMetrics()
    {
        var resp = await _client.GetAsync("/api/stats/summary");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var data = doc.RootElement.GetProperty("data");
        // 验证必要字段存在（字段名与 StatsSummaryResponse record 属性对应）
        data.TryGetProperty("totalCustomers", out _).Should().BeTrue();
        data.TryGetProperty("openInquiries", out _).Should().BeTrue();
        data.TryGetProperty("activeContracts", out _).Should().BeTrue();
        data.TryGetProperty("totalContractAmount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetPipeline_ReturnsInProgressContracts()
    {
        var resp = await _client.GetAsync("/api/stats/pipeline");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var data = doc.RootElement.GetProperty("data");
        // pipeline 返回活跃合同列表
        data.ValueKind.Should().Be(JsonValueKind.Array);
        // 种子数据有1个 active 合同
        data.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetOverdue_ReturnsOverdueContracts()
    {
        var resp = await _client.GetAsync("/api/stats/overdue");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        // 种子合同 DeliveryDate = now+30，未逾期
        // 只验证接口正常响应
        doc.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetSummary_TotalContractAmountIncludesSeedContract()
    {
        var resp = await _client.GetAsync("/api/stats/summary");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var totalAmount = doc!.RootElement.GetProperty("data")
            .GetProperty("totalContractAmount").GetDecimal();
        // 种子合同 280,000
        totalAmount.Should().BeGreaterThanOrEqualTo(280000m);
    }
}
