namespace OctopusPLM.Core.Entities;

public class ManufacturingVersion
{
    public long ManufacturingVersionId { get; set; }
    public long ProductId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public string BomVersion { get; set; } = string.Empty;
    public string RoutingVersion { get; set; } = string.Empty;
    public string? PayloadHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string MesSyncStatus { get; set; } = "not_sent";
    public DateTime? LastMesSyncAt { get; set; }
    public string? MesSyncError { get; set; }
    public int MesNotifyCount { get; set; }
    public Product? Product { get; set; }
    public List<ManufacturingBomItem> BomItems { get; set; } = new();
    public List<ManufacturingOperation> Operations { get; set; } = new();
}

public class ManufacturingBomItem
{
    public long BomItemId { get; set; }
    public long ManufacturingVersionId { get; set; }
    public int LineNo { get; set; }
    public long? MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string? MaterialSpecification { get; set; }
    public string? MaterialUnit { get; set; }
    public decimal Quantity { get; set; }
    public decimal LossRate { get; set; }
    public ManufacturingVersion? ManufacturingVersion { get; set; }
    public Material? Material { get; set; }
}

public class ManufacturingOperation
{
    public long OperationId { get; set; }
    public long ManufacturingVersionId { get; set; }
    public int Sequence { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string RequiredCapability { get; set; } = string.Empty;
    public int StandardCycleSec { get; set; }
    public int SetupMinutes { get; set; }
    public bool IsSplittable { get; set; } = true;
    public ManufacturingVersion? ManufacturingVersion { get; set; }
}
