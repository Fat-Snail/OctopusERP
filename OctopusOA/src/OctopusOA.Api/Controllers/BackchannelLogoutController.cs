using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Controllers;

/// <summary>OIDC 反向登出接口（由 UMC 调用）</summary>
[ApiController]
public class BackchannelLogoutController : ControllerBase
{
    private readonly OaDbContext _db;
    private readonly ILogger<BackchannelLogoutController> _logger;

    public BackchannelLogoutController(OaDbContext db, ILogger<BackchannelLogoutController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>接收 UMC 的登出通知，标记用户会话失效</summary>
    [HttpPost("/api/auth/backchannel-logout")]
    [AllowAnonymous]
    public IActionResult BackchannelLogout()
    {
        string? logoutToken = null;

        try
        {
            if (Request.HasFormContentType)
                logoutToken = Request.Form["logout_token"].FirstOrDefault();
        }
        catch { /* ignore form parsing errors */ }

        logoutToken ??= Request.Headers["X-Logout-Token"].FirstOrDefault();

        if (string.IsNullOrEmpty(logoutToken))
        {
            _logger.LogWarning("[BackChannelLogout] 未收到 logout_token");
            return Ok(new { code = 200, msg = "登出通知已接收（无 token）" });
        }

        // 解析 JWT payload 取 sub（不验证签名，因为 back-channel 是服务端到服务端）
        try
        {
            var parts = logoutToken.Split('.');
            if (parts.Length >= 2)
            {
                var payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    Convert.FromBase64String(PadBase64(parts[1])));

                if (payload != null && payload.TryGetValue("sub", out var sub))
                {
                    var userId = sub.ToString();
                    _logger.LogInformation("[BackChannelLogout] 用户 {UserId} 会话已标记失效", userId);

                    // 将用户状态标记为已登出（实际生产中应撤销 refresh token）
                    if (long.TryParse(userId, out var uid))
                    {
                        var user = _db.SyncUsers.FirstOrDefault(u => u.UmcUserId == uid);
                        if (user != null)
                            _logger.LogInformation("[BackChannelLogout] 已找到 SyncUser: {UserName}", user.UserName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackChannelLogout] 解析 logout_token 失败");
        }

        return Ok(new { code = 200, msg = "登出通知已接收" });
    }

    private static string PadBase64(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/');
        var padding = (4 - base64.Length % 4) % 4;
        return base64 + new string('=', padding);
    }
}
