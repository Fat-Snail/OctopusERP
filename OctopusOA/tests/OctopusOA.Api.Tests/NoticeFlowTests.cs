using FluentAssertions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 公告通知测试
///
/// 种子数据：
///   公告1：五一放假（type=2）
///   公告2：紧急系统维护（type=3）
///   公告3：欢迎李小红（type=1，admin 已读）
///   公告4：Q2 OKR 启动（type=1）
/// 默认测试用户：admin(sub=1)
/// </summary>
public class NoticeFlowTests : IClassFixture<OATestFactory>
{
    private readonly HttpClient _client;
    private const string SharedSecret = "octopus-sync-secret-key-2026";

    public NoticeFlowTests(OATestFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    private static string ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private async Task<JsonElement> GetJson(string url) =>
        JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync(url)).Content.ReadAsStringAsync());

    // ═══ 列表查询 ═══

    [Fact]
    public async Task GetList_ReturnsAllNotices()
    {
        var json = await GetJson("/api/notice/list");
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task GetList_FilterByType_Urgent()
    {
        var json = await GetJson("/api/notice/list?type=3");
        var rows = json.GetProperty("data").GetProperty("rows");
        rows.EnumerateArray().Should().OnlyContain(n => n.GetProperty("noticeType").GetString() == "3");
    }

    [Fact]
    public async Task GetList_SortedByPriorityThenTime()
    {
        var json = await GetJson("/api/notice/list");
        var rows = json.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        // 第一条应该是紧急公告（priority=10）
        rows[0].GetProperty("noticeType").GetString().Should().Be("3");
    }

    [Fact]
    public async Task GetList_IncludesReadStatus()
    {
        var json = await GetJson("/api/notice/list");
        var rows = json.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        // 种子数据：admin 已读公告3
        var n3 = rows.First(r => r.GetProperty("noticeId").GetInt64() == 3);
        n3.GetProperty("isRead").GetBoolean().Should().BeTrue();
    }

    // ═══ 未读数量 ═══

    [Fact]
    public async Task GetUnreadCount_ReturnsCorrect()
    {
        var json = await GetJson("/api/notice/unread/count");
        // admin 已读公告3，其他 3 条未读
        json.GetProperty("data").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    // ═══ 详情 + 自动标记已读 ═══

    [Fact]
    public async Task GetById_AutoMarksAsRead()
    {
        // 先确认公告4未读
        var listBefore = await GetJson("/api/notice/list");
        var n4Before = listBefore.GetProperty("data").GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("noticeId").GetInt64() == 4);
        n4Before.GetProperty("isRead").GetBoolean().Should().BeFalse();

        // 访问详情
        var detail = await GetJson("/api/notice/4");
        detail.GetProperty("code").GetInt32().Should().Be(200);

        // 再次查列表应为已读
        var listAfter = await GetJson("/api/notice/list");
        var n4After = listAfter.GetProperty("data").GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("noticeId").GetInt64() == 4);
        n4After.GetProperty("isRead").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var json = await GetJson("/api/notice/9999");
        json.GetProperty("code").GetInt32().Should().Be(404);
    }

    // ═══ 最新公告 ═══

    [Fact]
    public async Task GetLatest_Returns5()
    {
        var json = await GetJson("/api/notice/latest");
        var data = json.GetProperty("data");
        data.GetArrayLength().Should().BeLessThanOrEqualTo(5);
        data.GetArrayLength().Should().BeGreaterThanOrEqualTo(3);
    }

    // ═══ UMC 公告同步 ═══

    [Fact]
    public async Task Sync_ValidSignature_AddsNewNotice()
    {
        var payload = new
        {
            Action = "upsert", NoticeId = 100,
            Title = "新同步公告_测试", Content = "内容",
            NoticeType = "1", Priority = 2, Publisher = "admin",
            PublishTime = DateTime.UtcNow, Status = 1
        };
        var json = JsonSerializer.Serialize(payload);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/notice/sync");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(json)}");

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 验证能在列表中看到
        var listJson = await GetJson("/api/notice/list");
        var raw = listJson.GetRawText();
        raw.Should().Contain("新同步公告_测试");
    }

    [Fact]
    public async Task Sync_InvalidSignature_Returns401()
    {
        var payload = new { Action = "upsert", NoticeId = 200, Title = "伪造", Content = "", NoticeType = "1", Priority = 0, Publisher = "", PublishTime = DateTime.UtcNow, Status = 1 };
        var json = JsonSerializer.Serialize(payload);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/notice/sync");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Headers.Add("X-Sync-Signature", "sha256=bad");

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sync_UpdateExisting_OverwritesContent()
    {
        var payload = new
        {
            Action = "upsert", NoticeId = 1,
            Title = "五一假期（已更新）", Content = "假期调整内容",
            NoticeType = "2", Priority = 8, Publisher = "admin",
            PublishTime = DateTime.UtcNow, Status = 1
        };
        var json = JsonSerializer.Serialize(payload);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/notice/sync");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(json)}");

        await _client.SendAsync(req);

        var detail = await GetJson("/api/notice/1");
        detail.GetProperty("data").GetProperty("title").GetString().Should().Contain("已更新");
    }

    [Fact]
    public async Task Sync_DeleteAction_RemovesNotice()
    {
        // 先新增
        var addPayload = new { Action = "upsert", NoticeId = 300, Title = "待删除", Content = "", NoticeType = "1", Priority = 0, Publisher = "", PublishTime = DateTime.UtcNow, Status = 1 };
        var addJson = JsonSerializer.Serialize(addPayload);
        var addReq = new HttpRequestMessage(HttpMethod.Post, "/api/notice/sync");
        addReq.Content = new StringContent(addJson, Encoding.UTF8, "application/json");
        addReq.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(addJson)}");
        await _client.SendAsync(addReq);

        // 再删除
        var delPayload = new { Action = "delete", NoticeId = 300, Title = "", Content = "", NoticeType = "1", Priority = 0, Publisher = "", PublishTime = DateTime.UtcNow, Status = 0 };
        var delJson = JsonSerializer.Serialize(delPayload);
        var delReq = new HttpRequestMessage(HttpMethod.Post, "/api/notice/sync");
        delReq.Content = new StringContent(delJson, Encoding.UTF8, "application/json");
        delReq.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(delJson)}");
        await _client.SendAsync(delReq);

        var detail = await GetJson("/api/notice/300");
        detail.GetProperty("code").GetInt32().Should().Be(404);
    }
}
