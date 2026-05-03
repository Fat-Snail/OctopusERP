namespace OctopusUMC.Core.Domain.Entities;

public class OperLog
{
    public long OperId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OperName { get; set; } = string.Empty;
    public string OperUrl { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = string.Empty;
    public string OperParam { get; set; } = string.Empty;
    public string JsonResult { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string? ErrorMsg { get; set; }
    public DateTime OperTime { get; set; } = DateTime.UtcNow;
    public long CostTime { get; set; }
}

public class LoginInfo
{
    public long InfoId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Ipaddr { get; set; } = string.Empty;
    public string LoginLocation { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string Msg { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; } = DateTime.UtcNow;
}
