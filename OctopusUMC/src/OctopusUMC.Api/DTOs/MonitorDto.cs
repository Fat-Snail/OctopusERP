namespace OctopusUMC.Api.DTOs;

/// <summary>在线用户响应</summary>
public class OnlineUserResponse
{
    /// <summary>会话标识（当前阶段使用 userId 字符串，SignalR 接入后替换为真实 ConnectionId）</summary>
    public string TokenId { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string DeptName { get; set; } = string.Empty;
    public string Ipaddr { get; set; } = string.Empty;
    public string LoginLocation { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
}

/// <summary>操作日志响应</summary>
public class OperLogResponse
{
    public long OperId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OperName { get; set; } = string.Empty;
    public string OperUrl { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = string.Empty;
    public string OperParam { get; set; } = string.Empty;
    public string JsonResult { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? ErrorMsg { get; set; }
    public DateTime OperTime { get; set; }
    public long CostTime { get; set; }
}

/// <summary>访问日志响应</summary>
public class LoginfoResponse
{
    public long InfoId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Ipaddr { get; set; } = string.Empty;
    public string LoginLocation { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Msg { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
}

/// <summary>服务器信息响应</summary>
public class ServerInfoResponse
{
    public CpuInfo Cpu { get; set; } = new();
    public MemInfo Mem { get; set; } = new();
    public JvmInfo Jvm { get; set; } = new();
    public List<SysFileInfo> SysFiles { get; set; } = new();
}

public class CpuInfo
{
    public int CpuNum { get; set; }
    public double Used { get; set; }
    public double Sys { get; set; }
    public double Free { get; set; }
}

public class MemInfo
{
    public long Total { get; set; }
    public long Used { get; set; }
    public long Free { get; set; }
}

public class JvmInfo
{
    public long Total { get; set; }
    public long Used { get; set; }
    public long Free { get; set; }
    public string Version { get; set; } = string.Empty;
}

public class SysFileInfo
{
    public string DirName { get; set; } = string.Empty;
    public string SysTypeName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
    public string Free { get; set; } = string.Empty;
    public string Used { get; set; } = string.Empty;
    public double Usage { get; set; }
}

/// <summary>工作台概览数据</summary>
public class DashboardSummaryResponse
{
    public int OnlineUserCount { get; set; }
    public int TodayLoginCount { get; set; }
    public int NoticeCount { get; set; }
    public int TotalUserCount { get; set; }
}
