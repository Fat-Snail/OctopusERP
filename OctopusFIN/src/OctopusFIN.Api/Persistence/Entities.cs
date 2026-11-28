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
    /// <summary>所属组织（多组织核算维度，null 表示全局）</summary>
    public long? OrganizationId { get; set; }
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
    /// <summary>利润中心维度（阿米巴核算，null 表示全局/未分配）</summary>
    public long? ProfitCenterId { get; set; }

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

/// <summary>组织（映射 UMC 公司/部门，多组织核算的基础）</summary>
public class FinOrganization
{
    public long OrganizationId { get; set; }
    public string OrgCode { get; set; } = string.Empty;
    public string OrgName { get; set; } = string.Empty;
    /// <summary>映射的 UMC 部门 ID</summary>
    public long? UmcDeptId { get; set; }
    /// <summary>active / disabled</summary>
    public string Status { get; set; } = "active";
    public bool IsPrimary { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

/// <summary>利润中心（阿米巴单元，多维度核算的核心维度）</summary>
public class FinProfitCenter
{
    public long ProfitCenterId { get; set; }
    public string CenterCode { get; set; } = string.Empty;
    public string CenterName { get; set; } = string.Empty;
    /// <summary>所属组织</summary>
    public long OrganizationId { get; set; }
    public FinOrganization? Organization { get; set; }
    /// <summary>active / disabled</summary>
    public string Status { get; set; } = "active";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>电子发票（开票/查验/红冲）</summary>
public class FinInvoice
{
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    /// <summary>invoice（蓝票）/ red（红冲）</summary>
    public string InvoiceType { get; set; } = "invoice";
    /// <summary>销项 / 进项</summary>
    public string InvoiceCategory { get; set; } = "output";
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerTaxNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    /// <summary>tax_rate 如 13% → 0.13</summary>
    public decimal TaxRate { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateTime InvoiceDate { get; set; }
    /// <summary>issued / verified / red_flushed / void</summary>
    public string Status { get; set; } = "issued";
    /// <summary>外部税务平台发票 ID（凭证到位后回填）</summary>
    public string? ExternalInvoiceId { get; set; }
    /// <summary>关联应收/应付发票 ID</summary>
    public long? RelatedArInvoiceId { get; set; }
    public long? RelatedApInvoiceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<FinInvoiceItem> Items { get; set; } = new();
}

/// <summary>发票明细行</summary>
public class FinInvoiceItem
{
    public long ItemId { get; set; }
    public long InvoiceId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Specification { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public FinInvoice? Invoice { get; set; }
}

/// <summary>纳税申报单（按税种/期间）</summary>
public class FinTaxReturn
{
    public long ReturnId { get; set; }
    /// <summary>税种：vat（增值税）/ cit（企业所得税）/ iprt（个税）</summary>
    public string TaxType { get; set; } = "vat";
    public long PeriodId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public decimal TaxableAmount { get; set; }
    public decimal TaxPayable { get; set; }
    public decimal TaxPaid { get; set; }
    public decimal TaxRefund { get; set; }
    /// <summary>draft / submitted / filed / approved</summary>
    public string Status { get; set; } = "draft";
    public string? ExternalReturnId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FiledAt { get; set; }
}

/// <summary>银行账户</summary>
public class FinBankAccount
{
    public long AccountId { get; set; }
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    /// <summary>关联的组织/利润中心</summary>
    public long? OrganizationId { get; set; }
    public long? ProfitCenterId { get; set; }
    /// <summary>active / frozen / closed</summary>
    public string Status { get; set; } = "active";
    /// <summary>银行直连适配器编码（凭证到位后填真实银行编码）</summary>
    public string? AdapterCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>银行对账单明细（自动拉取/匹配）</summary>
public class FinBankStatement
{
    public long StatementId { get; set; }
    public long BankAccountId { get; set; }
    public string TransactionNo { get; set; } = string.Empty;
    /// <summary>debit（银行扣款）/ credit（银行入账）</summary>
    public string Direction { get; set; } = "credit";
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Counterparty { get; set; }
    public string? Remark { get; set; }
    /// <summary>unmatched / matched / reconciled</summary>
    public string MatchStatus { get; set; } = "unmatched";
    /// <summary>匹配的应收/应付收付款 ID</summary>
    public long? MatchedReceiptId { get; set; }
    public long? MatchedPaymentId { get; set; }
    /// <summary>外部银行流水号</summary>
    public string? ExternalTxnId { get; set; }
    public DateTime CreatedAt { get; set; }
}
