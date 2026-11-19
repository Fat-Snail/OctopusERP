using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

/// <summary>
/// Q3-③ 月结服务：结转损益（收入/费用 → 本年利润 4103）+ 月结报告。
/// 结转凭证 voucherType="closing"、Source="closing"，按期间幂等。
/// </summary>
public class ClosingService(FinDbContext db, VoucherService voucherSvc)
{
    /// <summary>结转损益：将期间内 posted 凭证中收入/费用科目的净额清零，转入本年利润。</summary>
    public async Task<(FinVoucher? voucher, string? error)> TransferProfitAsync(long periodId, long userId)
    {
        var period = await db.Periods.FirstOrDefaultAsync(p => p.PeriodId == periodId);
        if (period == null) return (null, "期间不存在");
        if (period.Status == "closed") return (null, "期间已关账，不能重复结转");

        // 幂等：本期间已有未作废的结转凭证则直接复用
        var existing = await db.Vouchers
            .FirstOrDefaultAsync(v => v.PeriodId == periodId && v.Source == "closing" && v.Status != "void");
        if (existing != null) return (existing, null);

        // posted 分录按科目聚合（与试算平衡同口径）
        var rows = await db.VoucherEntries
            .Join(db.Vouchers, e => e.VoucherId, v => v.VoucherId, (e, v) => new { e, v })
            .Where(x => x.v.PeriodId == periodId && x.v.Status == "posted")
            .GroupBy(x => x.e.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(x => x.e.DebitAmount),
                Credit = g.Sum(x => x.e.CreditAmount),
            })
            .ToListAsync();

        var accounts = await db.Accounts.ToDictionaryAsync(a => a.AccountId);
        var profitAcct = accounts.Values.First(a => a.AccountCode == "4103");

        var entries = new List<VoucherEntryInput>();
        foreach (var r in rows)
        {
            if (!accounts.TryGetValue(r.AccountId, out var acct)) continue;
            if (acct.AccountType is not ("revenue" or "expense")) continue;

            // 科目净额（方向感知）：费用借方余额 / 收入贷方余额为正
            var net = acct.Direction == "debit" ? r.Debit - r.Credit : r.Credit - r.Debit;
            if (net == 0) continue;

            // 生成把该科目净额清零的分录，对方科目为本年利润
            if (net > 0)
            {
                if (acct.AccountType == "expense")
                {
                    entries.Add(new(profitAcct.AccountId, $"结转{acct.AccountName}", net, 0, "CNY"));
                    entries.Add(new(acct.AccountId, $"结转{acct.AccountName}", 0, net, "CNY"));
                }
                else
                {
                    entries.Add(new(acct.AccountId, $"结转{acct.AccountName}", net, 0, "CNY"));
                    entries.Add(new(profitAcct.AccountId, $"结转{acct.AccountName}", 0, net, "CNY"));
                }
            }
            else
            {
                // 反向余额（冲销导致），方向镜像
                var abs = -net;
                if (acct.AccountType == "expense")
                {
                    entries.Add(new(acct.AccountId, $"结转{acct.AccountName}", abs, 0, "CNY"));
                    entries.Add(new(profitAcct.AccountId, $"结转{acct.AccountName}", 0, abs, "CNY"));
                }
                else
                {
                    entries.Add(new(profitAcct.AccountId, $"结转{acct.AccountName}", abs, 0, "CNY"));
                    entries.Add(new(acct.AccountId, $"结转{acct.AccountName}", 0, abs, "CNY"));
                }
            }
        }

        if (entries.Count == 0) return (null, null); // 本期无收入/费用发生，无需结转

        var (voucher, err) = await voucherSvc.CreateAsync(new VoucherInput(
            periodId, DateTime.UtcNow, "closing",
            $"结转损益：期间 {period.PeriodCode}", "closing", period.PeriodCode,
            entries), userId);
        if (voucher == null) return (null, err);

        // 结转凭证必须已过账（关账校验要求期间内无草稿）
        var (posted, postErr) = await voucherSvc.PostAsync(voucher.VoucherId);
        if (posted == null) return (null, postErr);

        return (posted, null);
    }

    /// <summary>月结报告：期间凭证统计、损益汇总（不含结转凭证口径）、结转凭证、科目余额快照。</summary>
    public async Task<(MonthEndClosingReport? report, string? error)> GetMonthEndReportAsync(long periodId)
    {
        var period = await db.Periods.FirstOrDefaultAsync(p => p.PeriodId == periodId);
        if (period == null) return (null, "期间不存在");

        var vouchers = await db.Vouchers.Where(v => v.PeriodId == periodId).ToListAsync();
        var postedIds = vouchers.Where(v => v.Status == "posted").Select(v => v.VoucherId).ToHashSet();
        var closingVoucher = vouchers.FirstOrDefault(v => v.Source == "closing" && v.Status == "posted");

        // 全部 posted 分录（含结转）——当前余额快照
        var allEntries = await db.VoucherEntries
            .Join(db.Vouchers, e => e.VoucherId, v => v.VoucherId, (e, v) => new { e, v })
            .Where(x => x.v.PeriodId == periodId && x.v.Status == "posted")
            .Select(x => new { x.e.AccountId, x.e.DebitAmount, x.e.CreditAmount })
            .ToListAsync();

        // 损益口径：排除结转凭证，使关账前后 NetProfit 口径一致
        var profitEntries = await db.VoucherEntries
            .Join(db.Vouchers, e => e.VoucherId, v => v.VoucherId, (e, v) => new { e, v })
            .Where(x => x.v.PeriodId == periodId && x.v.Status == "posted" && x.v.Source != "closing")
            .Select(x => new { x.e.AccountId, x.e.DebitAmount, x.e.CreditAmount })
            .ToListAsync();

        var accounts = await db.Accounts.ToDictionaryAsync(a => a.AccountId);

        decimal revenueTotal = 0, expenseTotal = 0;
        foreach (var r in profitEntries)
        {
            if (!accounts.TryGetValue(r.AccountId, out var acct)) continue;
            if (acct.AccountType == "revenue") revenueTotal += r.CreditAmount - r.DebitAmount;
            else if (acct.AccountType == "expense") expenseTotal += r.DebitAmount - r.CreditAmount;
        }

        // 余额快照（方向感知）
        var grouped = allEntries
            .GroupBy(x => x.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToList();

        var balances = new List<TrialBalanceRow>();
        foreach (var g in grouped)
        {
            if (!accounts.TryGetValue(g.AccountId, out var acct)) continue;
            balances.Add(new TrialBalanceRow
            {
                AccountId = acct.AccountId,
                AccountCode = acct.AccountCode,
                AccountName = acct.AccountName,
                Direction = acct.Direction,
                DebitTotal = g.Debit,
                CreditTotal = g.Credit,
                Balance = acct.Direction == "debit" ? g.Debit - g.Credit : g.Credit - g.Debit,
            });
        }

        var report = new MonthEndClosingReport
        {
            PeriodId = period.PeriodId,
            PeriodCode = period.PeriodCode,
            PeriodStatus = period.Status,
            ClosedAt = period.ClosedAt,
            ClosedBy = period.ClosedBy,
            VoucherTotal = vouchers.Count,
            PostedCount = vouchers.Count(v => v.Status == "posted"),
            DraftCount = vouchers.Count(v => v.Status == "draft"),
            VoidCount = vouchers.Count(v => v.Status == "void"),
            PostedDebit = balances.Sum(b => b.DebitTotal),
            PostedCredit = balances.Sum(b => b.CreditTotal),
            RevenueTotal = revenueTotal,
            ExpenseTotal = expenseTotal,
            NetProfit = revenueTotal - expenseTotal,
            ClosingVoucherNo = closingVoucher?.VoucherNo,
            ClosingAmount = closingVoucher?.TotalDebit,
            BalanceSnapshot = balances.OrderBy(b => b.AccountCode).ToList(),
            TrialBalanceOk = balances.Sum(b => b.DebitTotal) == balances.Sum(b => b.CreditTotal),
        };

        return (report, null);
    }
}

/// <summary>月结报告（Q3-③）</summary>
public class MonthEndClosingReport
{
    public long PeriodId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public string PeriodStatus { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public long? ClosedBy { get; set; }

    public int VoucherTotal { get; set; }
    public int PostedCount { get; set; }
    public int DraftCount { get; set; }
    public int VoidCount { get; set; }

    /// <summary>本期过账发生额（含结转凭证）</summary>
    public decimal PostedDebit { get; set; }
    public decimal PostedCredit { get; set; }

    /// <summary>损益汇总（不含结转凭证口径：关账前后数值一致）</summary>
    public decimal RevenueTotal { get; set; }
    public decimal ExpenseTotal { get; set; }
    public decimal NetProfit { get; set; }

    public string? ClosingVoucherNo { get; set; }
    public decimal? ClosingAmount { get; set; }

    /// <summary>全科目当前余额快照（关账后即为结转后余额）</summary>
    public List<TrialBalanceRow> BalanceSnapshot { get; set; } = new();
    public bool TrialBalanceOk { get; set; }
}
