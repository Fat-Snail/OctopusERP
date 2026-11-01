using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusMES.Api.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

public class GarmentScheduleTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public GarmentScheduleTests(MesTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetTeams_ReturnsSeededGarmentTeams()
    {
        var resp = await _client.GetAsync("/api/garment-schedule/teams");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        // 22春夏 真实数据有 9 个组（A/B/C/D + L下 5个）
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        var rows = doc.RootElement.GetProperty("data").GetProperty("rows");
        rows.EnumerateArray().Should().Contain(t => t.GetProperty("teamCode").GetString() == "A");
    }

    [Fact]
    public async Task GetCalendar_RequiresValidDateRange()
    {
        var resp = await _client.GetAsync("/api/garment-schedule/calendar?startDate=2022-01-01&endDate=2021-12-01");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task GetCalendar_ReturnsTeamsAndDateAxis()
    {
        // 真实数据 22春夏 范围: 2021-10-01 ~ 2022-04-30
        var resp = await _client.GetAsync("/api/garment-schedule/calendar?startDate=2021-10-01&endDate=2021-10-15");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("teams").GetArrayLength().Should().BeGreaterThan(0);
        data.GetProperty("dates").GetArrayLength().Should().Be(15);  // 10/01 - 10/15
        data.GetProperty("dates")[0].GetString().Should().Be("2021-10-01");
        data.GetProperty("dates")[14].GetString().Should().Be("2021-10-15");
        // cells 是字典：至少有几条数据
        data.GetProperty("cells").EnumerateObject().Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListSchedules_FilterByTeamAndDate()
    {
        var resp = await _client.GetAsync("/api/garment-schedule?from=2021-10-01&to=2021-10-31&pageSize=50");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        var rows = doc.RootElement.GetProperty("data").GetProperty("rows");
        foreach (var row in rows.EnumerateArray())
        {
            var dateStr = row.GetProperty("scheduleDate").GetString()!;
            dateStr.Should().StartWith("2021-10");
        }
    }

    [Fact]
    public async Task ListSchedules_KeywordSearchByCustomer()
    {
        var resp = await _client.GetAsync("/api/garment-schedule?keyword=客户001&pageSize=50");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var row in rows.EnumerateArray())
        {
            var matches =
                (row.GetProperty("customer").GetString() ?? "").Contains("客户001") ||
                (row.GetProperty("orderNo").GetString() ?? "").Contains("客户001") ||
                (row.GetProperty("styleCode").GetString() ?? "").Contains("客户001");
            matches.Should().BeTrue();
        }
    }

    [Fact]
    public async Task CreateSchedule_UpsertsByTeamAndDate()
    {
        var teamsResp = await _client.GetAsync("/api/garment-schedule/teams");
        var teamsDoc = await teamsResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var teamId = teamsDoc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("teamId").GetInt64();

        var date = new DateTime(2099, 1, 15);  // 远期日期, 避免和 seed 冲突

        var resp1 = await _client.PostAsJsonAsync("/api/garment-schedule", new
        {
            teamId,
            scheduleDate = date,
            season = "测试季",
            customer = "测试客户",
            orderNo = "T001",
            styleCode = "TEST-CODE-001",
            plannedQty = 500,
            sewingQty = 320,
            cuttingQty = 400,
            totalHours = 10,
            headcount = 12,
            avgWage = 150.0,
            fabricStatus = "ok",
        });
        var doc1 = await resp1.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc1!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var firstId = doc1.RootElement.GetProperty("data").GetProperty("scheduleId").GetInt64();

        // 同一 (team, date) 再次提交应 upsert 而非新增
        var resp2 = await _client.PostAsJsonAsync("/api/garment-schedule", new
        {
            teamId,
            scheduleDate = date,
            customer = "测试客户-修改",
            sewingQty = 380,
        });
        var doc2 = await resp2.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc2!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc2.RootElement.GetProperty("data").GetProperty("scheduleId").GetInt64().Should().Be(firstId);
        doc2.RootElement.GetProperty("data").GetProperty("customer").GetString().Should().Be("测试客户-修改");
    }

    [Fact]
    public async Task GetMonthlySummary_AggregatesByTeamAndMonth()
    {
        var resp = await _client.GetAsync("/api/garment-schedule/monthly-summary?season=22春夏");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.RootElement.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var row in rows.EnumerateArray())
        {
            row.GetProperty("yearMonth").GetString().Should().MatchRegex(@"\d{4}-\d{2}");
            row.TryGetProperty("plannedQty", out _).Should().BeTrue();
            row.TryGetProperty("sewingQty", out _).Should().BeTrue();
            row.TryGetProperty("achievementRate", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task BatchCreate_CreatesMultipleSchedules_OneCallPerSelectedDate()
    {
        var teamsResp = await _client.GetAsync("/api/garment-schedule/teams");
        var teamsDoc = await teamsResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var teamId = teamsDoc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("teamId").GetInt64();

        // 选 3 个非连续远期日期
        var dates = new[]
        {
            new DateTime(2099, 3, 1),
            new DateTime(2099, 3, 3),
            new DateTime(2099, 3, 8),
        };

        var resp = await _client.PostAsJsonAsync("/api/garment-schedule/batch", new
        {
            teamId,
            dates,
            season = "批量测试季",
            customer = "批量客户",
            orderNo = "B001",
            styleCode = "BATCH-001",
            plannedQty = 200,
            fabricStatus = "ok",
            remark = "日历多选批量创建",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("created").GetInt32().Should().Be(3);
        data.GetProperty("updated").GetInt32().Should().Be(0);
        data.GetProperty("total").GetInt32().Should().Be(3);
        data.GetProperty("scheduleIds").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task BatchCreate_UpsertsExistingDates_UpdatesInsteadOfDuplicate()
    {
        var teamsResp = await _client.GetAsync("/api/garment-schedule/teams");
        var teamsDoc = await teamsResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var teamId = teamsDoc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("teamId").GetInt64();

        // 第一次：创建 2 个
        var first = await _client.PostAsJsonAsync("/api/garment-schedule/batch", new
        {
            teamId,
            dates = new[] { new DateTime(2099, 4, 1), new DateTime(2099, 4, 2) },
            customer = "原客户",
            orderNo = "U001",
            plannedQty = 100,
        });
        var firstDoc = await first.Content.ReadFromJsonAsync<JsonDocument>(_json);
        firstDoc!.RootElement.GetProperty("data").GetProperty("created").GetInt32().Should().Be(2);

        // 第二次：含 1 个已存在 + 1 个新增 → 期望 1 update + 1 create
        var second = await _client.PostAsJsonAsync("/api/garment-schedule/batch", new
        {
            teamId,
            dates = new[] { new DateTime(2099, 4, 2), new DateTime(2099, 4, 3) },
            customer = "新客户",
            orderNo = "U001-V2",
            plannedQty = 150,
        });
        var secondDoc = await second.Content.ReadFromJsonAsync<JsonDocument>(_json);
        secondDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = secondDoc.RootElement.GetProperty("data");
        data.GetProperty("created").GetInt32().Should().Be(1);
        data.GetProperty("updated").GetInt32().Should().Be(1);

        // 验证 4-2 的客户已被更新为"新客户"
        var listResp = await _client.GetAsync($"/api/garment-schedule?from=2099-04-02&to=2099-04-02&teamId={teamId}");
        var listDoc = await listResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = listDoc!.RootElement.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().Be(1);
        rows[0].GetProperty("customer").GetString().Should().Be("新客户");
    }

    [Fact]
    public async Task BatchCreate_RejectsEmptyDates()
    {
        var teamsResp = await _client.GetAsync("/api/garment-schedule/teams");
        var teamsDoc = await teamsResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var teamId = teamsDoc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("teamId").GetInt64();

        var resp = await _client.PostAsJsonAsync("/api/garment-schedule/batch", new
        {
            teamId,
            dates = Array.Empty<DateTime>(),
            customer = "空日期测试",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("至少");
    }

    [Fact]
    public async Task BatchCreate_RejectsNonexistentTeam()
    {
        var resp = await _client.PostAsJsonAsync("/api/garment-schedule/batch", new
        {
            teamId = 999999L,
            dates = new[] { new DateTime(2099, 5, 1) },
            customer = "测试",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("组别不存在");
    }

    [Fact]
    public async Task BatchCreate_DeduplicatesDates()
    {
        var teamsResp = await _client.GetAsync("/api/garment-schedule/teams");
        var teamsDoc = await teamsResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var teamId = teamsDoc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("teamId").GetInt64();

        // 故意传入重复日期
        var resp = await _client.PostAsJsonAsync("/api/garment-schedule/batch", new
        {
            teamId,
            dates = new[]
            {
                new DateTime(2099, 6, 1),
                new DateTime(2099, 6, 1),  // 重复
                new DateTime(2099, 6, 2),
            },
            customer = "去重测试",
            orderNo = "D001",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        // 去重后应只有 2 条
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().Be(2);
    }

    // ── 归档相关测试 ──────────────────────────────────────────
    // 这些测试临时切换 ArchiveCutoff 为 2022-01-01，让 2021-10 seed 数据进入归档态。
    // try/finally 恢复默认（远古），避免污染其他测试。

    [Fact]
    public async Task ArchiveSummary_GroupsByYearAndMonth()
    {
        var prev = GarmentScheduleService.CutoffProvider;
        GarmentScheduleService.CutoffProvider = () => new DateTime(2022, 1, 1);
        try
        {
            var resp = await _client.GetAsync("/api/garment-schedule/archive/summary");
            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
            doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
            var data = doc.RootElement.GetProperty("data");
            data.GetProperty("archiveCutoff").GetString().Should().Be("2022-01-01");
            data.GetProperty("totalArchived").GetInt32().Should().BeGreaterThan(0);

            var years = data.GetProperty("years");
            years.GetArrayLength().Should().BeGreaterThan(0);
            var first = years[0];
            first.GetProperty("year").GetInt32().Should().Be(2021);
            first.GetProperty("months").GetArrayLength().Should().BeGreaterThan(0);
            first.GetProperty("seasons").GetArrayLength().Should().BeGreaterThan(0);
        }
        finally { GarmentScheduleService.CutoffProvider = prev; }
    }

    [Fact]
    public async Task UpdateArchivedSchedule_ReturnsForbidden()
    {
        // 先用默认（远古 cutoff）改一条 2021 记录拿到 ID
        var listResp = await _client.GetAsync("/api/garment-schedule?from=2021-10-15&to=2021-10-16&pageSize=1");
        var listDoc = await listResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = listDoc!.RootElement.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().BeGreaterThan(0);
        var scheduleId = rows[0].GetProperty("scheduleId").GetInt64();
        var teamId = rows[0].GetProperty("teamId").GetInt64();

        var prev = GarmentScheduleService.CutoffProvider;
        GarmentScheduleService.CutoffProvider = () => new DateTime(2022, 1, 1);
        try
        {
            // 试图更新已归档 (2021-10) 的排程
            var resp = await _client.PutAsJsonAsync("/api/garment-schedule", new
            {
                scheduleId,
                teamId,
                scheduleDate = "2021-10-15",
                customer = "尝试改归档",
            });
            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
            doc!.RootElement.GetProperty("code").GetInt32().Should().Be(403);
            doc.RootElement.GetProperty("msg").GetString().Should().Contain("归档");
        }
        finally { GarmentScheduleService.CutoffProvider = prev; }
    }

    [Fact]
    public async Task DeleteArchivedSchedule_ReturnsForbidden()
    {
        var listResp = await _client.GetAsync("/api/garment-schedule?from=2021-10-15&to=2021-10-16&pageSize=1");
        var listDoc = await listResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var scheduleId = listDoc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("scheduleId").GetInt64();

        var prev = GarmentScheduleService.CutoffProvider;
        GarmentScheduleService.CutoffProvider = () => new DateTime(2022, 1, 1);
        try
        {
            var resp = await _client.DeleteAsync($"/api/garment-schedule/{scheduleId}");
            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
            doc!.RootElement.GetProperty("code").GetInt32().Should().Be(403);
            doc.RootElement.GetProperty("msg").GetString().Should().Contain("归档");
        }
        finally { GarmentScheduleService.CutoffProvider = prev; }
    }

    [Fact]
    public async Task BatchCreate_RejectsArchivedDates()
    {
        var teamsResp = await _client.GetAsync("/api/garment-schedule/teams");
        var teamsDoc = await teamsResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var teamId = teamsDoc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("teamId").GetInt64();

        var prev = GarmentScheduleService.CutoffProvider;
        // cutoff = 2025-01-01 → 2024-12-01 是归档日期
        GarmentScheduleService.CutoffProvider = () => new DateTime(2025, 1, 1);
        try
        {
            var resp = await _client.PostAsJsonAsync("/api/garment-schedule/batch", new
            {
                teamId,
                dates = new[]
                {
                    new DateTime(2024, 12, 1),  // 归档
                    new DateTime(2099, 12, 1),  // 未归档
                },
                customer = "归档测试",
            });
            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
            doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
            doc.RootElement.GetProperty("msg").GetString().Should().Contain("归档");
        }
        finally { GarmentScheduleService.CutoffProvider = prev; }
    }

    [Fact]
    public async Task Calendar_TagsCellsAsArchived_WhenDateBeforeCutoff()
    {
        var prev = GarmentScheduleService.CutoffProvider;
        GarmentScheduleService.CutoffProvider = () => new DateTime(2022, 1, 1);
        try
        {
            var resp = await _client.GetAsync("/api/garment-schedule/calendar?startDate=2021-10-01&endDate=2021-10-05");
            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
            doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
            var data = doc.RootElement.GetProperty("data");
            data.GetProperty("archiveCutoff").GetString().Should().Be("2022-01-01");
            // 至少一个 cell 标记 archived=true（2021-10 数据全部归档）
            var cellsObj = data.GetProperty("cells");
            var anyArchived = cellsObj.EnumerateObject().Any(p => p.Value.GetProperty("archived").GetBoolean());
            anyArchived.Should().BeTrue();
        }
        finally { GarmentScheduleService.CutoffProvider = prev; }
    }

    [Fact]
    public async Task DeleteSchedule_RemovesRecord()
    {
        // 找一条已有记录
        var listResp = await _client.GetAsync("/api/garment-schedule?pageSize=1");
        var listDoc = await listResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var first = listDoc!.RootElement.GetProperty("data").GetProperty("rows")[0];
        var id = first.GetProperty("scheduleId").GetInt64();

        var delResp = await _client.DeleteAsync($"/api/garment-schedule/{id}");
        var delDoc = await delResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        delDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var getResp = await _client.GetAsync($"/api/garment-schedule/{id}");
        var getDoc = await getResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        getDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(404);
    }
}
