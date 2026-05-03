namespace OctopusPLM.Api.DTOs;

/// <summary>类目树节点</summary>
public class CategoryTreeNode
{
    public long CategoryId { get; set; }
    public long? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int OrderNum { get; set; }
    public List<CategoryTreeNode> Children { get; set; } = new();
}

/// <summary>类目下绑定的属性（含枚举值）</summary>
public class CategoryAttributeResponse
{
    public long Id { get; set; }
    public long AttributeId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AttributeType { get; set; } = string.Empty;
    public string InputType { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public int OrderNum { get; set; }
    public string? GroupName { get; set; }
    public string GroupType { get; set; } = "basic";
    public bool IsSaleAxis { get; set; }
    public string? ExtRulesJson { get; set; }
    public List<AttributeValueItem> Values { get; set; } = new();
}

public class AttributeValueItem
{
    public long ValueId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>按 GroupName 分组后的属性</summary>
public class CategoryAttributeGrouped
{
    public string? GroupName { get; set; }
    public string GroupLabel { get; set; } = string.Empty;
    public List<CategoryAttributeResponse> Attributes { get; set; } = new();
}

public class CategoryModelVersionSummary
{
    public long ModelVersionId { get; set; }
    public string VersionNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? PublishedAt { get; set; }
    public long? PublishedBy { get; set; }
    public string? ChangeSummary { get; set; }
}

public class CategoryRuleResponse
{
    public long RuleId { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string? TriggerExpr { get; set; }
    public string ActionExpr { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int Status { get; set; }
    public string? Message { get; set; }
}

public class DetailComponentItem
{
    public long BindId { get; set; }
    public long ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public bool IsRequired { get; set; }
    public string? DisplayRuleJson { get; set; }
    public string? DefaultContentJson { get; set; }
}

public class DetailTemplateResponse
{
    public long TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<DetailComponentItem> Components { get; set; } = new();
}

public class ChannelAttributeMappingResponse
{
    public long MappingId { get; set; }
    public long AttributeId { get; set; }
    public string AttributeName { get; set; } = string.Empty;
    public string? AttributeCode { get; set; }
    public string ExternalAttributeId { get; set; } = string.Empty;
    public string ExternalAttributeName { get; set; } = string.Empty;
    public bool IsRequiredOutbound { get; set; }
    public string? TransformRuleJson { get; set; }
}

public class ChannelCategoryMappingResponse
{
    public long MappingId { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ExternalCategoryId { get; set; } = string.Empty;
    public string ExternalCategoryName { get; set; } = string.Empty;
    public string MappingVersion { get; set; } = string.Empty;
    public List<ChannelAttributeMappingResponse> AttributeMappings { get; set; } = new();
}

public class CategoryModelDetailResponse
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public CategoryModelVersionSummary? ActiveVersion { get; set; }
    public List<CategoryModelVersionSummary> Versions { get; set; } = new();
    public List<CategoryAttributeGrouped> AttributeGroups { get; set; } = new();
    public List<CategoryRuleResponse> Rules { get; set; } = new();
    public List<DetailTemplateResponse> DetailTemplates { get; set; } = new();
    public List<ChannelCategoryMappingResponse> ChannelMappings { get; set; } = new();
}
