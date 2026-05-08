namespace OctopusUMC.Core.Domain.Entities;

public class JobLog
{
    public long JobLogId { get; set; }
    public long JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = string.Empty;
    public string InvokeTarget { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public long ElapsedMs { get; set; }
    public int Status { get; set; }  // 0=成功 1=失败
    public string? ErrorMsg { get; set; }
}
