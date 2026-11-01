using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Api.Hubs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace OctopusUMC.Api.Controllers;

/// <summary>
/// P9 — 跨系统统一通知中心
/// 1. POST /api/internal/notify：各子系统内网推送（HMAC-SHA256 签名验证）
/// 2. GET  /api/notifications：当前用户通知列表（分页）
/// 3. GET  /api/notifications/unread-count：未读数（铃铛红点）
/// 4. PUT  /api/notifications/{id}/read：标记单条已读
/// 5. PUT  /api/notifications/read-all：全部标记已读
/// </summary>
[ApiController]
[Route("api")]
public class NotificationController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<OnlineUserHub> _hub;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        ApplicationDbContext db,
        IHubContext<OnlineUserHub> hub,
        IConfiguration config,
        ILogger<NotificationController> logger)
    {
        _db = db;
        _hub = hub;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 内网推送接口：各子系统业务事件（合同审批通过/库存预警/工单完工等）后调用。
    /// 必须携带 X-Notify-Signature: sha256=&lt;HMAC-SHA256(rawBody, NotifySecret)&gt;
    /// </summary>
    [HttpPost("internal/notify")]
    [AllowAnonymous]
    public async Task<IActionResult> Notify()
    {
        // 1) 读原始 body 做 HMAC 验签
        Request.EnableBuffering();
        Request.Body.Position = 0;
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var secret = _config["Notify:SharedSecret"] ?? "umc-notify-secret-dev";
        var sigHeader = Request.Headers["X-Notify-Signature"].FirstOrDefault() ?? "";
        var expected = "sha256=" + HmacSha256Hex(body, secret);
        if (!string.Equals(sigHeader, expected, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Internal notify signature mismatch");
            return Unauthorized();
        }

        var req = System.Text.Json.JsonSerializer.Deserialize<NotifyRequest>(body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        if (req == null || req.UserId <= 0 || string.IsNullOrEmpty(req.Title))
            return Ok(ApiResponse<object?>.Fail("payload 不合法"));

        // 2) 持久化通知
        var notification = new UserNotification
        {
            UserId = req.UserId,
            Source = req.Source ?? "umc",
            Type = req.Type ?? "notice",
            Title = req.Title,
            Message = req.Message ?? "",
            LinkUrl = req.LinkUrl,
            PayloadJson = req.PayloadJson,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        };
        _db.UserNotifications.Add(notification);
        await _db.SaveChangesAsync();

        // 3) 实时推送给在线用户（SignalR）
        await _hub.Clients.User(req.UserId.ToString()).SendAsync("Notification", new
        {
            id = notification.NotificationId,
            source = notification.Source,
            type = notification.Type,
            title = notification.Title,
            message = notification.Message,
            linkUrl = notification.LinkUrl,
            createdAt = notification.CreatedAt,
        });

        return Ok(ApiResponse<object>.Success(new { notificationId = notification.NotificationId }, "已推送"));
    }

    /// <summary>当前用户的通知列表（分页）</summary>
    [Authorize]
    [HttpGet("notifications")]
    public async Task<IActionResult> List(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] string? type = null)
    {
        var userId = GetUserId();
        if (userId <= 0) return Unauthorized();

        var q = _db.UserNotifications.Where(n => n.UserId == userId);
        if (unreadOnly == true) q = q.Where(n => !n.IsRead);
        if (!string.IsNullOrEmpty(type)) q = q.Where(n => n.Type == type);

        var total = await q.CountAsync();
        var rows = await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNum - 1) * pageSize).Take(pageSize)
            .Select(n => new
            {
                n.NotificationId,
                n.Source,
                n.Type,
                n.Title,
                n.Message,
                n.LinkUrl,
                n.IsRead,
                n.CreatedAt,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Success(new { rows, total }));
    }

    /// <summary>未读通知数（前端铃铛红点）</summary>
    [Authorize]
    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = GetUserId();
        if (userId <= 0) return Unauthorized();
        var count = await _db.UserNotifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
        return Ok(ApiResponse<object>.Success(new { count }));
    }

    /// <summary>标记单条已读</summary>
    [Authorize]
    [HttpPut("notifications/{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var userId = GetUserId();
        if (userId <= 0) return Unauthorized();
        var n = await _db.UserNotifications
            .FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == userId);
        if (n == null) return Ok(ApiResponse<object?>.Fail("通知不存在", 404));
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return Ok(ApiResponse<object?>.Success(null, "已标记已读"));
    }

    /// <summary>全部标记已读</summary>
    [Authorize]
    [HttpPut("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetUserId();
        if (userId <= 0) return Unauthorized();
        var now = DateTime.UtcNow;
        var unread = await _db.UserNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Success(new { count = unread.Count }, $"已标记 {unread.Count} 条已读"));
    }

    private long GetUserId() =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? User.FindFirst("sub")?.Value, out var u) ? u : 0;

    private static string HmacSha256Hex(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLower();
    }

    public class NotifyRequest
    {
        public long UserId { get; set; }
        public string? Source { get; set; }
        public string? Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? LinkUrl { get; set; }
        public string? PayloadJson { get; set; }
    }
}
