namespace OctopusPLM.Core.Entities;

/// <summary>类目-属性绑定：决定选了这个类目后表单渲染哪些字段</summary>
public class CategoryAttribute
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public long? ModelVersionId { get; set; }
    public long AttributeId { get; set; }
    public bool IsRequired { get; set; }
    public int OrderNum { get; set; }

    /// <summary>分组名：相同 GroupName 同一组（SKU 维度组）；null 表示非 SKU 属性</summary>
    public string? GroupName { get; set; }

    /// <summary>属性分组：basic / sale / logistics / compliance / detail</summary>
    public string GroupType { get; set; } = "basic";

    /// <summary>是否销售属性轴；为 true 时参与 SKU 组合生成</summary>
    public bool IsSaleAxis { get; set; }

    /// <summary>类目内属性扩展规则：条件显示、默认值、范围校验等</summary>
    public string? ExtRulesJson { get; set; }

    public Category? Category { get; set; }
    public CategoryModelVersion? ModelVersion { get; set; }
    public PlmAttribute? Attribute { get; set; }
}
