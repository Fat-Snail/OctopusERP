using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

public class VoucherService(FinDbContext db)
{
    /// <summary>录入凭证（自动校验借贷平衡 + 期间是否开放）</summary>
    public async Task<(FinVoucher? voucher, string? error)> CreateAsync(VoucherInput input, long createdBy)
    {
        if (input.Entries == null || input.Entries.Count < 2)
            return (null, "凭证至少需要 2 条分录");

        var totalDebit = input.Entries.Sum(e => e.DebitAmount);
        var totalCredit = input.Entries.Sum(e => e.CreditAmount);
        if (totalDebit != totalCredit)
            return (null, $"借贷不平：借={totalDebit}，贷={totalCredit}");
        if (totalDebit == 0)
            return (null, "借方与贷方金额不能同时为 0");

        // 校验每条分录借贷不能同时有金额
        foreach (var (e, idx) in input.Entries.Select((e, i) => (e, i + 1)))
        {
            if (e.DebitAmount > 0 && e.CreditAmount > 0)
                return (null, $"第 {idx} 行：借贷不能同时有金额");
            if (e.DebitAmount < 0 || e.CreditAmount < 0)
                return (null, $"第 {idx} 行：金额必须 ≥ 0");
        }

        var period = input.PeriodId.HasValue
            ? await db.Periods.FirstOrDefaultAsync(p => p.PeriodId == input.PeriodId.Value)
            : await GetOrCreateCurrentPeriodAsync();
        if (period == null) return (null, "期间不存在");
        if (period.Status == "closed") return (null, $"期间 {period.PeriodCode} 已关账，禁止录入");

        var accountIds = input.Entries.Select(e => e.AccountId).Distinct().ToList();
        var accounts = await db.Accounts.Where(a => accountIds.Contains(a.AccountId)).ToDictionaryAsync(a => a.AccountId);
        foreach (var e in input.Entries)
        {
            if (!accounts.ContainsKey(e.AccountId)) return (null, $"科目 {e.AccountId} 不存在");
        }

        var voucherDate = input.VoucherDate ?? DateTime.UtcNow;
        var voucherNo = $"V-{voucherDate:yyyyMM}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var voucher = new FinVoucher
        {
            VoucherNo = voucherNo,
            PeriodId = period.PeriodId,
            VoucherDate = voucherDate,
            VoucherType = input.VoucherType ?? "normal",
            Summary = input.Summary ?? "",
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            Status = "draft",
            Source = input.Source ?? "manual",
            SourceRef = input.SourceRef,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };

        var lineOrder = 0;
        foreach (var e in input.Entries)
        {
            var acct = accounts[e.AccountId];
            voucher.Entries.Add(new FinVoucherEntry
            {
                LineOrder = ++lineOrder,
                AccountId = e.AccountId,
                AccountCode = acct.AccountCode,
                AccountName = acct.AccountName,
                Summary = e.Summary ?? "",
                DebitAmount = e.DebitAmount,
                CreditAmount = e.CreditAmount,
                Currency = e.Currency ?? "CNY",
            });
        }

        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();
        return (voucher, null);
    }

    /// <summary>过账（draft → posted）</summary>
    public async Task<(FinVoucher? voucher, string? error)> PostAsync(long voucherId)
    {
        var v = await db.Vouchers.Include(x => x.Entries).FirstOrDefaultAsync(x => x.VoucherId == voucherId);
        if (v == null) return (null, "凭证不存在");
        if (v.Status != "draft") return (null, $"凭证状态 {v.Status} 不能过账");

        var period = await db.Periods.FirstOrDefaultAsync(p => p.PeriodId == v.PeriodId);
        if (period == null || period.Status == "closed")
            return (null, "期间已关账或不存在");

        v.Status = "posted";
        v.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (v, null);
    }

    /// <summary>作废（仅未过账可作废）</summary>
    public async Task<(FinVoucher? voucher, string? error)> VoidAsync(long voucherId)
    {
        var v = await db.Vouchers.FirstOrDefaultAsync(x => x.VoucherId == voucherId);
        if (v == null) return (null, "凭证不存在");
        if (v.Status == "posted") return (null, "已过账凭证不能直接作废，请使用红字冲销");
        v.Status = "void";
        await db.SaveChangesAsync();
        return (v, null);
    }

    public async Task<(List<FinVoucher> rows, int total)> GetListAsync(long? periodId, string? status, string? source, int pageNum, int pageSize)
    {
        var q = db.Vouchers.Include(x => x.Entries).AsQueryable();
        if (periodId.HasValue) q = q.Where(x => x.PeriodId == periodId.Value);
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrEmpty(source)) q = q.Where(x => x.Source == source);
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(x => x.VoucherDate).ThenByDescending(x => x.VoucherId)
            .Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows, total);
    }

    public async Task<FinVoucher?> GetByIdAsync(long id) =>
        await db.Vouchers.Include(x => x.Entries).FirstOrDefaultAsync(x => x.VoucherId == id);

    public async Task<FinPeriod> GetOrCreateCurrentPeriodAsync()
    {
        var now = DateTime.UtcNow;
        var code = $"{now.Year}-{now.Month:D2}";
        var p = await db.Periods.FirstOrDefaultAsync(x => x.PeriodCode == code);
        if (p != null) return p;
        p = new FinPeriod { PeriodCode = code, Year = now.Year, Month = now.Month, Status = "open" };
        db.Periods.Add(p);
        await db.SaveChangesAsync();
        return p;
    }
}

public record VoucherInput(
    long? PeriodId,
    DateTime? VoucherDate,
    string? VoucherType,
    string? Summary,
    string? Source,
    string? SourceRef,
    List<VoucherEntryInput> Entries);

public record VoucherEntryInput(
    long AccountId,
    string? Summary,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Currency);
