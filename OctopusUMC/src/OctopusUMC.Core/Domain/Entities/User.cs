namespace OctopusUMC.Core.Domain.Entities;

public class User
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Sex { get; set; } = "0";
    public string? Avatar { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }

    /// <summary>
    /// 用户类型：employee（平台员工）| customer（零售客户/店员）| partner（外部协作方）
    ///
    /// 为什么需要：UMC 是唯一用户主数据源（ADR-010），所有能登录的用户都必须在 sys_user 里；
    /// 但下游同步（OA 通讯录等）只能收到 employee，否则客户会污染员工目录。
    /// </summary>
    public string UserType { get; set; } = "employee";

    /// <summary>数据来源：umc（平台自建）| retail（零售域邀请注册）</summary>
    public string Source { get; set; } = "umc";

    /// <summary>对应 Keycloak 用户 id（UUID）。同步成功后回写，供零售域与审计对齐</summary>
    public string? KcSub { get; set; }

    // 导航属性
    public List<UserDept> UserDepts { get; set; } = new();
    public List<UserRole> UserRoles { get; set; } = new();
}
