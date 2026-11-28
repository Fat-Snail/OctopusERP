namespace OctopusFIN.Api.Tax;

/// <summary>
/// 税务平台确定性 Mock 沙箱。凭证未到位时覆盖全部接口分支，
/// 凭证到位后实现真实 ITaxAdapter（金税/航信）替换本类，业务代码零改动。
/// </summary>
public class MockTaxAdapter : ITaxAdapter
{
    public string AdapterCode => "mock";
    public bool IsMock => true;

    public Task<TaxIssueResult> IssueInvoiceAsync(TaxIssueRequest request, CancellationToken ct = default)
    {
        // 确定性：以发票号哈希生成稳定的外部 ID
        var externalId = $"MOCK-INV-{request.InvoiceNo}";
        return Task.FromResult(new TaxIssueResult(
            Success: true,
            ExternalInvoiceId: externalId,
            Message: "Mock 沙箱：发票开具成功",
            CreatedAt: DateTime.UtcNow));
    }

    public Task<TaxVerifyResult> VerifyInvoiceAsync(string invoiceNo, CancellationToken ct = default)
    {
        // 确定性：发票号以 "MOCK" 开头视为已开具
        var valid = invoiceNo.StartsWith("MOCK-INV-", StringComparison.OrdinalIgnoreCase)
                    || invoiceNo.StartsWith("INV-", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(new TaxVerifyResult(
            Valid: valid,
            OriginalInvoiceNo: invoiceNo,
            Amount: null,
            Message: valid ? "Mock 沙箱：发票有效" : "Mock 沙箱：发票不存在"));
    }

    public Task<TaxRedFlushResult> RedFlushAsync(string originalInvoiceNo, CancellationToken ct = default)
    {
        var redNo = $"RED-{originalInvoiceNo}";
        return Task.FromResult(new TaxRedFlushResult(
            Success: true,
            RedInvoiceNo: redNo,
            Message: "Mock 沙箱：红冲成功"));
    }

    public Task<TaxFilingResult> FileTaxReturnAsync(TaxFilingRequest request, CancellationToken ct = default)
    {
        var externalId = $"MOCK-RET-{request.TaxType}-{request.PeriodCode}";
        return Task.FromResult(new TaxFilingResult(
            Success: true,
            ExternalReturnId: externalId,
            Message: "Mock 沙箱：申报提交成功",
            FiledAt: DateTime.UtcNow));
    }
}
