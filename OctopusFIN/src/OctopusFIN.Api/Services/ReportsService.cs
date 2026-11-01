using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

public class ReportsService(FinDbContext db)
{
    /// <summary>试算平衡表（Trial Balance）：每个科目的发生额（借贷）+ 余额方向</summary>
    public async Task<TrialBalanceResponse> GetTrialBalanceAsync(long? periodId)
    {
        var entries = db.VoucherEntries
            .Join(db.Vouchers, e => e.VoucherId, v => v.VoucherId, (e, v) => new { e, v })
            .Where(x => x.v.Status == "posted");

        if (periodId.HasValue)
            entries = entries.Where(x => x.v.PeriodId == periodId.Value);

        var rows = await entries
            .GroupBy(x => new { x.e.AccountId, x.e.AccountCode, x.e.AccountName })
            .Select(g => new TrialBalanceRow
            {
                AccountId = g.Key.AccountId,
                AccountCode = g.Key.AccountCode,
                AccountName = g.Key.AccountName,
                DebitTotal = g.Sum(x => x.e.DebitAmount),
                CreditTotal = g.Sum(x => x.e.CreditAmount),
            })
            .ToListAsync();

        // 余额方向：资产/费用借方余额；负债/权益/收入贷方余额
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.AccountId);
        foreach (var r in rows)
        {
            if (accounts.TryGetValue(r.AccountId, out var a))
            {
                r.Direction = a.Direction;
                r.Balance = a.Direction == "debit" ? r.DebitTotal - r.CreditTotal : r.CreditTotal - r.DebitTotal;
            }
        }

        var totalDebit = rows.Sum(r => r.DebitTotal);
        var totalCredit = rows.Sum(r => r.CreditTotal);

        return new TrialBalanceResponse
        {
            Rows = rows.OrderBy(r => r.AccountCode).ToList(),
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            IsBalanced = totalDebit == totalCredit,
        };
    }
}

public class TrialBalanceRow
{
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Direction { get; set; } = "debit";
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }
    public decimal Balance { get; set; }
}

public class TrialBalanceResponse
{
    public List<TrialBalanceRow> Rows { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced { get; set; }
}
