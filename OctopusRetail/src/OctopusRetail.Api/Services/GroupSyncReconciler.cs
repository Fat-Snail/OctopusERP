using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Persistence;

namespace OctopusRetail.Api.Services;

public record GroupSyncResult(int Scanned, int Synced, int Failed, bool Skipped);

/// <summary>
/// Keycloak group 双写对账器（design §5.3）。
///
/// B 方案必付的复杂度：成员关系同时存在于业务库（retail_membership）与 Keycloak group。
/// 两者不可能靠分布式事务保证一致，只能靠**幂等 + 对账**兜底：
///   - 业务库是事实来源
///   - 每次对账扫描 KcSyncState ∈ {null, pending, failed} 的成员关系并重放
///   - 成功置 synced + KcSyncedAt，失败置 failed 留待下轮重试
///
/// 状态语义：
///   active  → 确保 group 存在 + 用户加入该 group
///   其他    → 用户移出该 group（离职/停用/调店）
/// </summary>
public class GroupSyncReconciler(
    RetailDbContext db,
    KeycloakAdminClient keycloak,
    ILogger<GroupSyncReconciler> logger,
    TimeProvider clock)
{
    /// <summary>单轮最多处理多少条，避免一次性打爆 Keycloak</summary>
    public const int BatchSize = 200;

    public async Task<GroupSyncResult> ReconcileAsync(CancellationToken ct = default)
    {
        if (!keycloak.Enabled)
            return new GroupSyncResult(0, 0, 0, Skipped: true);

        // 无租户上下文的后台任务：必须显式 IgnoreQueryFilters 才能扫到全部租户
        var candidates = await db.Memberships.IgnoreQueryFilters()
            // 只处理 pending / failed / null；Unmanaged（演示占位 sub）与 Synced 跳过
            .Where(m => m.KcSyncState == null
                        || m.KcSyncState == MembershipSyncState.Pending
                        || m.KcSyncState == MembershipSyncState.Failed)
            .OrderBy(m => m.MembershipId)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return new GroupSyncResult(0, 0, 0, Skipped: false);

        var now = clock.GetUtcNow().UtcDateTime;
        var synced = 0;
        var failed = 0;

        foreach (var membership in candidates)
        {
            var path = KeycloakAdminClient.TenantGroupPath(membership.TenantId);
            bool ok;

            if (membership.Status == "active")
            {
                var groupId = await keycloak.EnsureGroupAsync(path, ct);
                ok = groupId is not null
                     && await keycloak.AddUserToGroupAsync(membership.KcSub, path, ct);
            }
            else
            {
                ok = await keycloak.RemoveUserFromGroupAsync(membership.KcSub, path, ct);
            }

            membership.KcSyncState = ok ? MembershipSyncState.Synced : MembershipSyncState.Failed;
            if (ok)
            {
                membership.KcSyncedAt = now;
                synced++;
            }
            else
            {
                failed++;
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Group 对账完成 Scanned={Scanned} Synced={Synced} Failed={Failed}",
            candidates.Count, synced, failed);

        return new GroupSyncResult(candidates.Count, synced, failed, Skipped: false);
    }
}
