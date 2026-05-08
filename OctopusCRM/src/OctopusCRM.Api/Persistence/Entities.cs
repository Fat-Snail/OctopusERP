using System.ComponentModel.DataAnnotations;

namespace OctopusCRM.Api.Persistence;

// 用户同步缓存
public class CrmSyncUser
{
    public long Id { get; set; }
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
    public int Status { get; set; } = 1;
    public DateTime LastSyncAt { get; set; }
}

// 客户档案
public class CrmCustomer
{
    public long CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? IndustryType { get; set; }
    public string Level { get; set; } = "C";
    public string Status { get; set; } = "prospect";
    public string? Address { get; set; }
    public string? Website { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CrmContact> Contacts { get; set; } = new();
    public List<CrmInquiry> Inquiries { get; set; } = new();
}

// 联系人
public class CrmContact
{
    public long ContactId { get; set; }
    public long CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; } = false;
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }

    public CrmCustomer? Customer { get; set; }
}

// 询盘
public class CrmInquiry
{
    public long InquiryId { get; set; }
    public string InquiryCode { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "open";
    public DateTime? ExpectedDelivery { get; set; }
    public long AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CrmCustomer? Customer { get; set; }
    public List<CrmInquiryItem> Items { get; set; } = new();
}

// 询盘明细
public class CrmInquiryItem
{
    public long ItemId { get; set; }
    public long InquiryId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Remark { get; set; }

    public CrmInquiry? Inquiry { get; set; }
}

// 报价单
public class CrmQuote
{
    public long QuoteId { get; set; }
    public string QuoteCode { get; set; } = string.Empty;
    public long InquiryId { get; set; }
    public long CustomerId { get; set; }
    public string Status { get; set; } = "draft";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateTime? ValidUntil { get; set; }
    public DateTime? ExpectedDelivery { get; set; }
    public string? Terms { get; set; }
    public string? Remark { get; set; }
    public long? OaApprovalId { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CrmInquiry? Inquiry { get; set; }
    public List<CrmQuoteItem> Items { get; set; } = new();
}

// 报价明细
public class CrmQuoteItem
{
    public long ItemId { get; set; }
    public long QuoteId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? Remark { get; set; }

    public CrmQuote? Quote { get; set; }
}

// 销售合同
public class CrmContract
{
    public long ContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public long QuoteId { get; set; }
    public long CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateTime? SignDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public string Status { get; set; } = "draft";
    public long? OaApprovalId { get; set; }
    public string? FileUrl { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CrmQuote? Quote { get; set; }
    public List<CrmContractItem> Items { get; set; } = new();
    public List<CrmPayment> Payments { get; set; } = new();
}

// 合同明细
public class CrmContractItem
{
    public long ItemId { get; set; }
    public long ContractId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    public CrmContract? Contract { get; set; }
}

// 回款记录
public class CrmPayment
{
    public long PaymentId { get; set; }
    public long ContractId { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string Status { get; set; } = "pending";
    public long? OaApprovalId { get; set; }
    public string? BankReference { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public CrmContract? Contract { get; set; }
}
