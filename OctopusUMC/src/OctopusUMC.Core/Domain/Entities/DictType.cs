namespace OctopusUMC.Core.Domain.Entities;

public class DictType
{
    public long DictId { get; set; }
    public string DictName { get; set; } = string.Empty;
    public string DictTypeCode { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }
}

public class DictData
{
    public long DictCode { get; set; }
    public string DictType { get; set; } = string.Empty;
    public string DictLabel { get; set; } = string.Empty;
    public string DictValue { get; set; } = string.Empty;
    public int DictSort { get; set; }
    public int Status { get; set; } = 1;
    public bool IsDefault { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }
}
