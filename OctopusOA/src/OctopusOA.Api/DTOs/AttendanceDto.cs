namespace OctopusOA.Api.DTOs;

public class TodayAttendanceResponse
{
    public string Date { get; set; } = string.Empty;
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string CheckInStatus { get; set; } = "missing";
    public string CheckOutStatus { get; set; } = "missing";
    public bool CanCheckIn { get; set; }
    public bool CanCheckOut { get; set; }
    public string RuleWorkStart { get; set; } = "09:00";
    public string RuleWorkEnd { get; set; } = "18:00";
}

public class AttendanceItem
{
    public long Id { get; set; }
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string CheckInStatus { get; set; } = string.Empty;
    public string CheckOutStatus { get; set; } = string.Empty;
    public double WorkHours { get; set; }
    public bool IsFixed { get; set; }  // 是否有补卡记录
}

public class AttendanceRuleDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WorkStartTime { get; set; } = "09:00";
    public string WorkEndTime { get; set; } = "18:00";
    public int LateThresholdMin { get; set; }
    public int EarlyLeaveThresholdMin { get; set; }
    public string? IpWhiteList { get; set; }
    public bool IsDefault { get; set; }
    public int Status { get; set; }
}

public class UserShiftItem
{
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public long ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string ShiftCode { get; set; } = string.Empty;
    public string WorkStartTime { get; set; } = string.Empty;
    public string WorkEndTime { get; set; } = string.Empty;
}

public class AssignUserShiftRequest
{
    public long UmcUserId { get; set; }
    public long ShiftId { get; set; }
}

public class AttendanceStatsItem
{
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public int NormalDays { get; set; }
    public int LateCount { get; set; }
    public int EarlyLeaveCount { get; set; }
    public int MissingCount { get; set; }
    public double TotalWorkHours { get; set; }
}
