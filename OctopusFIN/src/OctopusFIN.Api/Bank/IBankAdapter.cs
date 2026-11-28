namespace OctopusFIN.Api.Bank;

/// <summary>
/// 银行直连适配器契约。凭证到位后实现真实银行适配器（如工行/招行开放平台），
/// 当前由 MockBankAdapter 提供确定性沙箱，覆盖全部接口分支。
/// </summary>
public interface IBankAdapter
{
    /// <summary>适配器编码（如 "mock" / "icbc"）</summary>
    string AdapterCode { get; }

    /// <summary>是否为确定性 Mock 沙箱（凭证未到位时）</summary>
    bool IsMock { get; }

    /// <summary>查询账户余额</summary>
    Task<BankBalanceResult> GetBalanceAsync(string accountNo, CancellationToken ct = default);

    /// <summary>拉取对账单明细（指定日期范围）</summary>
    Task<List<BankStatementLine>> FetchStatementsAsync(
        string accountNo, DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>回写收付款结果到银行（付款/收款确认）</summary>
    Task<BankPaymentResult> PushPaymentAsync(BankPaymentRequest request, CancellationToken ct = default);
}

public record BankBalanceResult(
    bool Success,
    string AccountNo,
    decimal Balance,
    string Currency,
    string? Message);

public record BankStatementLine(
    string TransactionNo,
    string Direction,
    decimal Amount,
    DateTime TransactionDate,
    string? Counterparty,
    string? Remark,
    string ExternalTxnId);

public record BankPaymentRequest(
    string AccountNo,
    string Direction,
    decimal Amount,
    string? Counterparty,
    string? Remark,
    DateTime TransactionDate);

public record BankPaymentResult(
    bool Success,
    string? ExternalTxnId,
    string? Message);
