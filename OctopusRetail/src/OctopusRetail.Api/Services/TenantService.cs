using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Persistence;

namespace OctopusRetail.Api.Services;

public record CreateTenantInput(
    string TenantCode,
    string TenantName,
    string TenantType,
    string? ParentTenantId);

public record CreateTenantOutcome(
    bool Success,
    string? TenantId = null,
    string? TenantName = null,
    string? TenantCode = null,
    string? Error = null,
    bool GroupSynced = false);

/// <summary>
/// 租户（门店）自助创建 —— 解决「引导第一个店长」的鸡生蛋问题。
///
/// 背景：邀请需要店长权限，店长权限来自成员关系，成员关系来自邀请，
/// 而真实环境的第一个成员关系没有任何人可以发出邀请。此前只能手工写 SQL +
/// 手动操作 Keycloak 组，极不可用。
///
/// 设计取舍：
/// - **自助创建**：任何已登录用户都可以建店并成为 owner。零售 SaaS 的标准做法
///   （Slack/Notion 式的「创建工作空间」），不需要预先存在平台管理员。
/// - **防滥用**：租户编码格式受限 + 全局唯一 + 单用户可拥有的租户数上限。
/// - **立即同步 group**：建店时同步把 owner 加入 Keycloak 组，
///   使其**下次登录**就带 /tenants/{id}（当前 token 不会自动更新，见 GetStartedHint）。
///
/// 注意：本方法必须在 [AllowCrossTenant] 端点上调用——调用者此时通常
/// 还没有任何租户，无法提供合法的 X-Tenant-Id。
/// </summary>
public partial class TenantService(
    RetailDbContext db,
    KeycloakAdminClient keycloak,
    TimeProvider clock,
    ILogger<TenantService> logger)
{
    /// <summary>单个用户最多可拥有的租户数（防止批量刷店）</summary>
    public const int MaxOwnedTenants = 10;

    /// <summary>租户编码：字母数字下划线连字符，2-32 位</summary>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{1,31}$")]
    private static partial Regex TenantCodePattern();

    /// <summary>
    /// 合法租户类型。
    /// 注意不要假设"租户 = 门店"：同一套邀请机制也被子公司/企业的销售用来邀请客户，
    /// 因此类型要覆盖企业(company)与子公司(subsidiary)。
    /// </summary>
    private static readonly string[] AllowedTypes =
        { "store", "chain", "franchise", "company", "subsidiary" };

    public async Task<CreateTenantOutcome> CreateAsync(
        string kcSub, CreateTenantInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kcSub))
            return new CreateTenantOutcome(false, Error: "缺少用户身份");

        var code = (input.TenantCode ?? string.Empty).Trim();
        if (!TenantCodePattern().IsMatch(code))
            return new CreateTenantOutcome(false,
                Error: "租户编码只能包含字母、数字、下划线、连字符，长度 2-32");

        var name = (input.TenantName ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > 64)
            return new CreateTenantOutcome(false, Error: "名称长度需在 1-64 之间");

        var type = string.IsNullOrWhiteSpace(input.TenantType) ? "store" : input.TenantType.Trim();
        if (!AllowedTypes.Contains(type))
            return new CreateTenantOutcome(false,
                Error: $"组织类型只能是 {string.Join(" / ", AllowedTypes)}");

        // 派生 group-safe 的 TenantId：t_{code 小写}
        var tenantId = $"t_{code.ToLowerInvariant()}";

        // 两处都要查：
        //  - TenantCode 是业务编码，必须全局唯一（大小写不敏感）
        //  - TenantId 是 Keycloak 组名，由 code 派生，也必须唯一
        // 只查 TenantId 会漏掉「同编码但历史数据派生出了不同 id」的情况
        // （种子里的 SH001 / t_shanghai_001 正是如此）。
        var codeLower = code.ToLowerInvariant();
        var clash = await db.Tenants
            .Where(t => t.TenantId == tenantId || t.TenantCode.ToLower() == codeLower)
            .Select(t => t.TenantId)
            .FirstOrDefaultAsync(ct);
        if (clash is not null)
            return new CreateTenantOutcome(false, Error: "该租户编码已被占用");

        if (!string.IsNullOrWhiteSpace(input.ParentTenantId)
            && !await db.Tenants.AnyAsync(t => t.TenantId == input.ParentTenantId, ct))
            return new CreateTenantOutcome(false, Error: "上级租户不存在");

        var owned = await db.Memberships
            .CountAsync(m => m.KcSub == kcSub
                             && m.RoleInTenant == TenantAuthorization.RoleOwner
                             && m.Status == "active", ct);
        if (owned >= MaxOwnedTenants)
            return new CreateTenantOutcome(false,
                Error: $"单个账号最多拥有 {MaxOwnedTenants} 个组织，已达到上限");

        var now = clock.GetUtcNow().UtcDateTime;

        db.Tenants.Add(new RetailTenant
        {
            TenantId = tenantId,
            TenantCode = code,
            TenantName = name,
            TenantType = type,
            ParentTenantId = string.IsNullOrWhiteSpace(input.ParentTenantId) ? null : input.ParentTenantId,
            Status = "active",
            CreatedAt = now,
        });

        var membership = new RetailMembership
        {
            KcSub = kcSub,
            TenantId = tenantId,
            RoleInTenant = TenantAuthorization.RoleOwner,
            Status = "active",
            JoinedAt = now,
            KcSyncState = MembershipSyncState.Pending,
        };
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(ct);

        // 立即推 Keycloak group：让创建者下次取 token 就带上 /tenants/{id}
        var synced = await TrySyncGroupAsync(membership, ct);

        logger.LogInformation(
            "租户已创建 TenantId={TenantId} Code={Code} 创建者={Sub} GroupSynced={Synced}",
            tenantId, code, kcSub, synced);

        return new CreateTenantOutcome(true, tenantId, name, code, null, synced);
    }

    /// <summary>把 owner 加进 Keycloak 组；失败不影响建店成功（对账器会重试）</summary>
    private async Task<bool> TrySyncGroupAsync(RetailMembership membership, CancellationToken ct)
    {
        if (!keycloak.Enabled) return false;

        var path = KeycloakAdminClient.TenantGroupPath(membership.TenantId);
        var groupId = await keycloak.EnsureGroupAsync(path, ct);
        var ok = groupId is not null
                 && await keycloak.AddUserToGroupAsync(membership.KcSub, path, ct);

        membership.KcSyncState = ok ? MembershipSyncState.Synced : MembershipSyncState.Failed;
        if (ok) membership.KcSyncedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return ok;
    }

    /// <summary>当前租户详情（租户内端点用）</summary>
    public Task<RetailTenant?> GetCurrentAsync(string tenantId, CancellationToken ct = default) =>
        db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
}
