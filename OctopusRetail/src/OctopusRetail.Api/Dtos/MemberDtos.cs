namespace OctopusRetail.Api.Dtos;

/// <summary>组织成员</summary>
public class MemberResponse
{
    public long MembershipId { get; set; }

    /// <summary>Keycloak sub（用户的唯一标识）</summary>
    public string KcSub { get; set; } = string.Empty;

    /// <summary>加入时的用户名快照（可能为空：早期成员未记录）</summary>
    public string? UserName { get; set; }

    /// <summary>加入时的姓名快照</summary>
    public string? DisplayName { get; set; }

    /// <summary>加入时的手机号</summary>
    public string? Phone { get; set; }

    /// <summary>owner | manager | staff | cashier</summary>
    public string RoleInTenant { get; set; } = string.Empty;

    /// <summary>active | suspended | left</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }

    /// <summary>Keycloak group 对账状态：pending / synced / failed / unmanaged</summary>
    public string? KcSyncState { get; set; }
}

public class SetMemberStatusRequest
{
    public bool Enabled { get; set; }
}

public class ResetMemberPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>UMC → 零售：成员资料变更（ADR-010）</summary>
public class SyncMemberProfileRequest
{
    /// <summary>Keycloak sub（用户唯一标识）</summary>
    public string KcSub { get; set; } = string.Empty;

    /// <summary>只覆盖非空字段</summary>
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
}
