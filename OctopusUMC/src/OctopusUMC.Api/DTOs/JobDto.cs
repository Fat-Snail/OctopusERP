namespace OctopusUMC.Api.DTOs;

/// <summary>任务调度响应</summary>
public class JobResponse
{
    public long JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = "DEFAULT";
    public string InvokeTarget { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>创建任务请求</summary>
public class CreateJobRequest
{
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = "DEFAULT";
    public string InvokeTarget { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>修改任务请求</summary>
public class UpdateJobRequest
{
    public long JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = "DEFAULT";
    public string InvokeTarget { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>任务日志响应</summary>
public class JobLogResponse
{
    public long JobLogId { get; set; }
    public long JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = string.Empty;
    public string InvokeTarget { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public long ElapsedMs { get; set; }
    public int Status { get; set; }
    public string? ErrorMsg { get; set; }
}
