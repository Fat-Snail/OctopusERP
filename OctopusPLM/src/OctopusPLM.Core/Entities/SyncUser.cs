namespace OctopusPLM.Core.Entities;

/// <summary>从 UMC 同步的用户缓存（只读，不存密码）</summary>
public class SyncUser
{
    public long Id { get; set; }
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int Status { get; set; } = 1;

    /// <summary>PLM 本地角色，逗号分隔</summary>
    public string PlmRoles { get; set; } = "plm_user";

    public DateTime? LastSyncAt { get; set; } = DateTime.UtcNow;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
