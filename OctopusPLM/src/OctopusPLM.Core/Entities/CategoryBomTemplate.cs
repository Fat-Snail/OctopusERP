namespace OctopusPLM.Core.Entities;

public sealed class CategoryBomTemplate
{
    public long TemplateId { get; set; }
    public long CategoryId { get; set; }
    public string TemplateName { get; set; } = "通用 MES 生产 BOM";
    public string Version { get; set; } = "V1.0";
    public string Status { get; set; } = "draft";
    public bool IsLatest { get; set; }
    public long? SourceManufacturingVersionId { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Category? Category { get; set; }
    public List<CategoryBomTemplateItem> Items { get; set; } = [];
}

public sealed class CategoryBomTemplateItem
{
    public long TemplateItemId { get; set; }
    public long TemplateId { get; set; }
    public int LineNo { get; set; }
    public long MaterialId { get; set; }
    public decimal DefaultQuantity { get; set; }
    public decimal DefaultLossRate { get; set; }
    public CategoryBomTemplate? Template { get; set; }
    public Material? Material { get; set; }
}
