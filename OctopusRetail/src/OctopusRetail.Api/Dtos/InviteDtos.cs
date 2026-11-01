namespace OctopusRetail.Api.Dtos;

/// <summary>创建邀请请求</summary>
public class CreateInviteRequest
{
    /// <summary>定向邀请的手机号（与 InviteCode 二选一）</summary>
    public string? Phone { get; set; }

    /// <summary>通用邀请码（与 Phone 二选一）</summary>
    public string? InviteCode { get; set; }

    /// <summary>入店后授予的租户内角色：staff | cashier | manager</summary>
    public string RoleInTenant { get; set; } = "staff";

    public int MaxUses { get; set; } = 1;

    /// <summary>有效期（小时），默认 72</summary>
    public int ValidHours { get; set; } = 72;
}

/// <summary>创建邀请响应 —— rawToken 仅此一次返回，库中只存哈希</summary>
public class CreateInviteResponse
{
    public long InviteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RoleInTenant { get; set; } = string.Empty;
    public int MaxUses { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>明文令牌（仅创建时返回一次）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>可直接发给被邀请人的落地页链接（长 token 形式）</summary>
    public string InviteUrl { get; set; } = string.Empty;

    /// <summary>短码明文（仅创建时返回一次），形如 B1V2d9xK</summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>短链接，形如 http://localhost:5186/i/B1V2d9xK —— 推荐用这个</summary>
    public string ShortUrl { get; set; } = string.Empty;
}

/// <summary>邀请列表项（不含 token，明文与哈希都不外泄）</summary>
public class InviteResponse
{
    public long InviteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? InviteCode { get; set; }
    public string RoleInTenant { get; set; } = string.Empty;
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AcceptedBySub { get; set; }
}

/// <summary>接受邀请请求（Token 与 Code 二选一，短码更常用）</summary>
public class AcceptInviteRequest
{
    /// <summary>长令牌</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>短码，形如 B1V2d9xK</summary>
    public string? Code { get; set; }
}

/// <summary>邀请预览（接受前展示"你将加入哪家店"）</summary>
public class InvitePreviewResponse
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;

    /// <summary>
    /// store / chain / franchise / company / subsidiary
    /// 前端据此决定叫"门店"还是"企业/子公司"，不要把称谓写死。
    /// </summary>
    public string TenantType { get; set; } = "store";
    public string RoleInTenant { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    /// <summary>该邀请本身还能不能被「新的人」使用</summary>
    public bool Usable { get; set; }
    public string? Reason { get; set; }

    /// <summary>
    /// 当前调用者是否已经是该组织成员。
    ///
    /// 为什么需要它：被邀请人注册成功后会被引导登录，登录回来通常仍落在邀请页，
    /// 而那条邀请（maxUses=1）已经用掉了。此时若只看 Usable 就会显示"邀请已失效"，
    /// 对已经成功加入的人来说非常困惑。所以要把"你本来就是成员"单独表达出来。
    /// </summary>
    public bool AlreadyMember { get; set; }

    /// <summary>已是成员时，我在该组织内的角色</summary>
    public string? MyRole { get; set; }
}

/// <summary>
/// 受控自助建号请求（无账号的被邀请人使用）。
/// 匿名端点：授权凭据就是邀请令牌本身。
/// </summary>
public class RegisterInviteRequest
{
    /// <summary>长令牌（与 Code 二选一）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>短码，形如 B1V2d9xK（与 Token 二选一）</summary>
    public string? Code { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>定向邀请需要与邀请上的手机号一致</summary>
    public string? Phone { get; set; }

    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
}
