namespace OctopusFIN.Api.Bank;

/// <summary>
/// 银行直连确定性 Mock 沙箱。凭证未到位时覆盖全部接口分支，
/// 凭证到位后实现真实 IBankAdapter（工行/招行开放平台）替换本类，业务代码零改动。
/// </summary>
public class MockBankAdapter : IBankAdapter
{
    public string AdapterCode => "mock";
    public bool IsMock => true;

    public Task<BankBalanceResult> GetBalanceAsync(string accountNo, CancellationToken ct = default)
    {
        // 确定性：余额 = 账户号尾数 × 1000
        var tail = accountNo.Length >= 4 ? accountNo[^4..] : accountNo;
        var balance = tail.Aggregate(0m, (acc, c) => acc + (decimal)(c - '0'));
        balance *= 1000;
        return Task.FromResult(new BankBalanceResult(
            Success: true,
            AccountNo: accountNo,
            Balance: balance,
            Currency: "CNY",
            Message: "Mock 沙箱：余额查询成功"));
    }

    public Task<List<BankStatementLine>> FetchStatementsAsync(
        string accountNo, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        // 确定性：生成 3 条模拟流水
        var lines = new List<BankStatementLine>
        {
            new("MOCK-TXN-001", "credit", 5000m, startDate, "客户A", "货款", $"MOCK-{accountNo}-001"),
            new("MOCK-TXN-002", "debit", 1200m, startDate.AddDays(5), "供应商B", "采购", $"MOCK-{accountNo}-002"),
            new("MOCK-TXN-003", "credit", 3000m, startDate.AddDays(10), "客户C", "货款", $"MOCK-{accountNo}-003"),
        };
        return Task.FromResult(lines);
    }

    public Task<BankPaymentResult> PushPaymentAsync(BankPaymentRequest request, CancellationToken ct = default)
    {
        var externalId = $"MOCK-PAY-{request.AccountNo}-{request.TransactionDate:yyyyMMddHHmmss}";
        return Task.FromResult(new BankPaymentResult(
            Success: true,
            ExternalTxnId: externalId,
            Message: "Mock 沙箱：收付款回写成功"));
    }
}
