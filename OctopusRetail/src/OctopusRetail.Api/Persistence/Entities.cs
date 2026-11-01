namespace OctopusRetail.Api.Persistence;

/// <summary>
/// 租户（门店 / 连锁主体 / 加盟商）。
///
/// 生命周期与 UMC 部门解耦：关店、并店、加盟商变更 ≠ 组织架构调整。
/// TenantId 与 Keycloak Group 名对齐（group 路径 /tenants/{TenantId}），
/// 是 token 的 groups claim 与业务库之间的唯一契约。
/// </summary>
public class RetailTenant
{
    /// <summary>租户标识（= Keycloak group 名，如 t_shanghai_001）</summary>
    public string TenantId { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;

    /// <summary>store | chain | franchise</summary>
    public string TenantType { get; set; } = "store";

    /// <summary>上级租户（加盟店 → 连锁主体）；仅供展示与统计，不参与权限判定</summary>
    public string? ParentTenantId { get; set; }

    /// <summary>溯源用：UMC 中对应的公司部门 ID。刻意不做外键——零售域不依赖 UMC 表结构</summary>
    public long? UmcCompanyDeptId { get; set; }

    /// <summary>active | suspended | closed</summary>
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 成员关系 —— 「workspace」的持久化形态：user × tenant + 该租户内的角色。
///
/// 为什么不把角色放进 JWT：Keycloak 的 realm role 是全局的、client role 是 per-client，
/// 都无法表达「张三在 A 店是店长、在 B 店是店员」。因此 Keycloak 只回答
/// 「你是谁 + 你能进哪些租户」，租户内角色一律由本表判定
/// （见 design/retail-tenant-workspace.md §4.3）。
///
/// 本表属于「控制面」：不带全局租户过滤器——必须能跨租户读取以列出我的 workspace。
/// 所有查询都强制约束 KcSub。
/// </summary>
public class RetailMembership
{
    public long MembershipId { get; set; }
    /// <summary>Keycloak sub（UUID）。零售域引用用户的唯一外键，不存 UMC UserId</summary>
    public string KcSub { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    /// <summary>owner | manager | staff | cashier</summary>
    public string RoleInTenant { get; set; } = "staff";

    /// <summary>active | suspended | left</summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// 加入时的用户名快照（仅用于成员列表展示）。
    /// 零售域不主动查 UMC，也不给每个成员发 Keycloak 请求，所以存一份快照；
    /// 权威值仍在 Keycloak，这里允许过期。
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>加入时的展示名快照</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 加入时的手机号快照。
    /// 旧实现只在注册时用它校验邀请，随后丢弃，导致事后无法追溯
    /// （实测：feiwoniu001 明明填了手机号，UMC 与零售库都查不到）。
    /// </summary>
    public string? Phone { get; set; }

    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }

    /// <summary>Keycloak group 双写对账状态（见 design §5.3）</summary>
    public DateTime? KcSyncedAt { get; set; }
    public string? KcSyncState { get; set; }
}

/// <summary>
/// 租户隔离标记。任何实现此接口的实体自动获得 fail-closed 全局查询过滤器：
/// 未解析租户时匹配空集，而不是全集。
///
/// 新增业务表请一律实现本接口——这是防止跨租户泄漏的第一道结构性保障。
/// </summary>
public interface ITenantScoped
{
    string TenantId { get; set; }
}

/// <summary>
/// 租户内的成员档案快照（示范用的业务表，代表所有未来的零售业务表）。
/// 刻意不在查询里手写 TenantId 条件——隔离应来自全局过滤器，而非每个开发者记得加 where。
/// </summary>
public class RetailMemberProfile : ITenantScoped
{
    public long ProfileId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string KcSub { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 成员关系与 Keycloak group 的双写对账状态。
///
/// 放在 Persistence 而非 Services：Seeder / 业务服务 / 对账器都要用它，
/// 让 Persistence 反过来依赖 Services 会造成分层倒挂。
/// </summary>
public static class MembershipSyncState
{
    /// <summary>待同步（新建成员关系的初始状态）</summary>
    public const string Pending = "pending";

    /// <summary>已同步到 Keycloak group</summary>
    public const string Synced = "synced";

    /// <summary>同步失败，对账器会重试</summary>
    public const string Failed = "failed";

    /// <summary>
    /// 不受对账管理。用于演示/种子数据里**不对应真实 Keycloak 用户**的占位 sub
    /// （如 "kc-zhangsan"）。若不排除，对账器会每轮为它们发起请求并永久失败，
    /// 日志被无意义的重试刷屏。
    /// </summary>
    public const string Unmanaged = "unmanaged";

    /// <summary>对账器需要处理的状态集合</summary>
    public static bool NeedsSync(string? state) =>
        state is null || state == Pending || state == Failed;
}

/// <summary>邀请状态机：pending → accepted | expired | revoked（终态不可再迁移）</summary>
public static class InviteState
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Expired = "expired";
    public const string Revoked = "revoked";
}

/// <summary>
/// 店铺邀请。
///
/// 安全设计（design §5）：
/// - **只存 token 的 SHA-256 哈希**，明文 token 仅出现在发给被邀请人的链接里
/// - 邀请绑定租户：A 店的邀请无法用于进入 B 店
/// - 有有效期与最大使用次数，超限即失效
///
/// 实现 ITenantScoped（默认带租户过滤器，忘记过滤的后果是查不到而非泄漏）。
/// 接受邀请是唯一需要跨租户的场景——被邀请人此时还不是成员，token 里没有对应
/// /tenants/* 组，因此该路径显式使用 IgnoreQueryFilters()（见 InviteService）。
/// </summary>
public class RetailInvite : ITenantScoped
{
    public long InviteId { get; set; }
    public string TenantId { get; set; } = string.Empty;

    /// <summary>定向邀请的手机号（与 InviteCode 二选一）</summary>
    public string? Phone { get; set; }

    /// <summary>通用邀请码（与 Phone 二选一）</summary>
    public string? InviteCode { get; set; }

    /// <summary>明文 token 的 SHA-256（hex）。明文永不落库</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// 短码的 SHA-256（hex）。明文只出现在短链接 /i/{code} 里，同样不落库。
    ///
    /// 为什么用「随机短码」而不是「加密的 ID」：
    /// 加密（AES/Hashids 之类）是确定性映射，任何人拿到一个码就能反解/枚举出
    /// 其他邀请，等于把凭据变成了公开的可推导值；且需要管理密钥。
    /// 8 位随机码（57 字符表 ≈ 1.1e14 组合）+ 有效期 + 使用次数上限，
    /// 安全强度足够，且无需密钥、无状态可推导。
    /// </summary>
    public string? ShortCodeHash { get; set; }

    /// <summary>入店后授予的租户内角色</summary>
    public string RoleInTenant { get; set; } = "staff";

    public int MaxUses { get; set; } = 1;
    public int UsedCount { get; set; }

    public string State { get; set; } = InviteState.Pending;
    public DateTime ExpiresAt { get; set; }

    public string InvitedBySub { get; set; } = string.Empty;
    public string? AcceptedBySub { get; set; }
    public DateTime CreatedAt { get; set; }
}
