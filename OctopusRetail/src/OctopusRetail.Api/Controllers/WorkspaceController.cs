using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Dtos;
using OctopusRetail.Api.Multitenancy;
using OctopusRetail.Api.Persistence;
using OctopusRetail.Api.Services;

namespace OctopusRetail.Api.Controllers;

/// <summary>
/// Workspace（工作空间）接口 —— 对应 design §2「一人多租户的身份切换」。
///
/// 三个端点分别演示三件事：
///   /api/retail/me         跨租户：我是谁、我能进哪些租户、当前在哪个租户
///   /api/retail/workspaces 跨租户：列出我的全部 workspace（成员关系）
///   /api/retail/members    租户内：本租户成员（需合法 X-Tenant-Id）
///   /api/retail/profiles   租户内：业务表，隔离完全依赖全局过滤器（代码里不写 where）
/// </summary>
[ApiController]
[Authorize]
[Route("api/retail")]
public class WorkspaceController(
    RetailDbContext db,
    ITenantContext tenant,
    CurrentUser currentUser) : ControllerBase
{
    /// <summary>当前身份上下文：我是谁 / 可进入哪些租户 / 当前租户</summary>
    [HttpGet("me")]
    [AllowCrossTenant]
    public ApiResponse<object> Me() => ApiResponse<object>.Success(new
    {
        sub = currentUser.Sub,
        userName = currentUser.UserName,
        displayName = currentUser.DisplayName,
        availableTenants = tenant.AvailableTenants,
        activeTenant = tenant.TenantId,
    });

    /// <summary>列出我的全部 workspace（跨租户，仅限本人）</summary>
    [HttpGet("workspaces")]
    [AllowCrossTenant]
    public async Task<ApiResponse<object>> MyWorkspaces(CancellationToken ct)
    {
        var sub = currentUser.Sub;
        var memberships = await db.Memberships
            .Where(m => m.KcSub == sub && m.Status == "active")
            .ToListAsync(ct);

        var tenantIds = memberships.Select(m => m.TenantId).ToList();
        var tenants = await db.Tenants
            .Where(t => tenantIds.Contains(t.TenantId))
            .ToListAsync(ct);
        var byId = tenants.ToDictionary(t => t.TenantId, t => t);

        var items = memberships
            .Where(m => byId.ContainsKey(m.TenantId))
            .Select(m => new
            {
                tenantId = m.TenantId,
                tenantName = byId[m.TenantId].TenantName,
                tenantType = byId[m.TenantId].TenantType,
                role = m.RoleInTenant,
                joinedAt = m.JoinedAt,
            })
            .OrderBy(x => x.tenantName)
            .ToList();

        return ApiResponse<object>.Success(new { items, total = items.Count });
    }

    /// <summary>
    /// 本租户成员档案。
    /// 注意：查询里**没有任何** TenantId 条件——隔离完全来自全局过滤器。
    /// 这正是要验证的东西：漏写 where 也不会跨租户泄漏。
    /// </summary>
    [HttpGet("profiles")]
    public async Task<ApiResponse<object>> Profiles(CancellationToken ct)
    {
        var items = await db.MemberProfiles
            .OrderBy(p => p.DisplayName)
            .Select(p => new { p.ProfileId, p.TenantId, p.KcSub, p.DisplayName, p.Phone })
            .ToListAsync(ct);

        return ApiResponse<object>.Success(new { items, total = items.Count });
    }
}
