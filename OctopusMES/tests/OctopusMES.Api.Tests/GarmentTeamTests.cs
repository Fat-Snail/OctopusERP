using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

public class GarmentTeamTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public GarmentTeamTests(MesTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task ListTeams_DefaultExcludesInactive()
    {
        var resp = await _client.GetAsync("/api/garment-team");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        // seed 注入的 9 个组都是 active
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task CreateOwnedTeam_RequiresLeaderUmcUserId()
    {
        var resp = await _client.PostAsJsonAsync("/api/garment-team", new
        {
            teamCode = "X-NoLeader",
            teamName = "X 组缺组长",
            isOutsource = false,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("LeaderUmcUserId");
    }

    [Fact]
    public async Task CreateOutsourceTeam_RequiresPartnerName()
    {
        var resp = await _client.PostAsJsonAsync("/api/garment-team", new
        {
            teamCode = "L-NoPartner",
            teamName = "外协无名",
            isOutsource = true,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("OutsourcePartnerName");
    }

    [Fact]
    public async Task CreateValidOwnedTeam_Succeeds()
    {
        var resp = await _client.PostAsJsonAsync("/api/garment-team", new
        {
            teamCode = "E-test1",
            teamName = "E 组-新人",
            groupLabel = "E组",
            leader = "新组长",
            leaderUmcUserId = 200L,
            umcDeptId = 110L,
            isOutsource = false,
            headcount = 12,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("teamCode").GetString().Should().Be("E-test1");
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("active");
    }

    [Fact]
    public async Task CreateValidOutsourceTeam_Succeeds()
    {
        var resp = await _client.PostAsJsonAsync("/api/garment-team", new
        {
            teamCode = "L-嘉禾",
            teamName = "嘉禾外协",
            isOutsource = true,
            outsourcePartnerName = "嘉禾外协厂",
            headcount = 30,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("isOutsource").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetProperty("outsourcePartnerName").GetString().Should().Be("嘉禾外协厂");
    }

    [Fact]
    public async Task CreateDuplicateTeamCode_Returns400()
    {
        await _client.PostAsJsonAsync("/api/garment-team", new
        {
            teamCode = "E-dup",
            teamName = "E 重复",
            leaderUmcUserId = 201L,
        });
        var resp = await _client.PostAsJsonAsync("/api/garment-team", new
        {
            teamCode = "E-dup",
            teamName = "E 重复",
            leaderUmcUserId = 201L,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("已存在");
    }

    [Fact]
    public async Task UpdateTeam_ChangesHeadcountAndLeader()
    {
        var createResp = await _client.PostAsJsonAsync("/api/garment-team", new
        {
            teamCode = "E-upd",
            teamName = "E 待更新",
            leaderUmcUserId = 202L,
            headcount = 10,
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var teamId = createDoc!.RootElement.GetProperty("data").GetProperty("teamId").GetInt64();

        var updResp = await _client.PutAsJsonAsync("/api/garment-team", new
        {
            teamId,
            teamCode = "E-upd",
            teamName = "E 已更新",
            leader = "新组长",
            leaderUmcUserId = 203L,
            headcount = 20,
        });
        var updDoc = await updResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        updDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        updDoc.RootElement.GetProperty("data").GetProperty("headcount").GetInt32().Should().Be(20);
        updDoc.RootElement.GetProperty("data").GetProperty("leaderUmcUserId").GetInt64().Should().Be(203);
    }

    [Fact]
    public async Task SetStatusInactive_HidesFromDefaultList()
    {
        var createResp = await _client.PostAsJsonAsync("/api/garment-team", new
        {
            teamCode = "E-inactive",
            teamName = "E 停用",
            leaderUmcUserId = 210L,
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var teamId = createDoc!.RootElement.GetProperty("data").GetProperty("teamId").GetInt64();

        // 停用
        await _client.PutAsJsonAsync($"/api/garment-team/{teamId}/status", new { status = "inactive" });

        // 默认列表(active 过滤)不出现
        var listResp = await _client.GetAsync("/api/garment-team");
        var listDoc = await listResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = listDoc!.RootElement.GetProperty("data").GetProperty("rows");
        var found = false;
        foreach (var r in rows.EnumerateArray())
            if (r.GetProperty("teamId").GetInt64() == teamId) found = true;
        found.Should().BeFalse();

        // 加 includeInactive 后能看到
        var listAllResp = await _client.GetAsync("/api/garment-team?includeInactive=true");
        var listAllDoc = await listAllResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var foundAll = false;
        foreach (var r in listAllDoc!.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray())
            if (r.GetProperty("teamId").GetInt64() == teamId) foundAll = true;
        foundAll.Should().BeTrue();
    }

    [Fact]
    public async Task CandidateLeaders_ReturnsSyncedUmcUsers_WithOccupiedFlag()
    {
        var resp = await _client.GetAsync("/api/garment-team/candidate-leaders");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().BeGreaterThan(0);
        // 每条都有 isOccupied 字段
        foreach (var r in rows.EnumerateArray())
            r.TryGetProperty("isOccupied", out _).Should().BeTrue();
    }
}
