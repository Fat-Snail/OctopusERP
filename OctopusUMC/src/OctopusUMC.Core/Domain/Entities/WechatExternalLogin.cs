namespace OctopusUMC.Core.Domain.Entities;

public class WechatExternalLogin
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string OpenId { get; set; } = string.Empty;
    public string? UnionId { get; set; }
    public string? WechatNickName { get; set; }
    public string? WechatAvatar { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
