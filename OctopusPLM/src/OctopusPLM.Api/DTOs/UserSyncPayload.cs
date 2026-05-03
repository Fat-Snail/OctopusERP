namespace OctopusPLM.Api.DTOs;

/// <summary>UMC 推送的用户同步数据载荷</summary>
public class UserSyncPayload
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int Status { get; set; }
}
