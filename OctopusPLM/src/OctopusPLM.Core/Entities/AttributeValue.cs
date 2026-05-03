namespace OctopusPLM.Core.Entities;

/// <summary>属性可选值（枚举类属性用）</summary>
public class AttributeValue
{
    public long ValueId { get; set; }
    public long AttributeId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public int Status { get; set; } = 1;

    public PlmAttribute? Attribute { get; set; }
}
