using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.DTOs;

// ── HR 端 ──────────────────────────────────────────

public class CreateEmployeeRequest
{
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
}

public class UpdateEmployeeRequest : CreateEmployeeRequest
{
    public long EmployeeId { get; set; }
}

public class EmployeeResponse
{
    // 主档信息
    public long EmployeeId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
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
    // H5 填写
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
    // 系统
    public string H5Token { get; set; } = string.Empty;
    public string? H5Url { get; set; }
    public DateTime? H5FilledAt { get; set; }
    public long? UmcUserId { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    // 子表
    public List<EmployeeEducation> Educations { get; set; } = new();
    public List<EmployeeWorkHistory> WorkHistories { get; set; } = new();
    public List<EmployeeFamily> Families { get; set; } = new();
    public List<EmployeeEmergencyContact> EmergencyContacts { get; set; } = new();
}

// ── H5 端 ──────────────────────────────────────────

public class H5OnboardResponse
{
    public long EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? BirthDate { get; set; }
    public string? Ethnicity { get; set; }
    public string ApplyPosition { get; set; } = string.Empty;
    public string? ApplyDeptName { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool AlreadyFilled { get; set; }
}

public class H5SubmitRequest
{
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
    public List<EmployeeEducation> Educations { get; set; } = new();
    public List<EmployeeWorkHistory> WorkHistories { get; set; } = new();
    public List<EmployeeFamily> Families { get; set; } = new();
    public List<EmployeeEmergencyContact> EmergencyContacts { get; set; } = new();
}
