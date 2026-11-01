using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Persistence;

namespace OctopusRetail.Api.Services;

/// <summary>
/// 租户内授权判定。
///
/// 这是 design §4.3 的落地：Keycloak 只说「你是谁 + 你能进哪些租户」，
/// 「你在该租户里能做什么」一律由 retail_membership 判定，不放进 JWT。
///
/// 好处：改角色立即生效（不必等 token 过期，也不必强制刷新）。
/// </summary>
public class TenantAuthorization(RetailDbContext db)
{
    public const string RoleOwner = "owner";
    public const string RoleManager = "manager";
    public const string RoleStaff = "staff";
    public const string RoleCashier = "cashier";

    /// <summary>允许邀请成员的租户内角色</summary>
    public static readonly string[] InviteCapableRoles = { RoleOwner, RoleManager };

    /// <summary>取用户在某租户内的有效角色；非成员返回 null</summary>
    public Task<string?> GetRoleAsync(string kcSub, string tenantId, CancellationToken ct = default) =>
        db.Memberships
            .Where(m => m.KcSub == kcSub && m.TenantId == tenantId && m.Status == "active")
            .Select(m => m.RoleInTenant)
            .FirstOrDefaultAsync(ct)!;

    /// <summary>是否是该租户的有效成员</summary>
    public async Task<bool> IsMemberAsync(string kcSub, string tenantId, CancellationToken ct = default) =>
        await GetRoleAsync(kcSub, tenantId, ct) is not null;

    /// <summary>是否有权邀请成员（仅 owner / manager）</summary>
    public async Task<bool> CanInviteAsync(string kcSub, string tenantId, CancellationToken ct = default) =>
        await GetRoleAsync(kcSub, tenantId, ct) is { } role && InviteCapableRoles.Contains(role);
}
