using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/me")]
public class MeController(MesDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Me()
    {
        var sub = User.FindFirst("sub")?.Value;
        if (!long.TryParse(sub, out var userId))
            return Ok(new { code = 401, msg = "未认证", data = (object?)null });

        var syncUser = await db.SyncUsers.FirstOrDefaultAsync(u => u.UmcUserId == userId);
        if (syncUser is null)
        {
            syncUser = new MesSyncUser
            {
                UmcUserId = userId,
                UserName = User.FindFirst("name")?.Value ?? sub!,
                Email = User.FindFirst("email")?.Value,
                Status = 1,
                LastSyncAt = DateTime.UtcNow,
            };
            db.SyncUsers.Add(syncUser);
            await db.SaveChangesAsync();
        }

        return Ok(new
        {
            code = 200, msg = "ok",
            data = new { syncUser.UmcUserId, syncUser.UserName, syncUser.NickName, syncUser.Email, syncUser.Avatar, syncUser.Status }
        });
    }
}
