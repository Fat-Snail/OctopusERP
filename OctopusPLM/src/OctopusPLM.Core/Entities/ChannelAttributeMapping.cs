namespace OctopusPLM.Core.Entities;

/// <summary>内部属性到外部渠道属性的映射</summary>
public class ChannelAttributeMapping
{
    public long MappingId { get; set; }
    public long ChannelCategoryMappingId { get; set; }
    public long AttributeId { get; set; }
    public string ExternalAttributeId { get; set; } = string.Empty;
    public string ExternalAttributeName { get; set; } = string.Empty;
    public string? TransformRuleJson { get; set; }
    public bool IsRequiredOutbound { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ChannelCategoryMapping? ChannelCategoryMapping { get; set; }
    public PlmAttribute? Attribute { get; set; }
}
