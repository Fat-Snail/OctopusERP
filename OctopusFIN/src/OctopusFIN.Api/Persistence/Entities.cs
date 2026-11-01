namespace OctopusFIN.Api.Persistence;

/// <summary>会计科目（树形）</summary>
public class FinAccount
{
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    /// <summary>asset / liability / equity / revenue / expense</summary>
    public string AccountType { get; set; } = "asset";
    /// <summary>debit / credit (借/贷余额方向)</summary>
    public string Direction { get; set; } = "debit";
    public long? ParentId { get; set; }
    public int Level { get; set; } = 1;
    public bool IsLeaf { get; set; } = true;
    public string Status { get; set; } = "active";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>会计期间</summary>
public class FinPeriod
{
    public long PeriodId { get; set; }
    /// <summary>期间编号，格式 yyyy-MM</summary>
    public string PeriodCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>open / closed</summary>
    public string Status { get; set; } = "open";
    public DateTime? ClosedAt { get; set; }
    public long? ClosedBy { get; set; }
}

/// <summary>记账凭证</summary>
public class FinVoucher
{
    public long VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public long PeriodId { get; set; }
    public DateTime VoucherDate { get; set; }
    /// <summary>记账类型：normal / closing / opening / adjustment</summary>
    public string VoucherType { get; set; } = "normal";
    public string Summary { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    /// <summary>draft / posted / void</summary>
    public string Status { get; set; } = "draft";
    /// <summary>来源系统：crm-ar / mes-ap / oa-expense / manual</summary>
    public string Source { get; set; } = "manual";
    public string? SourceRef { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PostedAt { get; set; }

    public List<FinVoucherEntry> Entries { get; set; } = new();
}

/// <summary>凭证分录（借贷明细）</summary>
public class FinVoucherEntry
{
    public long EntryId { get; set; }
    public long VoucherId { get; set; }
    public int LineOrder { get; set; }
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string Currency { get; set; } = "CNY";

    public FinVoucher? Voucher { get; set; }
}

/// <summary>应收发票（CRM 合同发货 → 自动生成）</summary>
public class FinArInvoice
{
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public long? CrmContractId { get; set; }
    public long? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    /// <summary>open / partial / closed / void</summary>
    public string Status { get; set; } = "open";
    public long? VoucherId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>应收收款</summary>
public class FinArReceipt
{
    public long ReceiptId { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public long InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ReceiptDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? BankReference { get; set; }
    public long? VoucherId { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>应付发票（MES 入库 / OA 报销 → 自动生成）</summary>
public class FinApInvoice
{
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public long? MesPurchaseOrderId { get; set; }
    public long? OaApprovalId { get; set; }
    public long? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "open";
    public long? VoucherId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>应付付款</summary>
public class FinApPayment
{
    public long PaymentId { get; set; }
    public string PaymentNo { get; set; } = string.Empty;
    public long InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? BankReference { get; set; }
    public long? VoucherId { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>从 UMC 同步的用户缓存（每个子系统都有）</summary>
public class FinSyncUser
{
    public long Id { get; set; }
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Email { get; set; }
    public DateTime LastSyncAt { get; set; }
}
