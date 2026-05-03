namespace OctopusPLM.Core.Entities;

/// <summary>商品属性结构化值；与 Product.AttributesJson 双轨兼容</summary>
public class ProductAttributeValue
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long AttributeId { get; set; }
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public DateTime? ValueDateTime { get; set; }
    public string? ValueJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public PlmAttribute? Attribute { get; set; }
}
