namespace OctopusPLM.Core.Entities;

/// <summary>商品详情组件定义，例如主图轮播、参数表、资质证照、FAQ</summary>
public class DetailComponentDef
{
    public long ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>gallery / rich_text / param_table / certificate / faq / video</summary>
    public string ComponentType { get; set; } = "rich_text";

    public string? SchemaJson { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<DetailComponentBind> TemplateBinds { get; set; } = new();
    public List<ProductDetailBlock> ProductBlocks { get; set; } = new();
}
