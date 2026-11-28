namespace OctopusFIN.Api.Tax;

/// <summary>
/// 税务平台适配器契约。凭证到位后实现真实平台适配器（如金税/航信），
/// 当前由 MockTaxAdapter 提供确定性沙箱，覆盖全部接口分支。
/// </summary>
public interface ITaxAdapter
{
    /// <summary>适配器编码（如 "mock" / "jinshui"）</summary>
    string AdapterCode { get; }

    /// <summary>是否为确定性 Mock 沙箱（凭证未到位时）</summary>
    bool IsMock { get; }

    /// <summary>开具电子发票（蓝票），返回外部发票 ID</summary>
    Task<TaxIssueResult> IssueInvoiceAsync(TaxIssueRequest request, CancellationToken ct = default);

    /// <summary>查验发票真伪，返回是否有效 + 原始信息</summary>
    Task<TaxVerifyResult> VerifyInvoiceAsync(string invoiceNo, CancellationToken ct = default);

    /// <summary>红冲发票，返回红冲结果</summary>
    Task<TaxRedFlushResult> RedFlushAsync(string originalInvoiceNo, CancellationToken ct = default);

    /// <summary>提交纳税申报，返回申报回执</summary>
    Task<TaxFilingResult> FileTaxReturnAsync(TaxFilingRequest request, CancellationToken ct = default);
}

public record TaxIssueRequest(
    string InvoiceNo,
    string InvoiceType,
    string CustomerName,
    string CustomerTaxNo,
    decimal Amount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    string Currency,
    DateTime InvoiceDate,
    List<TaxItemLine> Items);

public record TaxItemLine(
    string ProductName,
    string Specification,
    string Unit,
    int Quantity,
    decimal UnitPrice,
    decimal Amount,
    decimal TaxRate,
    decimal TaxAmount);

public record TaxIssueResult(
    bool Success,
    string? ExternalInvoiceId,
    string? Message,
    DateTime CreatedAt);

public record TaxVerifyResult(
    bool Valid,
    string? OriginalInvoiceNo,
    decimal? Amount,
    string? Message);

public record TaxRedFlushResult(
    bool Success,
    string? RedInvoiceNo,
    string? Message);

public record TaxFilingRequest(
    string TaxType,
    string PeriodCode,
    decimal TaxableAmount,
    decimal TaxPayable,
    decimal TaxPaid,
    decimal TaxRefund);

public record TaxFilingResult(
    bool Success,
    string? ExternalReturnId,
    string? Message,
    DateTime FiledAt);
