namespace OctopusPLM.Core.Entities;

/// <summary>类目规则定义：条件显示、联动、字段校验、发布校验</summary>
public class RuleDef
{
    public long RuleId { get; set; }
    public long ModelVersionId { get; set; }

    /// <summary>visibility / linkage / validation / publish_check</summary>
    public string RuleType { get; set; } = "validation";

    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string? TriggerExpr { get; set; }
    public string ActionExpr { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int Status { get; set; } = 1;
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CategoryModelVersion? ModelVersion { get; set; }
}
