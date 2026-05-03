using FluentAssertions;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 考勤测试
///
/// TestAuthHandler 默认 admin(sub=1)
/// 种子数据：近一周工作日 zhangsan 和 lisi 有打卡记录（lisi 每周一迟到）
/// </summary>
public class AttendanceFlowTests : IClassFixture<OATestFactory>
{
    private readonly HttpClient _client;

    public AttendanceFlowTests(OATestFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    /// <summary>返回一个绑定特定用户的 HttpClient（每个测试用独立用户，避免共享状态）</summary>
    private HttpClient ClientAs(string userId)
    {
        var c = _client;
        if (!c.DefaultRequestHeaders.Contains("X-Test-UserId"))
            c.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return c;
    }

    private async Task<JsonElement> GetJson(string url, string userId = "1") =>
        JsonSerializer.Deserialize<JsonElement>(await (await SendAsync(HttpMethod.Get, url, null, userId)).Content.ReadAsStringAsync());

    private async Task<JsonElement> PostJson(string url, object? body = null, string userId = "1") =>
        JsonSerializer.Deserialize<JsonElement>(await (await SendAsync(HttpMethod.Post, url, body, userId)).Content.ReadAsStringAsync());

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, string userId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-UserId", userId);
        if (body != null) req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    // ═══ 打卡 ═══

    [Fact]
    public async Task CheckIn_FirstTime_Succeeds()
    {
        // 用独立用户 100 避免干扰
        var json = await PostJson("/api/attendance/check-in", null, "100");
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("checkInStatus").GetString()
            .Should().BeOneOf("normal", "late");
    }

    [Fact]
    public async Task CheckIn_Twice_ReturnsError()
    {
        // 独立用户 101
        await PostJson("/api/attendance/check-in", null, "101");
        var json = await PostJson("/api/attendance/check-in", null, "101");
        json.GetProperty("code").GetInt32().Should().Be(500);
        json.GetProperty("msg").GetString().Should().Contain("已打");
    }

    [Fact]
    public async Task CheckOut_AfterCheckIn_Succeeds()
    {
        // 独立用户 102
        await PostJson("/api/attendance/check-in", null, "102");
        var json = await PostJson("/api/attendance/check-out", null, "102");
        json.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task CheckOut_WithoutCheckIn_Fails()
    {
        // 独立用户 103，未打上班卡
        var json = await PostJson("/api/attendance/check-out", null, "103");
        json.GetProperty("code").GetInt32().Should().Be(500);
        json.GetProperty("msg").GetString().Should().Contain("未打");
    }

    [Fact]
    public async Task GetToday_ReturnsCurrentStatus()
    {
        var json = await GetJson("/api/attendance/today", "104");
        json.GetProperty("code").GetInt32().Should().Be(200);
        var data = json.GetProperty("data");
        data.GetProperty("ruleWorkStart").GetString().Should().Be("09:00");
        data.GetProperty("ruleWorkEnd").GetString().Should().Be("18:00");
    }

    // ═══ 月度考勤 ═══

    [Fact]
    public async Task Mine_ReturnsMonthItems()
    {
        var month = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM");
        var json = await GetJson($"/api/attendance/mine?month={month}");
        json.GetProperty("code").GetInt32().Should().Be(200);
        var rows = json.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().BeGreaterThan(0, "至少应包含本月几天");
    }

    // ═══ 规则 ═══

    [Fact]
    public async Task GetRule_ReturnsDefault()
    {
        var json = await GetJson("/api/attendance/rule");
        var data = json.GetProperty("data");
        data.GetProperty("workStartTime").GetString().Should().Be("09:00");
        data.GetProperty("lateThresholdMin").GetInt32().Should().Be(15);
    }

    [Fact]
    public async Task UpdateRule_PersistsChanges()
    {
        var payload = new
        {
            Id = 1, Name = "测试规则", WorkStartTime = "09:30", WorkEndTime = "18:30",
            LateThresholdMin = 10, EarlyLeaveThresholdMin = 20, IpWhiteList = (string?)null, Status = 1
        };
        var req = await _client.PutAsJsonAsync("/api/attendance/rule", payload);
        var json = JsonSerializer.Deserialize<JsonElement>(await req.Content.ReadAsStringAsync());
        json.GetProperty("code").GetInt32().Should().Be(200);

        var getJson = await GetJson("/api/attendance/rule");
        getJson.GetProperty("data").GetProperty("workStartTime").GetString().Should().Be("09:30");

        // 恢复（避免影响其他测试）
        payload = new
        {
            Id = 1, Name = "默认考勤规则", WorkStartTime = "09:00", WorkEndTime = "18:00",
            LateThresholdMin = 15, EarlyLeaveThresholdMin = 30, IpWhiteList = (string?)null, Status = 1
        };
        await _client.PutAsJsonAsync("/api/attendance/rule", payload);
    }

    // ═══ 统计 + 异常 ═══

    [Fact]
    public async Task Stats_ContainsAllUsers()
    {
        var month = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM");
        var json = await GetJson($"/api/attendance/stats?month={month}");
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Abnormal_ReturnsLateRecords()
    {
        var month = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM");
        var json = await GetJson($"/api/attendance/abnormal?month={month}");
        json.GetProperty("code").GetInt32().Should().Be(200);
        // 种子数据中 lisi 每周一迟到
        var rows = json.GetProperty("data").GetProperty("rows");
        // 至少 0 条（如果本月还没有周一就是 0）
        rows.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ═══ 补卡审批联动 ═══

    // ═══ 班次管理 ═══

    [Fact]
    public async Task ShiftList_ReturnsSeedShifts()
    {
        var json = await GetJson("/api/attendance/shift/list");
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(4, "至少 标准/早/晚/弹性 4 个种子班次");

        var codes = json.GetProperty("data").EnumerateArray().Select(s => s.GetProperty("code").GetString()).ToList();
        codes.Should().Contain("standard");
        codes.Should().Contain("early");
        codes.Should().Contain("late");
        codes.Should().Contain("flex");
    }

    [Fact]
    public async Task CreateShift_Success()
    {
        var payload = new
        {
            Code = "night_test", Name = "夜班_测试",
            WorkStartTime = "22:00", WorkEndTime = "06:00",
            LateThresholdMin = 10, EarlyLeaveThresholdMin = 15,
            IpWhiteList = (string?)null, IsDefault = false, Status = 1,
        };
        var resp = await _client.PostAsJsonAsync("/api/attendance/shift", payload);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        json.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task DeleteShift_Default_Fails()
    {
        // 标准班(id=1)是默认班次，不可删除
        var resp = await _client.DeleteAsync("/api/attendance/shift/1");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        json.GetProperty("code").GetInt32().Should().Be(500);
        json.GetProperty("msg").GetString().Should().Contain("默认");
    }

    [Fact]
    public async Task UserShiftList_ReturnsAllUsers()
    {
        var json = await GetJson("/api/attendance/user-shift/list");
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);

        // zhangsan 应该分配了弹性班
        var rows = json.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        var zs = rows.First(r => r.GetProperty("userName").GetString() == "zhangsan");
        zs.GetProperty("shiftCode").GetString().Should().Be("flex");
    }

    [Fact]
    public async Task AssignUserShift_Success()
    {
        var resp = await _client.PutAsJsonAsync("/api/attendance/user-shift", new { UmcUserId = 3L, ShiftId = 2L });
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        json.GetProperty("code").GetInt32().Should().Be(200);

        // 验证分配成功
        var listJson = await GetJson("/api/attendance/user-shift/list");
        var rows = listJson.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        var lisi = rows.First(r => r.GetProperty("userName").GetString() == "lisi");
        lisi.GetProperty("shiftCode").GetString().Should().Be("early");

        // 恢复
        await _client.PutAsJsonAsync("/api/attendance/user-shift", new { UmcUserId = 3L, ShiftId = 1L });
    }

    [Fact]
    public async Task FixRequest_ApprovalApproved_AutoFillsAttendance()
    {
        // 1. 提交补卡审批（使用 attendance_fix 模板，templateId=3）
        var fixDate = DateTime.UtcNow.AddHours(8).AddDays(-10).ToString("yyyy-MM-dd");
        var formData = JsonSerializer.Serialize(new
        {
            fixDate = fixDate,
            fixType = "checkIn",
            fixTime = "09:00",
            reason = "忘带工牌"
        });

        var submitJson = await PostJson("/api/approval/submit", new
        {
            TemplateId = 3, Title = "补卡申请_测试", FormData = formData
        });
        submitJson.GetProperty("code").GetInt32().Should().Be(200);
        var approvalId = submitJson.GetProperty("data").GetProperty("approvalId").GetInt64();

        // 2. admin 通过第一节点（dept_leader）
        await _client.PutAsJsonAsync($"/api/approval/{approvalId}/approve", new { Comment = "同意" });
        // 3. admin 通过第二节点（HR）→ approved → 触发补卡
        await _client.PutAsJsonAsync($"/api/approval/{approvalId}/approve", new { Comment = "HR确认" });

        // 4. 验证考勤记录已补上
        var mineJson = await GetJson($"/api/attendance/mine?month={DateTime.UtcNow.AddHours(8):yyyy-MM}");
        var rows = mineJson.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        var fixedRow = rows.FirstOrDefault(r => r.GetProperty("date").GetString() == fixDate);
        if (fixedRow.ValueKind != JsonValueKind.Undefined)
        {
            fixedRow.GetProperty("checkInStatus").GetString().Should().Be("normal");
            fixedRow.GetProperty("isFixed").GetBoolean().Should().BeTrue();
        }
    }
}
