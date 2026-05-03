namespace OctopusPLM.Core.Entities;

/// <summary>外部渠道定义，例如 1688、淘宝、抖店</summary>
public class ChannelDef
{
    public long ChannelId { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ChannelCategoryMapping> CategoryMappings { get; set; } = new();
}
