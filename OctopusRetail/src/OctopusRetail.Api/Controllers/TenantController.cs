using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusRetail.Api.Dtos;
using OctopusRetail.Api.Multitenancy;
using OctopusRetail.Api.Services;

namespace OctopusRetail.Api.Controllers;

/// <summary>
/// 租户（门店）接口。
///
/// 自助建店是解决「引导第一个店长」的入口：
/// 任何已登录用户都可以建一家店并成为 owner，不必先由谁邀请、
/// 也不必手工写 SQL 或操作 Keycloak 组。
/// </summary>
[ApiController]
[Authorize]
[Route("api/retail/tenants")]
public class TenantController(
    TenantService tenants,
    ITenantContext tenant,
    CurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// 自助创建门店，调用者成为 owner。
    ///
    /// 跨租户端点：调用者此时通常还没有任何租户，无法提供 X-Tenant-Id。
    /// </summary>
    [HttpPost]
    [AllowCrossTenant]
    public async Task<ApiResponse<CreateTenantResponse>> Create(
        [FromBody] CreateTenantRequest req, CancellationToken ct)
    {
        var outcome = await tenants.CreateAsync(currentUser.Sub, new CreateTenantInput(
            req.TenantCode, req.TenantName, req.TenantType, req.ParentTenantId), ct);

        if (!outcome.Success)
            return ApiResponse<CreateTenantResponse>.Fail(outcome.Error ?? "创建失败", 400);

        return ApiResponse<CreateTenantResponse>.Success(new CreateTenantResponse
        {
            TenantId = outcome.TenantId!,
            TenantCode = outcome.TenantCode!,
            TenantName = outcome.TenantName!,
            Role = TenantAuthorization.RoleOwner,
            GroupSynced = outcome.GroupSynced,
            NextStep = outcome.GroupSynced
                ? "请重新获取 token（刷新页面或重新登录），之后即可切换到该组织"
                : "已创建，但 Keycloak 组同步待重试；稍后刷新 token 即可看到该组织",
        }, "已创建");
    }

    /// <summary>当前门店详情（租户内）</summary>
    [HttpGet("current")]
    public async Task<ApiResponse<object>> Current(CancellationToken ct)
    {
        var t = await tenants.GetCurrentAsync(tenant.TenantId!, ct);
        if (t is null) return ApiResponse<object>.Fail("组织不存在", 404);

        return ApiResponse<object>.Success(new
        {
            tenantId = t.TenantId,
            tenantCode = t.TenantCode,
            tenantName = t.TenantName,
            tenantType = t.TenantType,
            parentTenantId = t.ParentTenantId,
            status = t.Status,
            createdAt = t.CreatedAt,
        });
    }
}
