namespace OctopusPLM.Api.DTOs;

public class ChannelDefResponse
{
    public long ChannelId { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChannelCategoryMappingItem
{
    public long MappingId { get; set; }
    public long ChannelId { get; set; }
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ExternalCategoryId { get; set; } = string.Empty;
    public string ExternalCategoryName { get; set; } = string.Empty;
    public string MappingVersion { get; set; } = "v1";
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ChannelAttrMappingItem> AttributeMappings { get; set; } = new();
}

public class ChannelAttrMappingItem
{
    public long MappingId { get; set; }
    public long AttributeId { get; set; }
    public string AttributeName { get; set; } = string.Empty;
    public string? AttributeCode { get; set; }
    public string ExternalAttributeId { get; set; } = string.Empty;
    public string ExternalAttributeName { get; set; } = string.Empty;
    public bool IsRequiredOutbound { get; set; }
    public string? TransformRuleJson { get; set; }
    public int Status { get; set; }
}
