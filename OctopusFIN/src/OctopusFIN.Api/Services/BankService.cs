using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Bank;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

/// <summary>银企直连服务：账户/对账单/自动对账，通过 IBankAdapter 对接银行开放平台</summary>
public class BankService(FinDbContext db, IBankAdapter bankAdapter)
{
    // ===== 银行账户 =====

    public async Task<(List<FinBankAccount> rows, int total)> ListAccountsAsync(
        string? status, int pageNum, int pageSize)
    {
        var q = db.BankAccounts.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        var total = await q.CountAsync();
        var rows = await q.OrderBy(x => x.AccountNo)
            .Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows, total);
    }

    public async Task<(FinBankAccount? acc, string? error)> CreateAccountAsync(BankAccountInput input)
    {
        if (string.IsNullOrWhiteSpace(input.AccountNo)) return (null, "账号不能为空");
        var exists = await db.BankAccounts.AnyAsync(x => x.AccountNo == input.AccountNo);
        if (exists) return (null, $"账号 {input.AccountNo} 已存在");

        var acc = new FinBankAccount
        {
            AccountNo = input.AccountNo,
            AccountName = input.AccountName ?? input.AccountNo,
            BankName = input.BankName ?? "",
            OrganizationId = input.OrganizationId,
            ProfitCenterId = input.ProfitCenterId,
            Status = input.Status ?? "active",
            AdapterCode = bankAdapter.AdapterCode,
            CreatedAt = DateTime.UtcNow,
        };
        db.BankAccounts.Add(acc);
        await db.SaveChangesAsync();
        return (acc, null);
    }

    // ===== 余额查询 =====

    public async Task<BankBalanceResult> GetBalanceAsync(long accountId)
    {
        var acc = await db.BankAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId);
        if (acc == null) return new BankBalanceResult(false, "", 0, "CNY", "账户不存在");
        return await bankAdapter.GetBalanceAsync(acc.AccountNo);
    }

    // ===== 对账单拉取 =====

    /// <summary>拉取对账单并入库（幂等：按外部流水号去重）</summary>
    public async Task<(int fetched, int skipped, string? error)> FetchStatementsAsync(long accountId, DateTime startDate, DateTime endDate)
    {
        var acc = await db.BankAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId);
        if (acc == null) return (0, 0, "账户不存在");

        var lines = await bankAdapter.FetchStatementsAsync(acc.AccountNo, startDate, endDate);
        int fetched = 0, skipped = 0;

        foreach (var line in lines)
        {
            // 幂等：同一账户 + 同一外部流水号不重复入库
            var exists = await db.BankStatements.AnyAsync(x =>
                x.BankAccountId == accountId && x.ExternalTxnId == line.ExternalTxnId);
            if (exists)
            {
                skipped++;
                continue;
            }

            db.BankStatements.Add(new FinBankStatement
            {
                BankAccountId = accountId,
                TransactionNo = line.TransactionNo,
                Direction = line.Direction,
                Amount = line.Amount,
                TransactionDate = line.TransactionDate,
                Counterparty = line.Counterparty,
                Remark = line.Remark,
                MatchStatus = "unmatched",
                ExternalTxnId = line.ExternalTxnId,
                CreatedAt = DateTime.UtcNow,
            });
            fetched++;
        }

        await db.SaveChangesAsync();
        return (fetched, skipped, null);
    }

    public async Task<(List<FinBankStatement> rows, int total)> ListStatementsAsync(
        long accountId, string? matchStatus, int pageNum, int pageSize)
    {
        var q = db.BankStatements.AsQueryable().Where(x => x.BankAccountId == accountId);
        if (!string.IsNullOrEmpty(matchStatus)) q = q.Where(x => x.MatchStatus == matchStatus);
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(x => x.TransactionDate)
            .Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows, total);
    }

    // ===== 自动对账：银行流水 vs 应收/应付收付款 =====

    /// <summary>
    /// 自动对账：将未匹配的银行流水与应收收款/应付付款匹配。
    /// 简化规则：金额相同 + 方向匹配（入账=收款，扣款=付款）。
    /// </summary>
    public async Task<(int matched, string? error)> AutoReconcileAsync(long accountId)
    {
        var unmatched = await db.BankStatements
            .Where(x => x.BankAccountId == accountId && x.MatchStatus == "unmatched")
            .ToListAsync();
        if (unmatched.Count == 0) return (0, null);

        int matched = 0;

        // 入账（credit）匹配应收收款
        foreach (var stmt in unmatched.Where(x => x.Direction == "credit").ToList())
        {
            var receipt = await db.ArReceipts.FirstOrDefaultAsync(x =>
                x.Amount == stmt.Amount && x.BankReference == null &&
                !db.BankStatements.Any(s => s.MatchedReceiptId == x.ReceiptId));
            if (receipt != null)
            {
                stmt.MatchStatus = "matched";
                stmt.MatchedReceiptId = receipt.ReceiptId;
                receipt.BankReference = stmt.TransactionNo;
                matched++;
            }
        }

        // 扣款（debit）匹配应付付款
        foreach (var stmt in unmatched.Where(x => x.Direction == "debit" && x.MatchStatus == "unmatched").ToList())
        {
            var payment = await db.ApPayments.FirstOrDefaultAsync(x =>
                x.Amount == stmt.Amount && x.BankReference == null &&
                !db.BankStatements.Any(s => s.MatchedPaymentId == x.PaymentId));
            if (payment != null)
            {
                stmt.MatchStatus = "matched";
                stmt.MatchedPaymentId = payment.PaymentId;
                payment.BankReference = stmt.TransactionNo;
                matched++;
            }
        }

        await db.SaveChangesAsync();
        return (matched, null);
    }

    /// <summary>当前适配器信息（供诊断）</summary>
    public (string AdapterCode, bool IsMock) GetAdapterInfo() => (bankAdapter.AdapterCode, bankAdapter.IsMock);
}

public record BankAccountInput(
    string AccountNo,
    string? AccountName,
    string BankName,
    long? OrganizationId,
    long? ProfitCenterId,
    string? Status);
