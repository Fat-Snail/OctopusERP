using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

public class PeriodService(FinDbContext db)
{
    public async Task<List<FinPeriod>> GetListAsync() =>
        await db.Periods.OrderByDescending(x => x.PeriodCode).ToListAsync();

    public async Task<FinPeriod?> GetCurrentAsync() =>
        await db.Periods.OrderByDescending(x => x.PeriodCode).FirstOrDefaultAsync(x => x.Status == "open");

    /// <summary>月结：将期间状态改为 closed，禁止再录入凭证</summary>
    public async Task<(FinPeriod? period, string? error)> CloseAsync(long periodId, long userId)
    {
        var period = await db.Periods.FirstOrDefaultAsync(x => x.PeriodId == periodId);
        if (period == null) return (null, "期间不存在");
        if (period.Status == "closed") return (null, "期间已关账");

        // 校验：期间内无草稿凭证
        var draftCount = await db.Vouchers.CountAsync(v => v.PeriodId == periodId && v.Status == "draft");
        if (draftCount > 0)
            return (null, $"期间内有 {draftCount} 张草稿凭证未过账，无法关账");

        period.Status = "closed";
        period.ClosedAt = DateTime.UtcNow;
        period.ClosedBy = userId;
        await db.SaveChangesAsync();
        return (period, null);
    }

    /// <summary>反结账（管理员用，将 closed 改回 open）</summary>
    public async Task<(FinPeriod? period, string? error)> ReopenAsync(long periodId)
    {
        var period = await db.Periods.FirstOrDefaultAsync(x => x.PeriodId == periodId);
        if (period == null) return (null, "期间不存在");
        if (period.Status == "open") return (null, "期间已是开放状态");
        period.Status = "open";
        period.ClosedAt = null;
        period.ClosedBy = null;
        await db.SaveChangesAsync();
        return (period, null);
    }
}
