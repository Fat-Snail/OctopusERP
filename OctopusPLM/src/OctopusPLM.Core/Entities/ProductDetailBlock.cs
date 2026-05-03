namespace OctopusPLM.Core.Entities;

/// <summary>商品详情实际内容块</summary>
public class ProductDetailBlock
{
    public long BlockId { get; set; }
    public long ProductId { get; set; }
    public long ComponentId { get; set; }
    public int OrderNum { get; set; }
    public string ContentJson { get; set; } = "{}";
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public DetailComponentDef? Component { get; set; }
}
