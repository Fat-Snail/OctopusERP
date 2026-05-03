namespace OctopusPLM.Core.Entities;

/// <summary>属性定义：描述商品的一个可配置特征</summary>
public class PlmAttribute
{
    public long AttributeId { get; set; }

    /// <summary>稳定编码，用于跨类目、跨渠道映射；为空时兼容历史数据</summary>
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>属性类型：text / number / enum / date / image</summary>
    public string AttributeType { get; set; } = "text";

    /// <summary>输入方式：single_line / multi_line / dropdown / checkbox / radio</summary>
    public string InputType { get; set; } = "single_line";

    public string? Unit { get; set; }

    /// <summary>取值范围：global / category / channel</summary>
    public string ValueScope { get; set; } = "global";

    /// <summary>是否全局属性；类目专属属性可设为 false</summary>
    public bool IsGlobal { get; set; } = true;

    /// <summary>扩展 schema，例如精度、长度、图片数量限制等</summary>
    public string? SchemaJson { get; set; }

    public bool IsRequired { get; set; }
    public int OrderNum { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public List<AttributeValue> Values { get; set; } = new();
    public List<CategoryAttribute> CategoryAttributes { get; set; } = new();
}
