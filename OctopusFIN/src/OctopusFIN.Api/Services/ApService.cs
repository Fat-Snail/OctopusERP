using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

/// <summary>应付（MES 入库 / OA 报销 → 自动生成 AP + 凭证）</summary>
public class ApService(FinDbContext db, VoucherService voucherSvc)
{
    public async Task<List<FinApInvoice>> GetListAsync(string? status)
    {
        var q = db.ApInvoices.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        return await q.OrderByDescending(x => x.InvoiceDate).ToListAsync();
    }

    public async Task<FinApInvoice?> GetByIdAsync(long id) =>
        await db.ApInvoices.FirstOrDefaultAsync(x => x.InvoiceId == id);

    /// <summary>从 MES 入库事件生成 AP 发票（自动联动凭证：借 库存商品 / 贷 应付账款）</summary>
    public async Task<(FinApInvoice? invoice, string? error)> CreateFromPurchaseAsync(
        long mesPurchaseOrderId, long? supplierId, string supplierName, decimal amount, long createdBy)
    {
        if (amount <= 0) return (null, "金额必须 > 0");

        var existing = await db.ApInvoices.FirstOrDefaultAsync(x => x.MesPurchaseOrderId == mesPurchaseOrderId);
        if (existing != null) return (existing, null); // 幂等

        var invoice = new FinApInvoice
        {
            InvoiceNo = $"AP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            MesPurchaseOrderId = mesPurchaseOrderId,
            SupplierId = supplierId,
            SupplierName = supplierName,
            Amount = amount,
            PaidAmount = 0,
            Currency = "CNY",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = "open",
            CreatedAt = DateTime.UtcNow,
        };
        db.ApInvoices.Add(invoice);
        await db.SaveChangesAsync();

        // 自动生成凭证：借 库存商品(1405) / 贷 应付账款(2202)
        var goodsAcct = await db.Accounts.FirstAsync(a => a.AccountCode == "1405");
        var apAcct = await db.Accounts.FirstAsync(a => a.AccountCode == "2202");

        var (voucher, vErr) = await voucherSvc.CreateAsync(new VoucherInput(
            null, DateTime.UtcNow, "normal",
            $"采购入库：{supplierName} - 采购单 {mesPurchaseOrderId}",
            "mes-ap", invoice.InvoiceNo,
            new List<VoucherEntryInput>
            {
                new(goodsAcct.AccountId, "采购入库", amount, 0, "CNY"),
                new(apAcct.AccountId, "应付货款", 0, amount, "CNY"),
            }), createdBy);

        if (voucher != null)
        {
            invoice.VoucherId = voucher.VoucherId;
            await db.SaveChangesAsync();
        }

        return (invoice, vErr);
    }

    /// <summary>从 OA 报销审批通过事件生成费用凭证（借 管理费用 / 贷 银行存款）</summary>
    public async Task<(FinVoucher? voucher, string? error)> CreateFromExpenseAsync(
        long oaApprovalId, decimal amount, string? summary, long createdBy)
    {
        if (amount <= 0) return (null, "金额必须 > 0");

        var existing = await db.Vouchers.FirstOrDefaultAsync(v =>
            v.Source == "oa-expense" && v.SourceRef == oaApprovalId.ToString());
        if (existing != null) return (existing, null); // 幂等

        var expAcct = await db.Accounts.FirstAsync(a => a.AccountCode == "6602");
        var bankAcct = await db.Accounts.FirstAsync(a => a.AccountCode == "1002");

        var (voucher, vErr) = await voucherSvc.CreateAsync(new VoucherInput(
            null, DateTime.UtcNow, "normal",
            summary ?? $"报销支出：审批 {oaApprovalId}",
            "oa-expense", oaApprovalId.ToString(),
            new List<VoucherEntryInput>
            {
                new(expAcct.AccountId, "报销支出", amount, 0, "CNY"),
                new(bankAcct.AccountId, "银行付款", 0, amount, "CNY"),
            }), createdBy);

        return (voucher, vErr);
    }

    public async Task<(FinApPayment? payment, string? error)> RegisterPaymentAsync(
        long invoiceId, decimal amount, string? paymentMethod, string? bankReference, long createdBy)
    {
        var invoice = await db.ApInvoices.FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);
        if (invoice == null) return (null, "应付发票不存在");
        if (amount <= 0) return (null, "金额必须 > 0");
        if (invoice.PaidAmount + amount > invoice.Amount)
            return (null, $"付款超过应付余额（剩余 {invoice.Amount - invoice.PaidAmount}）");

        var payment = new FinApPayment
        {
            PaymentNo = $"PAY-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            InvoiceId = invoiceId,
            Amount = amount,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = paymentMethod,
            BankReference = bankReference,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };
        db.ApPayments.Add(payment);

        invoice.PaidAmount += amount;
        invoice.Status = invoice.PaidAmount >= invoice.Amount ? "closed" : "partial";

        await db.SaveChangesAsync();
        return (payment, null);
    }
}
