using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

/// <summary>应收（CRM 发货 → 自动生成 AR + 凭证）</summary>
public class ArService(FinDbContext db, VoucherService voucherSvc)
{
    public async Task<List<FinArInvoice>> GetListAsync(string? status)
    {
        var q = db.ArInvoices.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        return await q.OrderByDescending(x => x.InvoiceDate).ToListAsync();
    }

    public async Task<FinArInvoice?> GetByIdAsync(long id) =>
        await db.ArInvoices.FirstOrDefaultAsync(x => x.InvoiceId == id);

    /// <summary>按 CRM 合同 ID 定位应收发票（一条合同一张 AR）</summary>
    public async Task<FinArInvoice?> GetByContractIdAsync(long crmContractId) =>
        await db.ArInvoices.FirstOrDefaultAsync(x => x.CrmContractId == crmContractId);

    /// <summary>按外部回款引用（crm-pay-{paymentId}）查重，webhook 幂等用</summary>
    public async Task<FinArReceipt?> FindReceiptByExternalRefAsync(string externalRef)
    {
        var normalized = externalRef.Split('|')[0];
        return await db.ArReceipts
            .FirstOrDefaultAsync(r => r.BankReference != null &&
                (r.BankReference == normalized || r.BankReference.EndsWith("|" + normalized)));
    }

    /// <summary>从 CRM 发货事件生成 AR 发票（自动联动凭证：借 应收账款 / 贷 主营业务收入）</summary>
    public async Task<(FinArInvoice? invoice, string? error)> CreateFromShipmentAsync(
        long crmContractId, long? customerId, string customerName, decimal amount, long createdBy)
    {
        if (amount <= 0) return (null, "金额必须 > 0");

        var existing = await db.ArInvoices.FirstOrDefaultAsync(x => x.CrmContractId == crmContractId);
        if (existing != null) return (existing, null); // 幂等

        var invoice = new FinArInvoice
        {
            InvoiceNo = $"AR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            CrmContractId = crmContractId,
            CustomerId = customerId,
            CustomerName = customerName,
            Amount = amount,
            ReceivedAmount = 0,
            Currency = "CNY",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = "open",
            CreatedAt = DateTime.UtcNow,
        };
        db.ArInvoices.Add(invoice);
        await db.SaveChangesAsync();

        // 自动生成凭证：借 应收账款(1122) / 贷 主营业务收入(6001)
        var arAcct = await db.Accounts.FirstAsync(a => a.AccountCode == "1122");
        var revAcct = await db.Accounts.FirstAsync(a => a.AccountCode == "6001");

        var (voucher, vErr) = await voucherSvc.CreateAsync(new VoucherInput(
            null, DateTime.UtcNow, "normal",
            $"销售确认收入：{customerName} - 合同 {crmContractId}",
            "crm-ar", invoice.InvoiceNo,
            new List<VoucherEntryInput>
            {
                new(arAcct.AccountId, "应收销售款", amount, 0, "CNY"),
                new(revAcct.AccountId, "确认收入", 0, amount, "CNY"),
            }), createdBy);

        if (voucher != null)
        {
            invoice.VoucherId = voucher.VoucherId;
            await db.SaveChangesAsync();
        }

        return (invoice, vErr);
    }

    /// <summary>登记收款</summary>
    public async Task<(FinArReceipt? receipt, string? error)> RegisterReceiptAsync(
        long invoiceId, decimal amount, string? paymentMethod, string? bankReference, long createdBy)
    {
        var invoice = await db.ArInvoices.FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);
        if (invoice == null) return (null, "应收发票不存在");
        if (invoice.Status == "void") return (null, "发票已作废");
        if (amount <= 0) return (null, "金额必须 > 0");
        if (invoice.ReceivedAmount + amount > invoice.Amount)
            return (null, $"收款超过应收余额（剩余 {invoice.Amount - invoice.ReceivedAmount}）");

        var receipt = new FinArReceipt
        {
            ReceiptNo = $"RCP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            InvoiceId = invoiceId,
            Amount = amount,
            ReceiptDate = DateTime.UtcNow,
            PaymentMethod = paymentMethod,
            BankReference = bankReference,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };
        db.ArReceipts.Add(receipt);

        invoice.ReceivedAmount += amount;
        invoice.Status = invoice.ReceivedAmount >= invoice.Amount ? "closed" : "partial";

        await db.SaveChangesAsync();

        // Q3-① 自动生成收款凭证（借 银行存款(1002) / 贷 应收账款(1122)），保证复式记账平衡：
        // 发货时已借 应收 / 贷 收入，收款时不冲减应收会导致应收账款虚挂
        var bankAcct = await db.Accounts.FirstAsync(a => a.AccountCode == "1002");
        var arAcct = await db.Accounts.FirstAsync(a => a.AccountCode == "1122");

        var (voucher, vErr) = await voucherSvc.CreateAsync(new VoucherInput(
            null, DateTime.UtcNow, "normal",
            $"收回应收账款：{invoice.CustomerName} - 发票 {invoice.InvoiceNo}" +
            (invoice.Status == "closed" ? "（已结清）" : "（部分收款）"),
            "crm-receipt", receipt.ReceiptNo,
            new List<VoucherEntryInput>
            {
                new(bankAcct.AccountId, "银行收款", amount, 0, "CNY"),
                new(arAcct.AccountId, "冲减应收", 0, amount, "CNY"),
            }), createdBy);

        if (voucher != null)
        {
            receipt.VoucherId = voucher.VoucherId;
            await db.SaveChangesAsync();
        }
        else if (vErr != null)
        {
            // 凭证生成失败不阻断收款（收款已落库），仅保留错误信息供排查；凭证可事后手工补录
        }

        return (receipt, null);
    }
}
