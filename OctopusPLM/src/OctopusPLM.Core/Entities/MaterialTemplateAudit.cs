namespace OctopusPLM.Core.Entities;

public sealed class MaterialTemplateAudit
{
    public long AuditId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public long OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public long? SourceId { get; set; }
    public long? ManufacturingVersionId { get; set; }
    public string? DetailJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
