namespace OctopusPLM.Core.Entities;

/// <summary>内部类目到外部渠道类目的映射</summary>
public class ChannelCategoryMapping
{
    public long MappingId { get; set; }
    public long ChannelId { get; set; }
    public long CategoryId { get; set; }
    public string ExternalCategoryId { get; set; } = string.Empty;
    public string ExternalCategoryName { get; set; } = string.Empty;
    public string MappingVersion { get; set; } = "v1";
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ChannelDef? Channel { get; set; }
    public Category? Category { get; set; }
    public List<ChannelAttributeMapping> AttributeMappings { get; set; } = new();
}
