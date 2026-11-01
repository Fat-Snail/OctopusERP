using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Multitenancy;
using OctopusRetail.Api.Persistence;

namespace OctopusRetail.Api.Services;

public record MemberResult(bool Success, bool KeycloakSynced = false, string? Error = null);

/// <summary>
/// 组织成员管理。
///
/// 存在的意义：零售域的用户主数据在 Keycloak，不在 UMC（ADR-008）。
/// 如果不提供这个入口，禁用某人 / 重置密码 / 移出组织只能去 Keycloak Admin Console 手工操作，
/// 而这恰恰是「零售域没有管理后台」这个缺口的正解。
///
/// 权限模型（关键）：
///   - 只有 owner / manager 能管理成员
///   - **不能操作同级或更高级的人**（manager 管不了 owner 和别的 manager）
///   - 不能把组织里最后一个 owner 移除/停用（否则组织变成无主，谁都管不了）
/// </summary>
public class MemberService(
    RetailDbContext db,
    TenantAuthorization auth,
    KeycloakAdminClient keycloak,
    ITenantContext tenant,
    TimeProvider clock,
    ILogger<MemberService> logger)
{
    /// <summary>角色等级：数字越大权限越高</summary>
    private static int RankOf(string? role) => role switch
    {
        TenantAuthorization.RoleOwner => 3,
        TenantAuthorization.RoleManager => 2,
        _ => 1,
    };

    private const int MinManageRank = 2;   // manager 及以上才能管理成员

    private string TenantId => tenant.TenantId
        ?? throw new InvalidOperationException("缺少租户上下文");

    /// <summary>单次列表最多补多少个缺失快照，避免拖慢接口</summary>
    private const int MaxSnapshotBackfill = 20;

    public async Task<List<RetailMembership>> ListAsync(CancellationToken ct = default)
    {
        var list = await db.Memberships
            .Where(m => m.TenantId == TenantId)
            .OrderByDescending(m => m.Status == "active")
            .ThenByDescending(m => m.RoleInTenant == TenantAuthorization.RoleOwner)
            .ThenByDescending(m => m.RoleInTenant == TenantAuthorization.RoleManager)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync(ct);

        await BackfillSnapshotsAsync(list, ct);
        return list;
    }

    /// <summary>
    /// 自愈：早于「快照字段」加入的成员 user_name/display_name 为空，
    /// 这里向 Keycloak 补取一次并写回，让列表能显示名字而不是一串 sub。
    /// 演示用的占位 sub（kc-xxx）在 Keycloak 中不存在，直接跳过。
    /// </summary>
    private async Task BackfillSnapshotsAsync(List<RetailMembership> list, CancellationToken ct)
    {
        if (!keycloak.Enabled) return;

        var missing = list
            .Where(m => string.IsNullOrEmpty(m.UserName) && !m.KcSub.StartsWith("kc-", StringComparison.Ordinal))
            .Take(MaxSnapshotBackfill)
            .ToList();

        if (missing.Count == 0) return;

        var changed = false;
        foreach (var m in missing)
        {
            var info = await keycloak.TryGetUserAsync(m.KcSub, ct);
            if (info is null) continue;
            m.UserName = info.Value.UserName;
            m.DisplayName ??= info.Value.DisplayName;
            changed = true;
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    /// <summary>启用/停用成员</summary>
    public async Task<MemberResult> SetEnabledAsync(
        long membershipId, bool enabled, string operatorSub, CancellationToken ct = default)
    {
        var guard = await AuthorizeActionAsync(membershipId, operatorSub, ct);
        if (guard.Error is not null) return new MemberResult(false, Error: guard.Error);
        var target = guard.Target!;

        if (!enabled && await WouldLoseLastOwnerAsync(target, ct))
            return new MemberResult(false, Error: "不能停用组织内最后一个所有者，请先指定新的所有者");

        target.Status = enabled ? "active" : "suspended";
        target.KcSyncState = MembershipSyncState.Pending;   // 状态变了要重新对账 group
        await db.SaveChangesAsync(ct);

        // enabled 状态不由对账器管，这里直接下发
        var synced = await keycloak.SetUserEnabledAsync(target.KcSub, enabled, ct);

        logger.LogInformation("成员状态变更 Tenant={TenantId} Sub={Sub} Enabled={Enabled} Kc={Kc}",
            TenantId, target.KcSub, enabled, synced);

        return synced || !keycloak.Enabled
            ? new MemberResult(true, synced)
            : new MemberResult(true, false, "本地已更新，但 Keycloak 未同步（用户可能仍可登录），请稍后重试");
    }

    /// <summary>移出组织（保留账号，仅解除成员关系）</summary>
    public async Task<MemberResult> RemoveAsync(
        long membershipId, string operatorSub, CancellationToken ct = default)
    {
        var guard = await AuthorizeActionAsync(membershipId, operatorSub, ct);
        if (guard.Error is not null) return new MemberResult(false, Error: guard.Error);
        var target = guard.Target!;

        if (await WouldLoseLastOwnerAsync(target, ct))
            return new MemberResult(false, Error: "不能移除组织内最后一个所有者，请先指定新的所有者");

        target.Status = "left";
        target.LeftAt = clock.GetUtcNow().UtcDateTime;
        target.KcSyncState = MembershipSyncState.Pending;   // 对账器会把它移出 Keycloak 组
        await db.SaveChangesAsync(ct);

        logger.LogInformation("成员移出组织 Tenant={TenantId} Sub={Sub} 操作人={Operator}",
            TenantId, target.KcSub, operatorSub);

        return new MemberResult(true, false);
    }

    /// <summary>重置成员密码</summary>
    public async Task<MemberResult> ResetPasswordAsync(
        long membershipId, string newPassword, string operatorSub, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            return new MemberResult(false, Error: "密码至少 8 位");

        var guard = await AuthorizeActionAsync(membershipId, operatorSub, ct);
        if (guard.Error is not null) return new MemberResult(false, Error: guard.Error);
        var target = guard.Target!;

        if (!keycloak.Enabled)
            return new MemberResult(false, Error: "当前环境未启用 Keycloak，无法重置密码");

        var ok = await keycloak.SetPasswordAsync(target.KcSub, newPassword, temporary: false, ct);
        logger.LogInformation("重置成员密码 Tenant={TenantId} Sub={Sub} 操作人={Operator} 成功={Ok}",
            TenantId, target.KcSub, operatorSub, ok);

        return ok
            ? new MemberResult(true, true)
            : new MemberResult(false, Error: "Keycloak 重置失败，请稍后重试");
    }

    // ─── 授权与不变量 ───────────────────────────────────

    private sealed record GuardResult(RetailMembership? Target, string? Error);

    private async Task<GuardResult> AuthorizeActionAsync(
        long membershipId, string operatorSub, CancellationToken ct)
    {
        var myRole = await auth.GetRoleAsync(operatorSub, TenantId, ct);
        if (RankOf(myRole) < MinManageRank)
            return new GuardResult(null, "无权管理成员：需要管理员或所有者");

        var target = await db.Memberships
            .FirstOrDefaultAsync(m => m.MembershipId == membershipId && m.TenantId == TenantId, ct);
        if (target is null)
            return new GuardResult(null, "成员不存在或不属于当前组织");

        if (target.KcSub == operatorSub)
            return new GuardResult(null, "不能对自己执行该操作");

        // 不能操作同级或更高权限的人
        if (RankOf(target.RoleInTenant) >= RankOf(myRole))
            return new GuardResult(null, "不能操作同级或权限更高的成员");

        return new GuardResult(target, null);
    }

    /// <summary>目标是否为组织里最后一个有效的 owner</summary>
    private async Task<bool> WouldLoseLastOwnerAsync(
        RetailMembership target, CancellationToken ct)
    {
        if (target.RoleInTenant != TenantAuthorization.RoleOwner) return false;

        var activeOwners = await db.Memberships
            .CountAsync(m => m.TenantId == TenantId
                             && m.RoleInTenant == TenantAuthorization.RoleOwner
                             && m.Status == "active", ct);

        return activeOwners <= 1;
    }
}
