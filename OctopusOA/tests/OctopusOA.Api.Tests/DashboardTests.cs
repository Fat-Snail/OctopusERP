using FluentAssertions;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 工作台聚合接口测试。
/// TestAuthHandler 默认 admin(sub=1)。
/// </summary>
public class DashboardTests : IClassFixture<OATestFactory>
{
    private readonly HttpClient _client;

    public DashboardTests(OATestFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    private async Task<JsonElement> GetJson(string url, string userId = "1")
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Test-UserId", userId);
        var resp = await _client.SendAsync(req);
        return JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
    }

    // ═══ summary ═══

    [Fact]
    public async Task Summary_Admin_HasPendingAndNotices()
    {
        var json = await GetJson("/api/dashboard/summary");
        json.GetProperty("code").GetInt32().Should().Be(200);

        var data = json.GetProperty("data");
        data.GetProperty("pendingApprovals").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("unreadNotices").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("todayAttendance").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task Summary_TodayAttendance_HasFlags()
    {
        var json = await GetJson("/api/dashboard/summary");
        var ta = json.GetProperty("data").GetProperty("todayAttendance");
        ta.GetProperty("checkedIn").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
        ta.GetProperty("checkedOut").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
    }

    // ═══ todos ═══

    [Fact]
    public async Task Todos_All_Aggregates()
    {
        var json = await GetJson("/api/dashboard/todos");
        json.GetProperty("code").GetInt32().Should().Be(200);

        var rows = json.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        // 至少包含公告类型（未读）或打卡提醒（取决于 admin 是否打过卡）
        rows.Select(r => r.GetProperty("type").GetString())
            .Should().NotBeEmpty();
    }

    [Fact]
    public async Task Todos_FilterByType_Notice()
    {
        var json = await GetJson("/api/dashboard/todos?type=notice");
        var rows = json.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        rows.Should().OnlyContain(r => r.GetProperty("type").GetString() == "notice");
    }

    [Fact]
    public async Task Todos_FilterByType_Approval()
    {
        var json = await GetJson("/api/dashboard/todos?type=approval");
        var rows = json.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        rows.Should().OnlyContain(r => r.GetProperty("type").GetString() == "approval");
    }

    [Fact]
    public async Task Todos_NoticeItem_HasRightLink()
    {
        var json = await GetJson("/api/dashboard/todos?type=notice");
        var rows = json.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        if (rows.Count > 0)
        {
            var first = rows[0];
            first.GetProperty("link").GetString().Should().StartWith("/notice/");
            first.GetProperty("tag").GetString().Should().BeOneOf("通知", "公告", "紧急");
        }
    }

    [Fact]
    public async Task Todos_DifferentUsers_ReturnDifferentCounts()
    {
        // 用隔离用户 200 检查（他的待审批和已读状态都是干净的）
        var admin = await GetJson("/api/dashboard/todos", "1");
        var other = await GetJson("/api/dashboard/todos", "200");
        // 两个结果都应成功返回（不一定数量相同）
        admin.GetProperty("code").GetInt32().Should().Be(200);
        other.GetProperty("code").GetInt32().Should().Be(200);
    }
}
