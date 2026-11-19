using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

/// <summary>Q3-④ 设备 OEE 看板（报工口径）：基于 seed 报工数据的精确数值验证</summary>
public class OeeTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public OeeTests(MesTestFactory factory) => _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<JsonElement> GetOeeAsync(string? query = null)
    {
        var resp = await _client.GetAsync("/api/stats/oee" + (query ?? ""));
        resp.IsSuccessStatusCode.Should().BeTrue();
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        return doc.RootElement.GetProperty("data");
    }

    private static JsonElement Equipment(JsonElement data, string code) =>
        data.GetProperty("equipments").EnumerateArray()
            .Single(e => e.GetProperty("equipmentCode").GetString() == code);

    [Fact]
    public async Task Oee_ComputedFromSeedReports_WithExactValues()
    {
        var data = await GetOeeAsync();

        // 全部 4 台设备都在看板上（含 pending 无报工的会以 0 值出现；此处 4 台均有数据）
        data.GetProperty("equipments").GetArrayLength().Should().Be(4);

        // 设备 1（EQ-SMT-01）：completed 5d 窗口 / 0 停机 / 1000 件 / 节拍 30→32
        // A=1.0, P=30/32=0.9375, Q=990/1000=0.99, OEE=0.928125
        var eq1 = Equipment(data, "EQ-SMT-01");
        eq1.GetProperty("availability").GetDouble().Should().BeApproximately(1.0, 0.0001);
        eq1.GetProperty("performance").GetDouble().Should().BeApproximately(0.9375, 0.0001);
        eq1.GetProperty("quality").GetDouble().Should().BeApproximately(0.99, 0.0001);
        eq1.GetProperty("oee").GetDouble().Should().BeApproximately(0.9281, 0.001);
        eq1.GetProperty("totalQty").GetDecimal().Should().Be(1000m);
        eq1.GetProperty("downtimeMinutes").GetInt32().Should().Be(0);

        // 设备 2（EQ-SMT-02）：节拍 30→28（快于标准）→ 表现率按教科书口径封顶 1.0
        var eq2 = Equipment(data, "EQ-SMT-02");
        eq2.GetProperty("performance").GetDouble().Should().Be(1.0);
        eq2.GetProperty("quality").GetDouble().Should().BeApproximately(0.99, 0.0001);
        eq2.GetProperty("oee").GetDouble().Should().BeApproximately(0.99, 0.001);

        // 设备 3（EQ-ASS-01）：completed 8d（60→60）+ in_progress 持续计时（60→75，停机 30min）
        // A≈23010/23040≈0.9987, P=(855×60+2000×60)/(855×75+2000×60)≈0.9304, Q=2850/2855≈0.9982
        var eq3 = Equipment(data, "EQ-ASS-01");
        eq3.GetProperty("availability").GetDouble().Should().BeApproximately(0.9987, 0.001);
        eq3.GetProperty("performance").GetDouble().Should().BeApproximately(0.9304, 0.001);
        eq3.GetProperty("quality").GetDouble().Should().BeApproximately(0.9982, 0.001);
        eq3.GetProperty("oee").GetDouble().Should().BeApproximately(0.9273, 0.005);
        eq3.GetProperty("downtimeMinutes").GetInt32().Should().Be(30);

        // 设备 4（EQ-TST-01）：completed 1d（45→45）/ 2000 件 / 良 1995 → A=P=1.0, Q=0.9975
        var eq4 = Equipment(data, "EQ-TST-01");
        eq4.GetProperty("availability").GetDouble().Should().BeApproximately(1.0, 0.0001);
        eq4.GetProperty("performance").GetDouble().Should().BeApproximately(1.0, 0.0001);
        eq4.GetProperty("quality").GetDouble().Should().BeApproximately(0.9975, 0.0001);
        eq4.GetProperty("oee").GetDouble().Should().BeApproximately(0.9975, 0.001);

        // 全局汇总（时间/产量加权）：A≈0.9993, P≈0.9701, Q≈0.9949, OEE≈0.9643
        var overall = data.GetProperty("overall");
        overall.GetProperty("availability").GetDouble().Should().BeApproximately(0.9993, 0.001);
        overall.GetProperty("performance").GetDouble().Should().BeApproximately(0.9701, 0.001);
        overall.GetProperty("quality").GetDouble().Should().BeApproximately(0.9949, 0.001);
        overall.GetProperty("oee").GetDouble().Should().BeApproximately(0.9643, 0.005);
    }

    [Fact]
    public async Task Oee_EquipmentsAreSortedByOeeDescending()
    {
        var data = await GetOeeAsync();
        var codes = data.GetProperty("equipments").EnumerateArray()
            .Select(e => e.GetProperty("equipmentCode").GetString()).ToList();

        // seed 数值排序：EQ-TST-01(0.9975) > EQ-SMT-02(0.99) > EQ-SMT-01(0.9281) > EQ-ASS-01(0.9273)
        codes.Should().Equal("EQ-TST-01", "EQ-SMT-02", "EQ-SMT-01", "EQ-ASS-01");
    }

    [Fact]
    public async Task Oee_WorkCenterFilter_ReturnsOnlyMatchingEquipments()
    {
        var data = await GetOeeAsync("?workCenter=SMT%E8%BD%A6%E9%97%B4"); // SMT车间

        var codes = data.GetProperty("equipments").EnumerateArray()
            .Select(e => e.GetProperty("equipmentCode").GetString()).ToList();
        codes.Should().BeEquivalentTo("EQ-SMT-01", "EQ-SMT-02");

        // 全局汇总只聚合该车间
        var overall = data.GetProperty("overall");
        overall.GetProperty("quality").GetDouble().Should().BeApproximately(
            (990.0 + 1980) / (1000 + 2000), 0.0005);
    }
}
