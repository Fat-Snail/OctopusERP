namespace OctopusPLM.Core.Entities;

/// <summary>类目详情模板：由多个详情组件按顺序组合</summary>
public class DetailTemplate
{
    public long TemplateId { get; set; }
    public long ModelVersionId { get; set; }
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>draft / active / archived</summary>
    public string Status { get; set; } = "draft";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public CategoryModelVersion? ModelVersion { get; set; }
    public List<DetailComponentBind> ComponentBinds { get; set; } = new();
}
