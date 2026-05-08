namespace OctopusWMS.Api.Persistence;

public class WmsSyncUser
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

public class WmsWarehouse
{
    public long WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Manager { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "active";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<WmsLocation> Locations { get; set; } = new();
}

public class WmsLocation
{
    public long LocationId { get; set; }
    public long WarehouseId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string? Aisle { get; set; }
    public string? Shelf { get; set; }
    public decimal Capacity { get; set; }
    public string Status { get; set; } = "available";

    public WmsWarehouse? Warehouse { get; set; }
}

public class WmsInventory
{
    public long InventoryId { get; set; }
    public long WarehouseId { get; set; }
    public long? LocationId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string? Spec { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal LockedQuantity { get; set; }
    public decimal SafetyStock { get; set; }
    public DateTime UpdatedAt { get; set; }

    public WmsWarehouse? Warehouse { get; set; }
}

public class WmsInboundOrder
{
    public long InboundId { get; set; }
    public string InboundCode { get; set; } = string.Empty;
    public string InboundType { get; set; } = "purchase";
    public long WarehouseId { get; set; }
    public long? MesPurchaseOrderId { get; set; }
    public long? MesWorkOrderId { get; set; }
    public string Status { get; set; } = "pending";
    public string? Supplier { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    public WmsWarehouse? Warehouse { get; set; }
    public List<WmsInboundItem> Items { get; set; } = new();
}

public class WmsInboundItem
{
    public long ItemId { get; set; }
    public long InboundId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public string? Unit { get; set; }
    public decimal ExpectedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public long? LocationId { get; set; }

    public WmsInboundOrder? Inbound { get; set; }
}

public class WmsOutboundOrder
{
    public long OutboundId { get; set; }
    public string OutboundCode { get; set; } = string.Empty;
    public string OutboundType { get; set; } = "sales";
    public long WarehouseId { get; set; }
    public long? CrmContractId { get; set; }
    public string Status { get; set; } = "pending";
    public string? Recipient { get; set; }
    public string? ShipAddress { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }

    public WmsWarehouse? Warehouse { get; set; }
    public List<WmsOutboundItem> Items { get; set; } = new();
}

public class WmsOutboundItem
{
    public long ItemId { get; set; }
    public long OutboundId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public string? Unit { get; set; }
    public decimal RequestedQty { get; set; }
    public decimal ShippedQty { get; set; }
    public long? LocationId { get; set; }

    public WmsOutboundOrder? Outbound { get; set; }
}

public class WmsStocktake
{
    public long StocktakeId { get; set; }
    public string StocktakeCode { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public string Status { get; set; } = "draft";
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public WmsWarehouse? Warehouse { get; set; }
    public List<WmsStocktakeItem> Items { get; set; } = new();
}

public class WmsStocktakeItem
{
    public long ItemId { get; set; }
    public long StocktakeId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public string? Unit { get; set; }
    public decimal BookQty { get; set; }
    public decimal ActualQty { get; set; }
    public decimal DiffQty { get; set; }

    public WmsStocktake? Stocktake { get; set; }
}
