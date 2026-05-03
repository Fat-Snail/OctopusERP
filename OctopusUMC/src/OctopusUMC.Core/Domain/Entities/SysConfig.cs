namespace OctopusUMC.Core.Domain.Entities;

public class SysConfig
{
    public long ConfigId { get; set; }
    public string ConfigName { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public bool ConfigType { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }
}
