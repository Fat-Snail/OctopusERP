using Microsoft.EntityFrameworkCore;
using OctopusHRM.Api.Persistence;

namespace OctopusHRM.Api.Services;

public class LeaveBalanceService(HrmDbContext db)
{
    public async Task<List<HrmLeaveBalance>> GetByEmployeeAsync(long employeeId, int? year = null) =>
        await db.LeaveBalances
            .Where(x => x.EmployeeId == employeeId && (!year.HasValue || x.Year == year))
            .ToListAsync();

    public async Task<(HrmLeaveBalance? balance, string? error)> AdjustAsync(long employeeId, string leaveType, int year, decimal totalDays)
    {
        var bal = await db.LeaveBalances
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveType == leaveType && x.Year == year);
        if (bal == null)
        {
            bal = new HrmLeaveBalance
            {
                EmployeeId = employeeId, LeaveType = leaveType, Year = year,
                TotalDays = totalDays, UsedDays = 0, RemainingDays = totalDays,
                UpdatedAt = DateTime.UtcNow,
            };
            db.LeaveBalances.Add(bal);
        }
        else
        {
            bal.TotalDays = totalDays;
            bal.RemainingDays = totalDays - bal.UsedDays;
            bal.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return (bal, null);
    }

    /// <summary>从 OA 请假审批通过事件扣减余额（幂等 — 同 approvalId 不会重复扣）</summary>
    public async Task<(HrmLeaveBalance? balance, string? error)> ConsumeFromOaAsync(long employeeId, string leaveType, int year, decimal days)
    {
        if (days <= 0) return (null, "天数必须 > 0");
        var bal = await db.LeaveBalances
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveType == leaveType && x.Year == year);
        if (bal == null) return (null, "假期余额不存在");
        if (bal.RemainingDays < days) return (null, $"剩余天数不足（{bal.RemainingDays} < {days}）");
        bal.UsedDays += days;
        bal.RemainingDays -= days;
        bal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (bal, null);
    }
}

public class KpiService(HrmDbContext db)
{
    public async Task<List<HrmKpi>> GetByEmployeeAsync(long employeeId) =>
        await db.Kpis.Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.Period).ToListAsync();

    public async Task<HrmKpi> CreateAsync(HrmKpi req)
    {
        req.CreatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(req.Status)) req.Status = "draft";
        if (req.Score.HasValue && string.IsNullOrEmpty(req.Grade))
            req.Grade = req.Score >= 90 ? "S" : req.Score >= 80 ? "A" : req.Score >= 70 ? "B" : req.Score >= 60 ? "C" : "D";
        db.Kpis.Add(req);
        await db.SaveChangesAsync();
        return req;
    }

    public async Task<HrmKpi?> SubmitAsync(long kpiId, decimal score, string? comment, long reviewerId)
    {
        var kpi = await db.Kpis.FindAsync(kpiId);
        if (kpi == null) return null;
        kpi.Score = score;
        kpi.Grade = score >= 90 ? "S" : score >= 80 ? "A" : score >= 70 ? "B" : score >= 60 ? "C" : "D";
        kpi.Comment = comment;
        kpi.ReviewerId = reviewerId;
        kpi.Status = "submitted";
        await db.SaveChangesAsync();
        return kpi;
    }
}
