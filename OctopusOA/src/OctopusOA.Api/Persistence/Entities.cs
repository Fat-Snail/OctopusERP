namespace OctopusOA.Api.Persistence;

// ── 同步用户 ─────────────────────────────────────────

public class SyncUser
{
    public long Id { get; set; }
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
    public int Status { get; set; } = 1;
    public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;
    public List<string> OaRoles { get; set; } = new();
}

// ── 部门缓存 ─────────────────────────────────────────

public class OaDept
{
    public long DeptId { get; set; }
    public long ParentId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public int Status { get; set; } = 1;
    public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;
}

public class OaUserDept
{
    public long UmcUserId { get; set; }
    public long DeptId { get; set; }
    public long? PostId { get; set; }
    public string? PostName { get; set; }
    public bool IsPrimary { get; set; }
}

// ── 审批流模板 ────────────────────────────────────────

public class WorkflowTemplate
{
    public long TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string FormSchema { get; set; } = "{}";
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}

public class WorkflowNode
{
    public long NodeId { get; set; }
    public long TemplateId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public int NodeOrder { get; set; }
    public string ApproverType { get; set; } = "role";
    public string? ApproverValue { get; set; }
    public int Status { get; set; } = 1;
}

// ── 审批实例 ─────────────────────────────────────────

public class Approval
{
    public long ApprovalId { get; set; }
    public long TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public long ApplicantId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public int CurrentNodeOrder { get; set; }
    public string Status { get; set; } = "draft";
    public string FormData { get; set; } = "{}";
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}

public class ApprovalRecord
{
    public long RecordId { get; set; }
    public long ApprovalId { get; set; }
    public long NodeId { get; set; }
    public int NodeOrder { get; set; }
    public long ApproverId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string Action { get; set; } = "approve";
    public string? Comment { get; set; }
    public DateTime ActionTime { get; set; } = DateTime.UtcNow;
}

// ── 职员档案 ─────────────────────────────────────────

public class Employee
{
    public long EmployeeId { get; set; }
    public string Status { get; set; } = "temp";

    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = "male";
    public string Phone { get; set; } = string.Empty;
    public string? BirthDate { get; set; }
    public string? Email { get; set; }
    public string? Ethnicity { get; set; }
    public string ApplyPosition { get; set; } = string.Empty;
    public long? ApplyDeptId { get; set; }
    public string? ApplyDeptName { get; set; }
    public string? Education { get; set; }
    public string? GraduateSchool { get; set; }
    public string? Major { get; set; }
    public string? ExpectedSalary { get; set; }
    public int? WorkYears { get; set; }
    public string? ResumeUrl { get; set; }
    public string? HrRemark { get; set; }

    public string? Photo { get; set; }
    public string? IdCardNo { get; set; }
    public string? IdCardFrontUrl { get; set; }
    public string? IdCardBackUrl { get; set; }
    public string? PoliticalStatus { get; set; }
    public string? MaritalStatus { get; set; }
    public string? NativePlace { get; set; }
    public string? CurrentAddress { get; set; }
    public string? HouseholdAddress { get; set; }
    public string? BankName { get; set; }
    public string? BankAccount { get; set; }
    public string? BankBranch { get; set; }

    public string H5Token { get; set; } = string.Empty;
    public DateTime? H5FilledAt { get; set; }
    public long? UmcUserId { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}

public class EmployeeEducation
{
    public long Id { get; set; }
    public long EmployeeId { get; set; }
    public string School { get; set; } = string.Empty;
    public string Education { get; set; } = string.Empty;
    public string? Major { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Degree { get; set; }
    public bool IsFullTime { get; set; } = true;
}

public class EmployeeWorkHistory
{
    public long Id { get; set; }
    public long EmployeeId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Salary { get; set; }
    public string? LeaveReason { get; set; }
    public string? RefName { get; set; }
    public string? RefPhone { get; set; }
}

public class EmployeeFamily
{
    public long Id { get; set; }
    public long EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string? Workplace { get; set; }
    public string? Phone { get; set; }
}

public class EmployeeEmergencyContact
{
    public long Id { get; set; }
    public long EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

// ── 公告 ───────────────────────────────────────────

public class OaNotice
{
    public long NoticeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "1";
    public int Priority { get; set; } = 0;
    public string Publisher { get; set; } = string.Empty;
    public DateTime PublishTime { get; set; }
    public string Source { get; set; } = "umc";
    public int Status { get; set; } = 1;
}

public class OaNoticeRead
{
    public long Id { get; set; }
    public long NoticeId { get; set; }
    public long UmcUserId { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}

// ── 考勤 ───────────────────────────────────────────

public class OaAttendanceRule
{
    public long Id { get; set; }
    public string Code { get; set; } = "standard";
    public string Name { get; set; } = "标准班";
    public string WorkStartTime { get; set; } = "09:00";
    public string WorkEndTime { get; set; } = "18:00";
    public int LateThresholdMin { get; set; } = 15;
    public int EarlyLeaveThresholdMin { get; set; } = 30;
    public string? IpWhiteList { get; set; }
    public bool IsDefault { get; set; }
    public int Status { get; set; } = 1;
}

public class OaUserShift
{
    public long UmcUserId { get; set; }
    public long ShiftId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

public class OaAttendance
{
    public long Id { get; set; }
    public long UmcUserId { get; set; }
    public string Date { get; set; } = string.Empty;
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? CheckInIp { get; set; }
    public string? CheckOutIp { get; set; }
    public string CheckInStatus { get; set; } = "missing";
    public string CheckOutStatus { get; set; } = "missing";
    public double WorkHours { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}

public class OaAttendanceFix
{
    public long Id { get; set; }
    public long AttendanceId { get; set; }
    public long ApprovalId { get; set; }
    public string Type { get; set; } = "checkIn";
    public DateTime FixTime { get; set; }
    public string? Reason { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}

// ── 会议室 ─────────────────────────────────────────

public class OaMeetingRoom
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Location { get; set; }
    public string Equipment { get; set; } = "[]";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}

public class OaMeetingBooking
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string Title { get; set; } = string.Empty;
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Description { get; set; }
    public string Attendees { get; set; } = "[]";
    public string Status { get; set; } = "confirmed";
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
