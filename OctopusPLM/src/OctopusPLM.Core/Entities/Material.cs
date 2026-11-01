namespace OctopusPLM.Core.Entities;

public sealed class Material
{
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string BaseUnit { get; set; } = "件";
    public string MaterialType { get; set; } = "raw_material";
    public string Status { get; set; } = "active";
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
