namespace OctopusUMC.Core.Domain.Entities;

public class Job
{
    public long JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = "DEFAULT";
    public string InvokeTarget { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }
}
