using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;

namespace OctopusCRM.Api.Controllers;

[ApiController]
[Route("api/me")]
[AllowAnonymous]
public class MeController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMe()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return Ok(new { code = 401, msg = "未登录", data = (object?)null });

        var sub = User.FindFirst("sub")?.Value;
        var userId = long.TryParse(sub, out var uid) ? uid : 0;

        var syncUser = await db.SyncUsers.FirstOrDefaultAsync(x => x.UmcUserId == userId);

        var crmRoles = new List<string>();
        if (syncUser != null)
        {
            // 根据业务逻辑动态赋予 CRM 角色
            // 实际项目可从数据库读取 CRM 角色表
            crmRoles.Add("crm_user");
        }

        return Ok(new
        {
            code = 200,
            msg = "ok",
            data = new
            {
                userId,
                userName = User.FindFirst("preferred_username")?.Value
                           ?? syncUser?.UserName ?? string.Empty,
                nickName = User.FindFirst("nickname")?.Value ?? syncUser?.NickName,
                email = User.FindFirst("email")?.Value ?? syncUser?.Email,
                crmRoles
            }
        });
    }
}
