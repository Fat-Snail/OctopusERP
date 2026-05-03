using FluentAssertions;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 会议室测试
///
/// 种子数据：3 间会议室（星光厅/月光厅/蓝天小屋）+ 3 条预订（今日 + 明日 + 后天）
/// </summary>
public class MeetingFlowTests : IClassFixture<OATestFactory>
{
    private readonly HttpClient _client;

    public MeetingFlowTests(OATestFactory factory)
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

    private async Task<JsonElement> SendJson(HttpMethod method, string url, object? body, string userId = "1")
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-UserId", userId);
        if (body != null) req.Content = JsonContent.Create(body);
        var resp = await _client.SendAsync(req);
        return JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
    }

    // ═══ 会议室 CRUD ═══

    [Fact]
    public async Task RoomList_ReturnsSeedRooms()
    {
        var json = await GetJson("/api/meeting/room/list");
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task RoomDetail_Equipment_ParsedAsArray()
    {
        var json = await GetJson("/api/meeting/room/1");
        var eq = json.GetProperty("data").GetProperty("equipment");
        eq.ValueKind.Should().Be(JsonValueKind.Array);
        eq.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateRoom_Success()
    {
        var json = await SendJson(HttpMethod.Post, "/api/meeting/room", new
        {
            Name = "新测试会议室", Capacity = 6, Location = "C 楼 1F",
            Equipment = new[] { "白板" }, Description = "测试房间", Status = 1,
        });
        json.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task DeleteRoom_WithFutureBooking_Fails()
    {
        // 房间 2 有明日预订（永远是未来）
        var json = await SendJson(HttpMethod.Delete, "/api/meeting/room/2", null);
        json.GetProperty("code").GetInt32().Should().Be(500);
        json.GetProperty("msg").GetString().Should().Contain("预订");
    }

    // ═══ 预订 ═══

    [Fact]
    public async Task Booking_Success()
    {
        var start = DateTime.UtcNow.AddDays(3).Date.AddHours(2); // 未来
        var json = await SendJson(HttpMethod.Post, "/api/meeting/booking", new
        {
            RoomId = 2, Title = "测试预订_OK",
            StartTime = start, EndTime = start.AddHours(1),
            Description = "测试", Attendees = new long[] { 2, 3 },
        });
        json.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task Booking_Conflict_Returns409()
    {
        // 先预订一个固定时段
        var start = DateTime.UtcNow.AddDays(5).Date.AddHours(3);
        await SendJson(HttpMethod.Post, "/api/meeting/booking", new
        {
            RoomId = 3, Title = "占位", StartTime = start, EndTime = start.AddHours(1),
            Description = "", Attendees = Array.Empty<long>(),
        });
        // 同房间同时段应冲突
        var json = await SendJson(HttpMethod.Post, "/api/meeting/booking", new
        {
            RoomId = 3, Title = "冲突", StartTime = start.AddMinutes(30), EndTime = start.AddMinutes(90),
            Description = "", Attendees = Array.Empty<long>(),
        });
        json.GetProperty("code").GetInt32().Should().Be(409);
    }

    [Fact]
    public async Task Booking_EndBeforeStart_Fails()
    {
        var start = DateTime.UtcNow.AddDays(3).Date.AddHours(5);
        var json = await SendJson(HttpMethod.Post, "/api/meeting/booking", new
        {
            RoomId = 2, Title = "错误时间",
            StartTime = start, EndTime = start.AddMinutes(-30),
            Description = "", Attendees = Array.Empty<long>(),
        });
        json.GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task Cancel_ByOther_Fails403()
    {
        var start = DateTime.UtcNow.AddDays(6).Date.AddHours(3);
        var createJson = await SendJson(HttpMethod.Post, "/api/meeting/booking", new
        {
            RoomId = 2, Title = "取消权限测试",
            StartTime = start, EndTime = start.AddHours(1),
            Description = "", Attendees = Array.Empty<long>(),
        }, "2"); // zhangsan 预订
        createJson.GetProperty("code").GetInt32().Should().Be(200);
        var bookingId = createJson.GetProperty("data").GetProperty("id").GetInt64();

        // admin 尝试取消
        var cancelJson = await SendJson(HttpMethod.Put, $"/api/meeting/booking/{bookingId}/cancel", null, "1");
        cancelJson.GetProperty("code").GetInt32().Should().Be(403);
    }

    [Fact]
    public async Task Cancel_ByOwner_Success()
    {
        var start = DateTime.UtcNow.AddDays(7).Date.AddHours(4);
        var create = await SendJson(HttpMethod.Post, "/api/meeting/booking", new
        {
            RoomId = 3, Title = "自己取消",
            StartTime = start, EndTime = start.AddHours(1),
            Description = "", Attendees = Array.Empty<long>(),
        }, "1");
        var bookingId = create.GetProperty("data").GetProperty("id").GetInt64();

        var cancel = await SendJson(HttpMethod.Put, $"/api/meeting/booking/{bookingId}/cancel", null, "1");
        cancel.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task Mine_ReturnsOwnBookings()
    {
        // admin(1) 有种子预订 1
        var json = await GetJson("/api/meeting/booking/mine", "1");
        json.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Today_ContainsOwnOrAttendee()
    {
        // admin 在今日预订中（预订 1 的创建者 + 预订 3 的受邀者）
        var json = await GetJson("/api/meeting/booking/today", "1");
        json.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task RoomCalendar_ReturnsRangeBookings()
    {
        var today = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd");
        var json = await GetJson($"/api/meeting/room/1/calendar?date={today}&range=week");
        json.GetProperty("code").GetInt32().Should().Be(200);
    }
}
