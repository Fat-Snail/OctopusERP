namespace OctopusHRM.Api.Persistence;

/// <summary>HRM 主员工档案（基础信息 + 雇佣关系，从 OA Employee 镜像或扩展）</summary>
public class HrmEmployee
{
    public long EmployeeId { get; set; }
    public long? OaEmployeeId { get; set; }
    public long? UmcUserId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? IdCardNo { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    /// <summary>雇佣类型：full_time / part_time / intern / contractor</summary>
    public string EmploymentType { get; set; } = "full_time";
    public DateTime? HiredAt { get; set; }
    public DateTime? RegularizedAt { get; set; }
    public DateTime? ResignedAt { get; set; }
    /// <summary>active / probation / on_leave / resigned</summary>
    public string Status { get; set; } = "active";
    public decimal? BaseSalary { get; set; }
    public string? BankAccount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>薪资项配置（基本工资/绩效/补贴/扣款）</summary>
public class HrmSalaryItem
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    /// <summary>earning（收入项）/ deduction（扣款项）</summary>
    public string ItemType { get; set; } = "earning";
    /// <summary>fixed（固定）/ formula（公式）/ manual（手动）</summary>
    public string CalculationType { get; set; } = "fixed";
    public decimal? FixedAmount { get; set; }
    public string? Formula { get; set; }
    public bool IsTaxable { get; set; } = true;
    public string Status { get; set; } = "active";
}

/// <summary>月度算薪批次</summary>
public class HrmPayrollRun
{
    public long PayrollRunId { get; set; }
    public string PeriodCode { get; set; } = string.Empty; // yyyy-MM
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>draft / calculated / approved / paid / cancelled</summary>
    public string Status { get; set; } = "draft";
    public int EmployeeCount { get; set; }
    public decimal TotalGrossPay { get; set; }
    public decimal TotalNetPay { get; set; }
    public long? FinVoucherId { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    public List<HrmPayrollDetail> Details { get; set; } = new();
}

/// <summary>员工算薪明细（一员工一行）</summary>
public class HrmPayrollDetail
{
    public long DetailId { get; set; }
    public long PayrollRunId { get; set; }
    public long EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    /// <summary>出勤天数（从 OA 考勤拉）</summary>
    public decimal AttendanceDays { get; set; }
    public decimal LeaveDays { get; set; }
    public decimal AbsentDays { get; set; }
    /// <summary>JSON 项明细 [{itemCode, itemName, type, amount}, ...]</summary>
    public string ItemsJson { get; set; } = "[]";
    public decimal GrossPay { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetPay { get; set; }
    public string? Remark { get; set; }

    public HrmPayrollRun? PayrollRun { get; set; }
}

/// <summary>假期余额（与 OA 考勤打通）</summary>
public class HrmLeaveBalance
{
    public long BalanceId { get; set; }
    public long EmployeeId { get; set; }
    /// <summary>annual（年假）/ sick（病假）/ personal（事假）/ marriage（婚假）/ maternity（产假）</summary>
    public string LeaveType { get; set; } = "annual";
    public int Year { get; set; }
    public decimal TotalDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal RemainingDays { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>KPI / 绩效（季度/月度评分）</summary>
public class HrmKpi
{
    public long KpiId { get; set; }
    public long EmployeeId { get; set; }
    public string Period { get; set; } = string.Empty;  // yyyy-Q1 / yyyy-MM
    public string Title { get; set; } = string.Empty;
    public decimal? Score { get; set; }   // 0-100
    public string? Grade { get; set; }    // S/A/B/C/D
    public string? Comment { get; set; }
    public long? ReviewerId { get; set; }
    /// <summary>draft / submitted / approved</summary>
    public string Status { get; set; } = "draft";
    public DateTime CreatedAt { get; set; }
}

/// <summary>UMC 同步用户</summary>
public class HrmSyncUser
{
    public long Id { get; set; }
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Email { get; set; }
    public DateTime LastSyncAt { get; set; }
}
