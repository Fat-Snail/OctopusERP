using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;
using OctopusFIN.Api.Tax;

namespace OctopusFIN.Api.Services;

/// <summary>税务与发票服务：开票/查验/红冲/纳税申报，通过 ITaxAdapter 对接外部平台</summary>
public class TaxService(FinDbContext db, ITaxAdapter taxAdapter)
{
    // ===== 发票 =====

    public async Task<(List<FinInvoice> rows, int total)> ListInvoicesAsync(
        string? status, string? invoiceType, int pageNum, int pageSize)
    {
        var q = db.Invoices.Include(x => x.Items).AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrEmpty(invoiceType)) q = q.Where(x => x.InvoiceType == invoiceType);
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(x => x.InvoiceDate)
            .Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows, total);
    }

    public async Task<FinInvoice?> GetInvoiceAsync(long id) =>
        await db.Invoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.InvoiceId == id);

    /// <summary>开具电子发票：创建发票记录 + 调用税务适配器开票 + 回填外部 ID</summary>
    public async Task<(FinInvoice? invoice, string? error)> IssueInvoiceAsync(InvoiceIssueInput input, long createdBy)
    {
        if (string.IsNullOrWhiteSpace(input.CustomerName)) return (null, "客户名称不能为空");
        if (input.Amount <= 0) return (null, "金额必须大于 0");

        var invoiceNo = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var taxAmount = Math.Round(input.Amount * input.TaxRate, 2);
        var totalAmount = input.Amount + taxAmount;

        var invoice = new FinInvoice
        {
            InvoiceNo = invoiceNo,
            InvoiceType = input.InvoiceType ?? "invoice",
            InvoiceCategory = input.InvoiceCategory ?? "output",
            CustomerName = input.CustomerName,
            CustomerTaxNo = input.CustomerTaxNo ?? "",
            Amount = input.Amount,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            TaxRate = input.TaxRate,
            Currency = input.Currency ?? "CNY",
            InvoiceDate = input.InvoiceDate ?? DateTime.UtcNow,
            Status = "issued",
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var item in input.Items ?? new())
        {
            invoice.Items.Add(new FinInvoiceItem
            {
                ProductName = item.ProductName,
                Specification = item.Specification ?? "",
                Unit = item.Unit ?? "",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Amount,
                TaxRate = item.TaxRate,
                TaxAmount = Math.Round(item.Amount * item.TaxRate, 2),
            });
        }

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        // 调用税务适配器开票
        var result = await taxAdapter.IssueInvoiceAsync(new TaxIssueRequest(
            invoice.InvoiceNo, invoice.InvoiceType, invoice.CustomerName, invoice.CustomerTaxNo,
            invoice.Amount, invoice.TaxRate, invoice.TaxAmount, invoice.TotalAmount,
            invoice.Currency, invoice.InvoiceDate,
            invoice.Items.Select(i => new TaxItemLine(i.ProductName, i.Specification, i.Unit, i.Quantity, i.UnitPrice, i.Amount, i.TaxRate, i.TaxAmount)).ToList()));

        if (!result.Success)
        {
            invoice.Status = "void";
            await db.SaveChangesAsync();
            return (invoice, $"开票失败：{result.Message}");
        }

        invoice.ExternalInvoiceId = result.ExternalInvoiceId;
        await db.SaveChangesAsync();
        return (invoice, null);
    }

    /// <summary>查验发票</summary>
    public async Task<(bool valid, string? message)> VerifyInvoiceAsync(string invoiceNo)
    {
        var result = await taxAdapter.VerifyInvoiceAsync(invoiceNo);
        if (result.Valid)
        {
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.InvoiceNo == invoiceNo);
            if (invoice != null && invoice.Status == "issued")
                invoice.Status = "verified";
                await db.SaveChangesAsync();
            return (true, "发票有效");
        }
        return (false, $"发票不存在或无效：{result.Message}");
    }

    /// <summary>红冲发票</summary>
    public async Task<(bool success, string? error)> RedFlushAsync(string originalInvoiceNo)
    {
        var original = await db.Invoices.FirstOrDefaultAsync(x => x.InvoiceNo == originalInvoiceNo);
        if (original == null) return (false, "原发票不存在");
        if (original.Status == "red_flushed") return (false, "该发票已红冲");

        var result = await taxAdapter.RedFlushAsync(originalInvoiceNo);
        if (!result.Success) return (false, $"红冲失败：{result.Message}");

        // 创建红冲发票记录
        var redInvoice = new FinInvoice
        {
            InvoiceNo = result.RedInvoiceNo ?? $"RED-{originalInvoiceNo}",
            InvoiceType = "red",
            InvoiceCategory = original.InvoiceCategory,
            CustomerName = original.CustomerName,
            CustomerTaxNo = original.CustomerTaxNo,
            Amount = -original.Amount,
            TaxAmount = -original.TaxAmount,
            TotalAmount = -original.TotalAmount,
            TaxRate = original.TaxRate,
            Currency = original.Currency,
            InvoiceDate = DateTime.UtcNow,
            Status = "issued",
            CreatedAt = DateTime.UtcNow,
        };
        db.Invoices.Add(redInvoice);
        original.Status = "red_flushed";
        await db.SaveChangesAsync();
        return (true, null);
    }

    // ===== 纳税申报 =====

    public async Task<(List<FinTaxReturn> rows, int total)> ListTaxReturnsAsync(
        string? taxType, string? status, int pageNum, int pageSize)
    {
        var q = db.TaxReturns.AsQueryable();
        if (!string.IsNullOrEmpty(taxType)) q = q.Where(x => x.TaxType == taxType);
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows, total);
    }

    /// <summary>创建纳税申报单并提交</summary>
    public async Task<(FinTaxReturn? ret, string? error)> FileTaxReturnAsync(TaxFilingInput input)
    {
        if (string.IsNullOrWhiteSpace(input.TaxType)) return (null, "税种不能为空");

        var ret = new FinTaxReturn
        {
            TaxType = input.TaxType,
            PeriodId = input.PeriodId ?? 0,
            PeriodCode = input.PeriodCode ?? "",
            TaxableAmount = input.TaxableAmount,
            TaxPayable = input.TaxPayable,
            TaxPaid = input.TaxPaid,
            TaxRefund = input.TaxRefund,
            Status = "draft",
            CreatedAt = DateTime.UtcNow,
        };
        db.TaxReturns.Add(ret);
        await db.SaveChangesAsync();

        var result = await taxAdapter.FileTaxReturnAsync(new TaxFilingRequest(
            ret.TaxType, ret.PeriodCode, ret.TaxableAmount, ret.TaxPayable, ret.TaxPaid, ret.TaxRefund));

        if (!result.Success)
        {
            ret.Status = "draft";
            await db.SaveChangesAsync();
            return (ret, $"申报提交失败：{result.Message}");
        }

        ret.ExternalReturnId = result.ExternalReturnId;
        ret.Status = "filed";
        ret.FiledAt = result.FiledAt;
        await db.SaveChangesAsync();
        return (ret, null);
    }

    /// <summary>当前适配器信息（供诊断）</summary>
    public (string AdapterCode, bool IsMock) GetAdapterInfo() => (taxAdapter.AdapterCode, taxAdapter.IsMock);
}

public record InvoiceIssueInput(
    string CustomerName,
    string? CustomerTaxNo,
    decimal Amount,
    decimal TaxRate,
    string? Currency,
    string? InvoiceType,
    string? InvoiceCategory,
    DateTime? InvoiceDate,
    List<InvoiceItemInput>? Items);

public record InvoiceItemInput(
    string ProductName,
    string? Specification,
    string? Unit,
    int Quantity,
    decimal UnitPrice,
    decimal Amount,
    decimal TaxRate);

public record TaxFilingInput(
    string TaxType,
    string PeriodCode,
    long? PeriodId,
    decimal TaxableAmount,
    decimal TaxPayable,
    decimal TaxPaid,
    decimal TaxRefund);
