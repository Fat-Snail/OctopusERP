namespace OctopusOA.Api.DTOs;

/// <summary>首页数据卡片汇总</summary>
public class DashboardSummary
{
    public int PendingApprovals { get; set; }
    public int UnreadNotices { get; set; }
    public TodayAttendanceSnapshot? TodayAttendance { get; set; }
    public int TodayMeetings { get; set; }
    public int MyApprovalsCount { get; set; }
}

public class TodayAttendanceSnapshot
{
    public bool CheckedIn { get; set; }
    public bool CheckedOut { get; set; }
    public string CheckInStatus { get; set; } = "missing";
    public string CheckOutStatus { get; set; } = "missing";
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
}

/// <summary>待办项（聚合多源）</summary>
public class TodoItem
{
    public string Type { get; set; } = string.Empty;       // approval / notice / attendance / meeting
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;       // 原始时间（用于排序）
    public string Link { get; set; } = string.Empty;       // 前端跳转路径
    public string? Tag { get; set; }                       // 类型小标签
    public string? TagType { get; set; }                   // success / warning / danger / info
}
