using Microsoft.EntityFrameworkCore;
using OctopusHRM.Api.Persistence;

namespace OctopusHRM.Api.Services;

public class EmployeeService(HrmDbContext db)
{
    public async Task<List<HrmEmployee>> GetListAsync(string? status, string? keyword)
    {
        var q = db.Employees.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrEmpty(keyword))
            q = q.Where(x => x.FullName.Contains(keyword) || x.EmployeeNo.Contains(keyword));
        return await q.OrderBy(x => x.EmployeeId).ToListAsync();
    }

    public async Task<HrmEmployee?> GetByIdAsync(long id) =>
        await db.Employees.FirstOrDefaultAsync(x => x.EmployeeId == id);

    public async Task<(HrmEmployee? employee, string? error)> CreateAsync(HrmEmployee req)
    {
        if (string.IsNullOrEmpty(req.FullName)) return (null, "姓名必填");
        if (string.IsNullOrEmpty(req.EmployeeNo))
            req.EmployeeNo = $"EMP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
        if (await db.Employees.AnyAsync(x => x.EmployeeNo == req.EmployeeNo))
            return (null, "员工编号已存在");

        req.CreatedAt = DateTime.UtcNow;
        req.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(req.Status)) req.Status = "active";
        db.Employees.Add(req);
        await db.SaveChangesAsync();
        return (req, null);
    }

    /// <summary>从 OA Employee 入职事件镜像创建（H5 入职流程联通）</summary>
    public async Task<(HrmEmployee? employee, string? error)> CreateFromOaAsync(
        long oaEmployeeId, long? umcUserId, string fullName, string? department, string? position, string? phone, string? email)
    {
        var existing = await db.Employees.FirstOrDefaultAsync(x => x.OaEmployeeId == oaEmployeeId);
        if (existing != null) return (existing, null); // 幂等

        var emp = new HrmEmployee
        {
            OaEmployeeId = oaEmployeeId,
            UmcUserId = umcUserId,
            EmployeeNo = $"EMP-{DateTime.UtcNow:yyyyMMdd}-{oaEmployeeId:D4}",
            FullName = fullName,
            Department = department,
            Position = position,
            Phone = phone,
            Email = email,
            EmploymentType = "full_time",
            HiredAt = DateTime.UtcNow,
            Status = "probation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Employees.Add(emp);
        await db.SaveChangesAsync();
        return (emp, null);
    }

    public async Task<(HrmEmployee? employee, string? error)> ResignAsync(long id, DateTime resignedAt)
    {
        var emp = await db.Employees.FindAsync(id);
        if (emp == null) return (null, "员工不存在");
        if (emp.Status == "resigned") return (null, "已离职");
        emp.Status = "resigned";
        emp.ResignedAt = resignedAt;
        emp.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (emp, null);
    }
}
