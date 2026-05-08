namespace OctopusMES.Api.Persistence;

public class MesSyncUser
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

public class MesSupplier
{
    public long SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? BankAccount { get; set; }
    public string? TaxNumber { get; set; }
    public string Status { get; set; } = "active";
    public int Level { get; set; } = 3;
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<MesPurchaseOrder> PurchaseOrders { get; set; } = new();
}

public class MesPurchaseOrder
{
    public long PurchaseId { get; set; }
    public string PurchaseCode { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string Status { get; set; } = "draft";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateTime? ExpectedDelivery { get; set; }
    public long? OaApprovalId { get; set; }
    public long? WmsInboundOrderId { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MesSupplier? Supplier { get; set; }
    public List<MesPurchaseItem> Items { get; set; } = new();
}

public class MesPurchaseItem
{
    public long ItemId { get; set; }
    public long PurchaseId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? Remark { get; set; }

    public MesPurchaseOrder? PurchaseOrder { get; set; }
}

public class MesWorkOrder
{
    public long WorkOrderId { get; set; }
    public string WorkOrderCode { get; set; } = string.Empty;
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal CompletedQty { get; set; }
    public string? Unit { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string? WorkshopName { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<MesWorkOrderProcess> Processes { get; set; } = new();
}

public class MesWorkOrderProcess
{
    public long ProcessId { get; set; }
    public long WorkOrderId { get; set; }
    public int StepOrder { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? WorkCenter { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Remark { get; set; }

    public MesWorkOrder? WorkOrder { get; set; }
}
