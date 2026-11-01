using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusRetail.Api.Dtos;
using OctopusRetail.Api.Multitenancy;
using OctopusRetail.Api.Services;

namespace OctopusRetail.Api.Controllers;

/// <summary>
/// 组织成员管理。
///
/// 这是「零售用户没有管理后台」的补齐：原来要禁用某人或重置密码
/// 只能去 Keycloak Admin Console 手工操作。
/// </summary>
[ApiController]
[Authorize]
[Route("api/retail/members")]
public class MemberController(
    MemberService members,
    ITenantContext tenant,
    CurrentUser currentUser) : ControllerBase
{
    /// <summary>本组织成员列表</summary>
    [HttpGet]
    public async Task<ApiResponse<object>> List(CancellationToken ct)
    {
        var items = (await members.ListAsync(ct)).Select(m => new MemberResponse
        {
            MembershipId = m.MembershipId,
            KcSub = m.KcSub,
            UserName = m.UserName,
            DisplayName = m.DisplayName,
            Phone = m.Phone,
            RoleInTenant = m.RoleInTenant,
            Status = m.Status,
            JoinedAt = m.JoinedAt,
            LeftAt = m.LeftAt,
            KcSyncState = m.KcSyncState,
        }).ToList();

        return ApiResponse<object>.Success(new
        {
            tenantId = tenant.TenantId,
            items,
            total = items.Count,
        });
    }

    /// <summary>停用/启用成员</summary>
    [HttpPost("{membershipId:long}/status")]
    public async Task<ApiResponse<object?>> SetStatus(
        long membershipId, [FromBody] SetMemberStatusRequest req, CancellationToken ct)
    {
        var r = await members.SetEnabledAsync(membershipId, req.Enabled, currentUser.Sub, ct);
        return r.Success
            ? ApiResponse<object?>.Success(null, req.Enabled ? "已启用" : "已停用")
            : ApiResponse<object?>.Fail(r.Error ?? "操作失败", 403);
    }

    /// <summary>移出组织</summary>
    [HttpPost("{membershipId:long}/remove")]
    public async Task<ApiResponse<object?>> Remove(long membershipId, CancellationToken ct)
    {
        var r = await members.RemoveAsync(membershipId, currentUser.Sub, ct);
        return r.Success
            ? ApiResponse<object?>.Success(null, "已移出组织")
            : ApiResponse<object?>.Fail(r.Error ?? "操作失败", 403);
    }

    /// <summary>重置成员密码</summary>
    [HttpPost("{membershipId:long}/reset-password")]
    public async Task<ApiResponse<object?>> ResetPassword(
        long membershipId, [FromBody] ResetMemberPasswordRequest req, CancellationToken ct)
    {
        var r = await members.ResetPasswordAsync(membershipId, req.NewPassword, currentUser.Sub, ct);
        return r.Success
            ? ApiResponse<object?>.Success(null, "密码已重置")
            : ApiResponse<object?>.Fail(r.Error ?? "操作失败", 400);
    }
}
