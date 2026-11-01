using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniappTodo.Api.Services;

namespace MiniappTodo.Api.Controllers;

/// <summary>当前用户上下文（从 JWT claims 提取 + Lazy Provisioning 本地 profile）</summary>
[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(UserContextService ctx) : ControllerBase
{
    /// <summary>
    /// 获取当前登录用户信息。
    /// 重点：完全不查 UMC，所有数据来自 Keycloak JWT claims + 本地 UserProfile 快照。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var info = ctx.Extract(User);
        var profile = await ctx.EnsureProfileAsync(User, ct);
        return Ok(new
        {
            sub = info.Sub,
            username = info.Username,
            name = info.Name,
            email = info.Email,
            octopusUserId = info.OctopusUserId,
            primaryDeptId = info.PrimaryDeptId,
            primaryDeptName = info.PrimaryDeptName,
            companyName = info.CompanyName,
            miniappScope = info.MiniappScope,
            roles = info.Roles,
            profile = new
            {
                profile.SnapshotName,
                profile.SnapshotDept,
                profile.SnapshotCompany,
                profile.LastLoginAt,
                profile.CreatedAt,
            }
        });
    }
}
