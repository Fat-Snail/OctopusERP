using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Controllers;

/// <summary>当前用户接口</summary>
[ApiController]
public class MeController : ControllerBase
{
    private readonly PlmDbContext _db;

    public MeController(PlmDbContext db) => _db = db;

    /// <summary>获取当前用户信息（从 JWT Claims 解析）</summary>
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

            user = new Core.Entities.SyncUser
            {
                UmcUserId = userId,
                UserName = name,
                NickName = User.FindFirst("nickname")?.Value ?? name,
                Email = email,
                Status = 1,
                PlmRoles = "plm_user",
                LastSyncAt = DateTime.UtcNow,
            };
            _db.SyncUsers.Add(user);
            _db.SaveChanges();
        }

        return Ok(new { code = 200, msg = "ok", data = user });
    }
}
