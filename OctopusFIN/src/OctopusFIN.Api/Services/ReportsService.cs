using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

public class ReportsService(FinDbContext db)
{
    // ============ 辅助：构建带维度过滤的已过账分录查询 ============

    /// <summary>已过账凭证的分录投影（带组织/利润中心维度过滤），用于所有报表</summary>
    private IQueryable<PostedEntryDto> PostedEntries(
        long? periodId, long? organizationId, long? profitCenterId)
    {
        var q = db.VoucherEntries
            .Join(db.Vouchers, e => e.VoucherId, v => v.VoucherId, (e, v) => new { e, v })
            .Where(x => x.v.Status == "posted");

        if (periodId.HasValue) q = q.Where(x => x.v.PeriodId == periodId.Value);
        if (organizationId.HasValue) q = q.Where(x => x.v.OrganizationId == organizationId.Value);
        if (profitCenterId.HasValue) q = q.Where(x => x.e.ProfitCenterId == profitCenterId.Value);

        return q.Select(x => new PostedEntryDto
        {
            AccountId = x.e.AccountId,
            AccountCode = x.e.AccountCode,
            AccountName = x.e.AccountName,
            DebitAmount = x.e.DebitAmount,
            CreditAmount = x.e.CreditAmount,
            ProfitCenterId = x.e.ProfitCenterId,
        });
    }

    /// <summary>按科目聚合借贷发生额（含维度过滤）</summary>
    private async Task<Dictionary<long, (decimal Debit, decimal Credit)>> AggregateByAccountAsync(
        long? periodId, long? organizationId, long? profitCenterId)
    {
        var rows = await PostedEntries(periodId, organizationId, profitCenterId)
            .GroupBy(x => x.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToListAsync();
        return rows.ToDictionary(r => r.AccountId, r => (r.Debit, r.Credit));
    }

    // ============ 试算平衡表（保留 + 扩展维度过滤）============

    public async Task<TrialBalanceResponse> GetTrialBalanceAsync(
        long? periodId, long? organizationId = null, long? profitCenterId = null)
    {
        var rows = await PostedEntries(periodId, organizationId, profitCenterId)
            .GroupBy(x => new { x.AccountId, x.AccountCode, x.AccountName })
            .Select(g => new TrialBalanceRow
            {
                AccountId = g.Key.AccountId,
                AccountCode = g.Key.AccountCode,
                AccountName = g.Key.AccountName,
                DebitTotal = g.Sum(x => x.DebitAmount),
                CreditTotal = g.Sum(x => x.CreditAmount),
            })
            .ToListAsync();

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

    // ============ 资产负债表 ============

    public async Task<BalanceSheetResponse> GetBalanceSheetAsync(
        long? periodId, long? organizationId = null, long? profitCenterId = null)
    {
        var accounts = await db.Accounts.ToListAsync();
        var aggregates = await AggregateByAccountAsync(periodId, organizationId, profitCenterId);

        var assetRows = new List<BalanceSheetRow>();
        var liabilityRows = new List<BalanceSheetRow>();
        decimal assetTotal = 0, liabilityTotal = 0;
        decimal revenueNet = 0, expenseNet = 0;

        foreach (var acct in accounts.OrderBy(a => a.AccountCode))
        {
            if (!aggregates.TryGetValue(acct.AccountId, out var agg)) continue;
            var balance = acct.Direction == "debit" ? agg.Debit - agg.Credit : agg.Credit - agg.Debit;
            if (balance == 0) continue;

            var row = new BalanceSheetRow
            {
                AccountCode = acct.AccountCode,
                AccountName = acct.AccountName,
                Balance = balance,
            };
            switch (acct.AccountType)
            {
                case "asset":
                    assetTotal += balance;
                    assetRows.Add(row);
                    break;
                case "liability":
                case "equity":
                    liabilityTotal += balance;
                    liabilityRows.Add(row);
                    break;
                case "revenue":
                    revenueNet += balance; // 贷方方向，正值
                    break;
                case "expense":
                    expenseNet += balance; // 借方方向，正值
                    break;
            }
        }

        // 未结转损益（收入 - 费用）计入本年利润，保证 资产 = 负债 + 权益
        var retainedProfit = revenueNet - expenseNet;
        if (retainedProfit != 0)
        {
            liabilityRows.Add(new BalanceSheetRow
            {
                AccountCode = "4103",
                AccountName = "本年利润（未结转）",
                Balance = retainedProfit,
            });
            liabilityTotal += retainedProfit;
        }

        return new BalanceSheetResponse
        {
            Assets = assetRows,
            AssetsTotal = assetTotal,
            Liabilities = liabilityRows,
            LiabilitiesTotal = liabilityTotal,
            EquityTotal = liabilityTotal,
            IsBalanced = assetTotal == liabilityTotal,
            PeriodId = periodId,
            OrganizationId = organizationId,
            ProfitCenterId = profitCenterId,
        };
    }

    // ============ 利润表（损益表）============

    public async Task<IncomeStatementResponse> GetIncomeStatementAsync(
        long? periodId, long? organizationId = null, long? profitCenterId = null)
    {
        var accounts = await db.Accounts.ToListAsync();
        var aggregates = await AggregateByAccountAsync(periodId, organizationId, profitCenterId);

        var revenueRows = new List<IncomeStatementRow>();
        var expenseRows = new List<IncomeStatementRow>();
        decimal revenueTotal = 0, expenseTotal = 0;

        foreach (var acct in accounts.OrderBy(a => a.AccountCode))
        {
            if (!aggregates.TryGetValue(acct.AccountId, out var agg)) continue;
            var net = acct.Direction == "credit" ? agg.Credit - agg.Debit : agg.Debit - agg.Credit;
            if (net == 0) continue;

            var row = new IncomeStatementRow
            {
                AccountCode = acct.AccountCode,
                AccountName = acct.AccountName,
                Amount = net,
            };
            if (acct.AccountType == "revenue")
            {
                revenueTotal += net;
                revenueRows.Add(row);
            }
            else if (acct.AccountType == "expense")
            {
                expenseTotal += net;
                expenseRows.Add(row);
            }
        }

        var netProfit = revenueTotal - expenseTotal;
        return new IncomeStatementResponse
        {
            Revenues = revenueRows,
            RevenueTotal = revenueTotal,
            Expenses = expenseRows,
            ExpenseTotal = expenseTotal,
            NetProfit = netProfit,
            PeriodId = periodId,
            OrganizationId = organizationId,
            ProfitCenterId = profitCenterId,
        };
    }

    // ============ 现金流量表（简化版：按科目分类经营/投资/筹资）============

    public async Task<CashFlowResponse> GetCashFlowAsync(
        long? periodId, long? organizationId = null, long? profitCenterId = null)
    {
        var accounts = await db.Accounts.ToListAsync();
        var aggregates = await AggregateByAccountAsync(periodId, organizationId, profitCenterId);

        decimal operating = 0, investing = 0, financing = 0;
        var detailRows = new List<CashFlowRow>();

        foreach (var acct in accounts.OrderBy(a => a.AccountCode))
        {
            if (!aggregates.TryGetValue(acct.AccountId, out var agg)) continue;
            decimal inflow = agg.Credit, outflow = agg.Debit;
            if (inflow == 0 && outflow == 0) continue;

            string category;
            switch (acct.AccountCode)
            {
                case "1601":
                    category = "investing";
                    investing += inflow - outflow;
                    break;
                case "4001":
                case "4103":
                    category = "financing";
                    financing += inflow - outflow;
                    break;
                default:
                    category = "operating";
                    operating += inflow - outflow;
                    break;
            }

            detailRows.Add(new CashFlowRow
            {
                AccountCode = acct.AccountCode,
                AccountName = acct.AccountName,
                Category = category,
                Inflow = inflow,
                Outflow = outflow,
                Net = inflow - outflow,
            });
        }

        return new CashFlowResponse
        {
            Operating = operating,
            Investing = investing,
            Financing = financing,
            NetChange = operating + investing + financing,
            Rows = detailRows,
            PeriodId = periodId,
            OrganizationId = organizationId,
            ProfitCenterId = profitCenterId,
        };
    }

    // ============ 阿米巴经营报表（按利润中心汇总）============

    public async Task<AmoebaReportResponse> GetAmoebaReportAsync(long? periodId, long? organizationId = null)
    {
        var profitCenters = await db.ProfitCenters
            .Include(x => x.Organization)
            .Where(x => !organizationId.HasValue || x.OrganizationId == organizationId.Value)
            .OrderBy(x => x.CenterCode)
            .ToListAsync();

        var accounts = await db.Accounts.ToListAsync();
        var revenueAccountIds = accounts.Where(a => a.AccountType == "revenue").Select(a => a.AccountId).ToList();
        var expenseAccountIds = accounts.Where(a => a.AccountType == "expense").Select(a => a.AccountId).ToList();

        var entries = await PostedEntries(periodId, organizationId, null).ToListAsync();

        var rows = profitCenters.Select(pc =>
        {
            var pcEntries = entries.Where(x => x.ProfitCenterId == pc.ProfitCenterId).ToList();
            decimal revenue = pcEntries.Where(x => revenueAccountIds.Contains(x.AccountId))
                .Sum(x => x.CreditAmount - x.DebitAmount);
            decimal expense = pcEntries.Where(x => expenseAccountIds.Contains(x.AccountId))
                .Sum(x => x.DebitAmount - x.CreditAmount);
            return new AmoebaRow
            {
                ProfitCenterId = pc.ProfitCenterId,
                CenterCode = pc.CenterCode,
                CenterName = pc.CenterName,
                OrganizationName = pc.Organization?.OrgName,
                Revenue = revenue,
                Expense = expense,
                Profit = revenue - expense,
                ProfitRate = revenue > 0 ? (revenue - expense) / revenue : 0,
            };
        }).ToList();

        var unassignedEntries = entries.Where(x => x.ProfitCenterId == null).ToList();
        decimal unassignedRevenue = unassignedEntries.Where(x => revenueAccountIds.Contains(x.AccountId))
            .Sum(x => x.CreditAmount - x.DebitAmount);
        decimal unassignedExpense = unassignedEntries.Where(x => expenseAccountIds.Contains(x.AccountId))
            .Sum(x => x.DebitAmount - x.CreditAmount);

        return new AmoebaReportResponse
        {
            Rows = rows,
            UnassignedRevenue = unassignedRevenue,
            UnassignedExpense = unassignedExpense,
            UnassignedProfit = unassignedRevenue - unassignedExpense,
            TotalRevenue = rows.Sum(r => r.Revenue) + unassignedRevenue,
            TotalExpense = rows.Sum(r => r.Expense) + unassignedExpense,
            TotalProfit = rows.Sum(r => r.Profit) + unassignedRevenue - unassignedExpense,
            PeriodId = periodId,
            OrganizationId = organizationId,
        };
    }
}

/// <summary>已过账分录的查询投影（纯 DTO，避免 EF 实体追踪冲突）</summary>
public class PostedEntryDto
{
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public long? ProfitCenterId { get; set; }
}

// ============ DTOs ============

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

public class BalanceSheetRow
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public class BalanceSheetResponse
{
    public List<BalanceSheetRow> Assets { get; set; } = new();
    public decimal AssetsTotal { get; set; }
    public List<BalanceSheetRow> Liabilities { get; set; } = new();
    public decimal LiabilitiesTotal { get; set; }
    public decimal EquityTotal { get; set; }
    public bool IsBalanced { get; set; }
    public long? PeriodId { get; set; }
    public long? OrganizationId { get; set; }
    public long? ProfitCenterId { get; set; }
}

public class IncomeStatementRow
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class IncomeStatementResponse
{
    public List<IncomeStatementRow> Revenues { get; set; } = new();
    public decimal RevenueTotal { get; set; }
    public List<IncomeStatementRow> Expenses { get; set; } = new();
    public decimal ExpenseTotal { get; set; }
    public decimal NetProfit { get; set; }
    public long? PeriodId { get; set; }
    public long? OrganizationId { get; set; }
    public long? ProfitCenterId { get; set; }
}

public class CashFlowRow
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Category { get; set; } = "operating";
    public decimal Inflow { get; set; }
    public decimal Outflow { get; set; }
    public decimal Net { get; set; }
}

public class CashFlowResponse
{
    public decimal Operating { get; set; }
    public decimal Investing { get; set; }
    public decimal Financing { get; set; }
    public decimal NetChange { get; set; }
    public List<CashFlowRow> Rows { get; set; } = new();
    public long? PeriodId { get; set; }
    public long? OrganizationId { get; set; }
    public long? ProfitCenterId { get; set; }
}

public class AmoebaRow
{
    public long ProfitCenterId { get; set; }
    public string CenterCode { get; set; } = string.Empty;
    public string CenterName { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public decimal Revenue { get; set; }
    public decimal Expense { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitRate { get; set; }
}

public class AmoebaReportResponse
{
    public List<AmoebaRow> Rows { get; set; } = new();
    public decimal UnassignedRevenue { get; set; }
    public decimal UnassignedExpense { get; set; }
    public decimal UnassignedProfit { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal TotalProfit { get; set; }
    public long? PeriodId { get; set; }
    public long? OrganizationId { get; set; }
}
