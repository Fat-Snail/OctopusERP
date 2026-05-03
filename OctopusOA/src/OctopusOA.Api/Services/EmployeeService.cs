using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Services;

/// <summary>职员档案服务</summary>
public class EmployeeService
{
    private readonly OaDbContext _db;

    public EmployeeService(OaDbContext db) => _db = db;

    /// <summary>HR 创建临时档案</summary>
    public Employee Create(CreateEmployeeRequest req, long createdBy)
    {
        var emp = new Employee
        {
            Status = "temp",
            Name = req.Name,
            Gender = req.Gender,
            Phone = req.Phone,
            BirthDate = req.BirthDate,
            Email = req.Email,
            Ethnicity = req.Ethnicity,
            ApplyPosition = req.ApplyPosition,
            ApplyDeptId = req.ApplyDeptId,
            ApplyDeptName = req.ApplyDeptName,
            Education = req.Education,
            GraduateSchool = req.GraduateSchool,
            Major = req.Major,
            ExpectedSalary = req.ExpectedSalary,
            WorkYears = req.WorkYears,
            ResumeUrl = req.ResumeUrl,
            HrRemark = req.HrRemark,
            H5Token = Guid.NewGuid().ToString("N")[..16],
            CreatedBy = createdBy,
            CreateTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow,
        };
        _db.Employees.Add(emp);
        _db.SaveChanges();
        return emp;
    }

    /// <summary>H5 提交入职信息</summary>
    public string? H5Submit(string token, H5SubmitRequest req)
    {
        var emp = _db.Employees.FirstOrDefault(e => e.H5Token == token);
        if (emp == null) return "无效的入职链接";
        if (emp.Status != "temp") return "该档案状态不允许填写";

        emp.Photo = req.Photo;
        emp.IdCardNo = req.IdCardNo;
        emp.IdCardFrontUrl = req.IdCardFrontUrl;
        emp.IdCardBackUrl = req.IdCardBackUrl;
        emp.PoliticalStatus = req.PoliticalStatus;
        emp.MaritalStatus = req.MaritalStatus;
        emp.NativePlace = req.NativePlace;
        emp.CurrentAddress = req.CurrentAddress;
        emp.HouseholdAddress = req.HouseholdAddress;
        emp.BankName = req.BankName;
        emp.BankAccount = req.BankAccount;
        emp.BankBranch = req.BankBranch;
        emp.H5FilledAt = DateTime.UtcNow;
        emp.Status = "pending";
        emp.UpdateTime = DateTime.UtcNow;

        foreach (var edu in req.Educations)
        {
            edu.Id = 0; // 让 EF 自动生成
            edu.EmployeeId = emp.EmployeeId;
            _db.EmployeeEducations.Add(edu);
        }
        foreach (var wh in req.WorkHistories)
        {
            wh.Id = 0;
            wh.EmployeeId = emp.EmployeeId;
            _db.EmployeeWorkHistories.Add(wh);
        }
        foreach (var fam in req.Families)
        {
            fam.Id = 0;
            fam.EmployeeId = emp.EmployeeId;
            _db.EmployeeFamilies.Add(fam);
        }
        foreach (var ec in req.EmergencyContacts)
        {
            ec.Id = 0;
            ec.EmployeeId = emp.EmployeeId;
            _db.EmployeeEmergencyContacts.Add(ec);
        }

        _db.SaveChanges();
        return null;
    }

    /// <summary>HR 确认入职</summary>
    public string? Confirm(long employeeId)
    {
        var emp = _db.Employees.FirstOrDefault(e => e.EmployeeId == employeeId);
        if (emp == null) return "档案不存在";
        if (emp.Status != "pending") return "只有待入职状态可以确认入职";

        emp.Status = "active";
        emp.UpdateTime = DateTime.UtcNow;
        _db.SaveChanges();
        return null;
    }

    /// <summary>HR 拒绝</summary>
    public string? Reject(long employeeId)
    {
        var emp = _db.Employees.FirstOrDefault(e => e.EmployeeId == employeeId);
        if (emp == null) return "档案不存在";
        if (emp.Status is not ("temp" or "pending")) return "当前状态不允许拒绝";

        emp.Status = "rejected";
        emp.UpdateTime = DateTime.UtcNow;
        _db.SaveChanges();
        return null;
    }

    /// <summary>办理离职</summary>
    public string? Resign(long employeeId)
    {
        var emp = _db.Employees.FirstOrDefault(e => e.EmployeeId == employeeId);
        if (emp == null) return "档案不存在";
        if (emp.Status != "active") return "只有在职状态可以办理离职";

        emp.Status = "resigned";
        emp.UpdateTime = DateTime.UtcNow;
        _db.SaveChanges();
        return null;
    }

    /// <summary>删除档案（仅 temp/rejected）</summary>
    public string? Delete(long employeeId)
    {
        var emp = _db.Employees.FirstOrDefault(e => e.EmployeeId == employeeId);
        if (emp == null) return "档案不存在";
        if (emp.Status is not ("temp" or "rejected")) return "只有临时或已拒绝的档案可以删除";

        _db.Employees.Remove(emp);
        _db.EmployeeEducations.RemoveRange(_db.EmployeeEducations.Where(e => e.EmployeeId == employeeId));
        _db.EmployeeWorkHistories.RemoveRange(_db.EmployeeWorkHistories.Where(e => e.EmployeeId == employeeId));
        _db.EmployeeFamilies.RemoveRange(_db.EmployeeFamilies.Where(e => e.EmployeeId == employeeId));
        _db.EmployeeEmergencyContacts.RemoveRange(_db.EmployeeEmergencyContacts.Where(e => e.EmployeeId == employeeId));
        _db.SaveChanges();
        return null;
    }

    /// <summary>构建详情响应</summary>
    public EmployeeResponse MapToResponse(Employee emp)
    {
        return new EmployeeResponse
        {
            EmployeeId = emp.EmployeeId, Status = emp.Status,
            Name = emp.Name, Gender = emp.Gender, Phone = emp.Phone,
            BirthDate = emp.BirthDate, Email = emp.Email, Ethnicity = emp.Ethnicity,
            ApplyPosition = emp.ApplyPosition, ApplyDeptId = emp.ApplyDeptId, ApplyDeptName = emp.ApplyDeptName,
            Education = emp.Education, GraduateSchool = emp.GraduateSchool, Major = emp.Major,
            ExpectedSalary = emp.ExpectedSalary, WorkYears = emp.WorkYears,
            ResumeUrl = emp.ResumeUrl, HrRemark = emp.HrRemark,
            Photo = emp.Photo, IdCardNo = emp.IdCardNo,
            IdCardFrontUrl = emp.IdCardFrontUrl, IdCardBackUrl = emp.IdCardBackUrl,
            PoliticalStatus = emp.PoliticalStatus, MaritalStatus = emp.MaritalStatus,
            NativePlace = emp.NativePlace, CurrentAddress = emp.CurrentAddress,
            HouseholdAddress = emp.HouseholdAddress,
            BankName = emp.BankName, BankAccount = emp.BankAccount, BankBranch = emp.BankBranch,
            H5Token = emp.H5Token, H5FilledAt = emp.H5FilledAt,
            UmcUserId = emp.UmcUserId, CreatedBy = emp.CreatedBy,
            CreateTime = emp.CreateTime, UpdateTime = emp.UpdateTime,
            Educations = _db.EmployeeEducations.Where(e => e.EmployeeId == emp.EmployeeId).ToList(),
            WorkHistories = _db.EmployeeWorkHistories.Where(e => e.EmployeeId == emp.EmployeeId).ToList(),
            Families = _db.EmployeeFamilies.Where(e => e.EmployeeId == emp.EmployeeId).ToList(),
            EmergencyContacts = _db.EmployeeEmergencyContacts.Where(e => e.EmployeeId == emp.EmployeeId).ToList(),
        };
    }
}
