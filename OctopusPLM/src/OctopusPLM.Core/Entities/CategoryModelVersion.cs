namespace OctopusPLM.Core.Entities;

/// <summary>类目模型版本：锁定某一时期的字段、规则和详情模板</summary>
public class CategoryModelVersion
{
    public long ModelVersionId { get; set; }
    public long CategoryId { get; set; }
    public string VersionNo { get; set; } = "v1";

    /// <summary>draft / active / archived</summary>
    public string Status { get; set; } = "draft";

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public long? PublishedBy { get; set; }
    public string? ChangeSummary { get; set; }

    /// <summary>发布时快照，便于历史商品回放和配置差异比对</summary>
    public string? ModelSnapshotJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Category? Category { get; set; }
    public List<CategoryAttribute> AttributeBinds { get; set; } = new();
    public List<RuleDef> Rules { get; set; } = new();
    public List<DetailTemplate> DetailTemplates { get; set; } = new();
    public List<Product> Products { get; set; } = new();
}
