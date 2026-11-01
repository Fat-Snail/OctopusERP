using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Multitenancy;
using OctopusRetail.Api.Persistence;

namespace OctopusRetail.Api.Services;

public record CreateInviteInput(
    string? Phone,
    string? InviteCode,
    string RoleInTenant,
    int MaxUses,
    TimeSpan ValidFor);

public record CreateInviteOutcome(
    bool Success,
    string? RawToken,
    RetailInvite? Invite,
    string? Error,
    string? ShortCode = null);

public record AcceptInviteOutcome(
    bool Success,
    string? TenantId = null,
    string? RoleInTenant = null,
    bool AlreadyMember = false,
    string? Error = null);

public record RegisterOutcome(
    bool Success,
    string? UserName = null,
    string? TenantId = null,
    string? Error = null);

/// <summary>
/// 邀请域服务（design §5）。
///
/// 关键安全性质（均有测试覆盖）：
/// - 明文 token 永不落库，只存 SHA-256
/// - 邀请与租户强绑定，A 店的邀请不能用于进入 B 店
/// - 有效期 + 最大使用次数，超限即失效，状态机终态不可回流
/// - 创建邀请必须由该租户的 owner/manager 发起（TenantAuthorization 判定）
///
/// 接受邀请是唯一跨租户路径：被邀请人此时还不是成员，token 里没有对应
/// /tenants/* 组。因此只有该方法使用 IgnoreQueryFilters，且立即按 TokenHash 精确定位。
/// </summary>
public class InviteService(
    RetailDbContext db,
    TenantAuthorization auth,
    ITenantContext tenant,
    KeycloakAdminClient keycloak,
    UmcUserClient umc,
    TimeProvider clock)
{
    private const int MaxUsesLimit = 1000;
    private const int MinPasswordLength = 8;

    /// <summary>创建邀请（必须在已解析租户的请求上下文中调用）</summary>
    public async Task<CreateInviteOutcome> CreateAsync(
        string inviterSub, CreateInviteInput input, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            return new CreateInviteOutcome(false, null, null, "缺少租户上下文");

        if (!await auth.CanInviteAsync(inviterSub, tenantId, ct))
            return new CreateInviteOutcome(false, null, null, "无权邀请成员：需要店长或店主角色");

        if (string.IsNullOrWhiteSpace(input.Phone) && string.IsNullOrWhiteSpace(input.InviteCode))
            return new CreateInviteOutcome(false, null, null, "必须提供手机号或通用邀请码");

        if (input.MaxUses < 1 || input.MaxUses > MaxUsesLimit)
            return new CreateInviteOutcome(false, null, null, $"使用次数必须在 1-{MaxUsesLimit} 之间");

        if (input.ValidFor <= TimeSpan.Zero)
            return new CreateInviteOutcome(false, null, null, "有效期必须为正");

        var rawToken = InviteToken.Generate();
        var shortCode = InviteToken.GenerateShortCode();
        var now = clock.GetUtcNow().UtcDateTime;

        var invite = new RetailInvite
        {
            TenantId = tenantId,
            Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim(),
            InviteCode = string.IsNullOrWhiteSpace(input.InviteCode) ? null : input.InviteCode.Trim(),
            TokenHash = InviteToken.Hash(rawToken),   // ★ 只存哈希
            ShortCodeHash = InviteToken.Hash(shortCode),
            RoleInTenant = input.RoleInTenant,
            MaxUses = input.MaxUses,
            UsedCount = 0,
            State = InviteState.Pending,
            ExpiresAt = now.Add(input.ValidFor),
            InvitedBySub = inviterSub,
            CreatedAt = now,
        };

        db.Invites.Add(invite);
        await db.SaveChangesAsync(ct);

        // 明文 token / 短码仅此一次返回
        return new CreateInviteOutcome(true, rawToken, invite, null, shortCode);
    }

    /// <summary>列出当前租户的邀请（带租户过滤器，A 店看不到 B 店）</summary>
    public Task<List<RetailInvite>> ListAsync(CancellationToken ct = default) =>
        db.Invites.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

    /// <summary>撤销邀请（仅 pending 可撤销，终态不可回流）</summary>
    public async Task<bool> RevokeAsync(long inviteId, string operatorSub, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId;
        if (string.IsNullOrEmpty(tenantId)) return false;
        if (!await auth.CanInviteAsync(operatorSub, tenantId, ct)) return false;

        var invite = await db.Invites.FirstOrDefaultAsync(i => i.InviteId == inviteId, ct);
        if (invite is null || invite.State != InviteState.Pending) return false;

        invite.State = InviteState.Revoked;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>按明文 token 精确查找（跨租户，接受邀请专用）</summary>
    public Task<RetailInvite?> FindByTokenAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = InviteToken.Hash(rawToken);
        return db.Invites.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
    }

    /// <summary>按短码查找（URL /i/{code} 用的就是这条）</summary>
    public Task<RetailInvite?> FindByShortCodeAsync(string shortCode, CancellationToken ct = default)
    {
        var hash = InviteToken.Hash(shortCode.Trim());
        return db.Invites.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.ShortCodeHash == hash, ct);
    }

    /// <summary>
    /// 统一凭据解析：短码与长 token 都接受。
    /// 短码是 8 位、来自受限字符表；其余一律当长 token 处理。
    /// </summary>
    public Task<RetailInvite?> FindByCredentialAsync(string credential, CancellationToken ct = default)
    {
        var value = (credential ?? string.Empty).Trim();
        if (value.Length == InviteToken.ShortCodeLength)
            return FindByShortCodeAsync(value, ct);
        return FindByTokenAsync(value, ct);
    }

    /// <summary>接受邀请：校验状态机 → 建立成员关系 → 消耗一次使用次数</summary>
    public async Task<AcceptInviteOutcome> AcceptAsync(
        string rawToken, string kcSub, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return new AcceptInviteOutcome(false, Error: "邀请令牌不能为空");

        // 唯一跨租户读取：被邀请人此时还不是成员，token 里没有该租户。
        // 支持长 token 与短码两种凭据。
        var invite = await FindByCredentialAsync(rawToken, ct);
        if (invite is null)
            return new AcceptInviteOutcome(false, Error: "邀请不存在或已失效");

        var now = clock.GetUtcNow().UtcDateTime;

        // 已是有效成员：幂等成功。
        // 成员关系才是事实来源——同一人重复点开已用掉的一次性链接，应得到「你已在店内」，
        // 而不是「邀请已被使用」。因此本判定必须早于状态机与额度检查。
        var existingRole = await auth.GetRoleAsync(kcSub, invite.TenantId, ct);
        if (existingRole is not null)
            return new AcceptInviteOutcome(true, invite.TenantId, existingRole, AlreadyMember: true);

        var rejection = await ValidateUsableAsync(invite, now, ct);
        if (rejection is not null)
            return new AcceptInviteOutcome(false, Error: rejection);

        return await GrantMembershipAsync(invite, kcSub, now, ct);
    }

    /// <summary>
    /// 受控自助建号 + 入店（design §5.2 的落地）。
    ///
    /// 这是「邀请即建号」的入口：邀请令牌本身是强凭据（经短信/企微送出），
    /// 被邀请人在注册页自行设定密码，由本服务用 retail-invite-bot 服务账号建号，
    /// 因此**不需要**打开 realm 的 registrationAllowed（那会让任何人都能注册进平台）。
    ///
    /// 建号成功后立即建立成员关系并把状态置未同步，由 GroupSyncReconciler 推进 Keycloak group。
    /// </summary>
    public async Task<RegisterOutcome> RegisterAsync(
        string rawToken,
        string userName,
        string displayName,
        string? phone,
        string password,
        string? email = null,
        CancellationToken ct = default)
    {
        if (!keycloak.Enabled)
            return new RegisterOutcome(false, Error: "当前环境未启用 Keycloak，无法自助建号，请联系店长代为开通");

        if (string.IsNullOrWhiteSpace(userName) || userName.Trim().Length < 3)
            return new RegisterOutcome(false, Error: "用户名至少 3 个字符");

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return new RegisterOutcome(false, Error: $"密码至少 {MinPasswordLength} 位");

        var invite = await FindByCredentialAsync(rawToken, ct);
        if (invite is null)
            return new RegisterOutcome(false, Error: "邀请不存在或已失效");

        // 定向邀请必须手机号匹配，防止链接被转发后他人冒用
        if (!string.IsNullOrWhiteSpace(invite.Phone)
            && NormalizePhone(invite.Phone) != NormalizePhone(phone))
            return new RegisterOutcome(false, Error: "手机号与邀请不匹配");

        var now = clock.GetUtcNow().UtcDateTime;
        var rejection = await ValidateUsableAsync(invite, now, ct);
        if (rejection is not null)
            return new RegisterOutcome(false, Error: rejection);

        var normalizedUser = userName.Trim();

        // ADR-010：账号在 UMC 建（UMC 是唯一用户主数据源），由 UMC 同步到 Keycloak。
        // 这样运营在 5173 用户管理里能看到这个人，也能停用/改密；
        // 原来直接调 Keycloak 建号会导致 UMC 完全不知道这个人存在。
        var created = await umc.CreateRetailUserAsync(
            normalizedUser,
            string.IsNullOrWhiteSpace(displayName) ? normalizedUser : displayName.Trim(),
            phone,
            string.IsNullOrWhiteSpace(email) ? $"{normalizedUser}@retail.placeholder" : email.Trim(),
            password,
            ct);

        if (!created.Success || string.IsNullOrEmpty(created.KcSub))
            return new RegisterOutcome(false,
                Error: created.Error ?? "建号失败：请稍后重试（详见服务端日志）");

        var kcSub = created.KcSub;

        var granted = await GrantMembershipAsync(
            invite, kcSub, now, ct, normalizedUser, displayName, phone);
        return granted.Success
            ? new RegisterOutcome(true, normalizedUser, invite.TenantId)
            : new RegisterOutcome(false, Error: granted.Error ?? "入店失败");
    }

    // ─── 内部：状态校验与成员关系授予 ───────────────────────

    /// <summary>返回拒绝原因；null 表示可用</summary>
    private async Task<string?> ValidateUsableAsync(RetailInvite invite, DateTime now, CancellationToken ct)
    {
        if (invite.State == InviteState.Revoked)
            return "邀请已被撤销";

        if (invite.State == InviteState.Expired || invite.ExpiresAt <= now)
        {
            if (invite.State == InviteState.Pending)
            {
                invite.State = InviteState.Expired;
                await db.SaveChangesAsync(ct);
            }
            return "邀请已过期";
        }

        if (invite.State != InviteState.Pending || invite.UsedCount >= invite.MaxUses)
            return "邀请已被使用";

        return null;
    }

    /// <summary>建立成员关系并消耗一次使用额度</summary>
    private async Task<AcceptInviteOutcome> GrantMembershipAsync(
        RetailInvite invite,
        string kcSub,
        DateTime now,
        CancellationToken ct,
        string? userName = null,
        string? displayName = null,
        string? phone = null)
    {
        // 快照缺失时向 Keycloak 取一次（成员列表要展示用户名/姓名）
        if (userName is null && keycloak.Enabled)
        {
            var info = await keycloak.TryGetUserAsync(kcSub, ct);
            if (info is not null)
            {
                userName = info.Value.UserName;
                displayName ??= info.Value.DisplayName;
            }
        }

        var membership = new RetailMembership
        {
            KcSub = kcSub,
            UserName = userName,
            DisplayName = displayName,
            Phone = phone,
            TenantId = invite.TenantId,
            RoleInTenant = invite.RoleInTenant,
            Status = "active",
            JoinedAt = now,
            KcSyncState = MembershipSyncState.Pending,
        };
        db.Memberships.Add(membership);

        invite.UsedCount++;
        invite.AcceptedBySub = kcSub;
        if (invite.UsedCount >= invite.MaxUses)
            invite.State = InviteState.Accepted;

        await db.SaveChangesAsync(ct);

        // ★ 立即推 Keycloak 组，而不是等后台对账（最多 60s）。
        // 原因：入店后用户马上就要登录，而 token 的 groups claim 是登录时从 Keycloak
        // 取的。若此刻没入组，用户登录后虽然 workspace 里能看到该组织（走 DB），
        // 但任何租户内接口都会 403——表现为"加入成功却什么都打不开"。
        // 建店路径（TenantService）本来就是立即推送，这里对齐。
        await TrySyncGroupAsync(membership, ct);

        return new AcceptInviteOutcome(true, invite.TenantId, invite.RoleInTenant);
    }

    /// <summary>把成员加入 Keycloak 组；失败不阻塞入店（对账器会重试）</summary>
    private async Task TrySyncGroupAsync(
        RetailMembership membership, CancellationToken ct)
    {
        if (!keycloak.Enabled) return;

        var path = KeycloakAdminClient.TenantGroupPath(membership.TenantId);
        var groupId = await keycloak.EnsureGroupAsync(path, ct);
        var ok = groupId is not null
                 && await keycloak.AddUserToGroupAsync(membership.KcSub, path, ct);

        membership.KcSyncState = ok ? MembershipSyncState.Synced : MembershipSyncState.Failed;
        if (ok) membership.KcSyncedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 手机号归一化：只比较数字，并容忍 +86 / 0086 国家码前缀。
    /// 否则「+86 139-0000-1111」与「13900001111」会被判为不匹配，误拒合法的定向邀请。
    /// </summary>
    private static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0086", StringComparison.Ordinal)) return digits[4..];
        if (digits.StartsWith("86", StringComparison.Ordinal) && digits.Length > 11) return digits[2..];
        return digits;
    }

    /// <summary>
    /// 把已过期但仍标记为 pending 的邀请置为 expired。
    /// 后台维护任务调用——无租户上下文，故用 IgnoreQueryFilters 扫描全量。
    /// </summary>
    public async Task<int> ExpireOverdueAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var overdue = await db.Invites.IgnoreQueryFilters()
            .Where(i => i.State == InviteState.Pending && i.ExpiresAt <= now)
            .ToListAsync(ct);

        foreach (var i in overdue) i.State = InviteState.Expired;
        if (overdue.Count > 0) await db.SaveChangesAsync(ct);
        return overdue.Count;
    }
}
