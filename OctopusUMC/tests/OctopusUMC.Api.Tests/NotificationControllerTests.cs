using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// P9 — 跨系统统一通知中心测试
/// - POST /api/internal/notify：HMAC 签名验证 + 持久化 + 实时推送
/// - GET  /api/notifications：列表 + 分页 + 过滤
/// - GET  /api/notifications/unread-count：未读数
/// - PUT  /api/notifications/{id}/read：单条已读
/// - PUT  /api/notifications/read-all：全部已读
/// </summary>
public class NotificationControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Secret = "umc-notify-secret-dev"; // 与 appsettings 默认一致
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NotificationControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static string Sign(string body) =>
        "sha256=" + Convert.ToHexString(
            new HMACSHA256(Encoding.UTF8.GetBytes(Secret)).ComputeHash(Encoding.UTF8.GetBytes(body)))
            .ToLower();

    private async Task<HttpResponseMessage> PostNotifyAsync(object payload)
    {
        var body = JsonSerializer.Serialize(payload);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/notify")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Notify-Signature", Sign(body));
        return await _client.SendAsync(req);
    }

    [Fact]
    public async Task Notify_WithValidSignature_PersistsNotification()
    {
        var resp = await PostNotifyAsync(new
        {
            userId = 1L,
            source = "oa",
            type = "approval_result",
            title = "审批通过：年假申请",
            message = "您的年假申请已通过",
            linkUrl = "/approval/detail/100",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("code").GetInt32().Should().Be(200);
        var notifId = doc.GetProperty("data").GetProperty("notificationId").GetInt64();
        notifId.Should().BeGreaterThan(0);

        // 验证已持久化
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var n = db.UserNotifications.FirstOrDefault(x => x.NotificationId == notifId);
        n.Should().NotBeNull();
        n!.UserId.Should().Be(1);
        n.Source.Should().Be("oa");
        n.Type.Should().Be("approval_result");
        n.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Notify_WithBadSignature_Returns401()
    {
        var body = JsonSerializer.Serialize(new { userId = 1, title = "test" });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/notify")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Notify-Signature", "sha256=wrong");
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Notify_RejectsEmptyTitleOrZeroUserId()
    {
        var resp1 = await PostNotifyAsync(new { userId = 0L, title = "x" });
        var doc1 = await resp1.Content.ReadFromJsonAsync<JsonElement>();
        doc1.GetProperty("code").GetInt32().Should().NotBe(200);

        var resp2 = await PostNotifyAsync(new { userId = 1L, title = "" });
        var doc2 = await resp2.Content.ReadFromJsonAsync<JsonElement>();
        doc2.GetProperty("code").GetInt32().Should().NotBe(200);
    }

    [Fact]
    public async Task GetList_ReturnsCurrentUserNotifications_OrderedByCreatedAtDesc()
    {
        // 给 admin (UserId=1) 推送两条
        await PostNotifyAsync(new { userId = 1L, source = "umc", type = "notice", title = "通知 A" });
        await Task.Delay(20); // 确保 CreatedAt 不同
        await PostNotifyAsync(new { userId = 1L, source = "oa", type = "approval_result", title = "通知 B" });

        // admin 调列表
        var resp = await _client.GetAsync("/api/notifications?pageNum=1&pageSize=20");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("code").GetInt32().Should().Be(200);
        var rows = doc.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().BeGreaterThan(0);

        // 第一条应是最新（B），第二条是 A
        var first = rows[0];
        first.GetProperty("title").GetString().Should().Contain("通知");
    }

    [Fact]
    public async Task UnreadCount_ReturnsCurrentUserUnreadCount()
    {
        await PostNotifyAsync(new { userId = 1L, source = "umc", type = "notice", title = "未读测试" });

        var resp = await _client.GetAsync("/api/notifications/unread-count");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("code").GetInt32().Should().Be(200);
        doc.GetProperty("data").GetProperty("count").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MarkRead_SetsIsReadTrueAndReadAt()
    {
        var post = await PostNotifyAsync(new { userId = 1L, source = "wms", type = "stock_alert", title = "低库存预警" });
        var postDoc = await post.Content.ReadFromJsonAsync<JsonElement>();
        var id = postDoc.GetProperty("data").GetProperty("notificationId").GetInt64();

        var resp = await _client.PutAsync($"/api/notifications/{id}/read", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var n = db.UserNotifications.First(x => x.NotificationId == id);
        n.IsRead.Should().BeTrue();
        n.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAllRead_MarksAllCurrentUserNotificationsRead()
    {
        // 先给 admin 推 3 条
        await PostNotifyAsync(new { userId = 1L, source = "umc", type = "notice", title = "批量已读 1" });
        await PostNotifyAsync(new { userId = 1L, source = "umc", type = "notice", title = "批量已读 2" });
        await PostNotifyAsync(new { userId = 1L, source = "umc", type = "notice", title = "批量已读 3" });

        var resp = await _client.PutAsync("/api/notifications/read-all", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("code").GetInt32().Should().Be(200);

        // 之后 unread-count 应为 0
        var countResp = await _client.GetAsync("/api/notifications/unread-count");
        var countDoc = await countResp.Content.ReadFromJsonAsync<JsonElement>();
        countDoc.GetProperty("data").GetProperty("count").GetInt32().Should().Be(0);
    }
}
