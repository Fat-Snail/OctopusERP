using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.Persistence;
using System.Security.Claims;

namespace OctopusOA.Api.Controllers;

/// <summary>当前用户接口</summary>
[ApiController]
public class MeController : ControllerBase
{
    private readonly OaDbContext _db;
    public MeController(OaDbContext db) => _db = db;

    /// <summary>获取当前用户信息（从 JWT Claims 或回退到 Header）</summary>
    [HttpGet("/api/me")]
    [Authorize]
    public IActionResult Me()
    {
        var subject = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(subject) || !long.TryParse(subject, out var userId))
            return Unauthorized(new { code = 401, msg = "未登录或 Token 无效" });

        var user = _db.SyncUsers.FirstOrDefault(u => u.UmcUserId == userId);
        if (user == null)
        {
            var name = User.FindFirst("name")?.Value ?? User.Identity?.Name ?? "unknown";
            var email = User.FindFirst("email")?.Value ?? "";
            var roles = User.FindAll("role").Select(c => c.Value).ToList();

            user = new SyncUser
            {
                UmcUserId = userId,
                UserName = name,
                NickName = User.FindFirst("nickname")?.Value ?? name,
                Email = email,
                Status = 1,
                LastSyncAt = DateTime.UtcNow,
                OaRoles = roles,
            };
            _db.SyncUsers.Add(user);
            _db.SaveChanges();
        }

        return Ok(new { code = 200, msg = "ok", data = user });
    }
}
