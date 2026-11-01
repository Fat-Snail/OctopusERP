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

// P11：货主（多租户多货主，如电商代发场景）
public class WmsOwner
{
    public long OwnerId { get; set; }
    public string OwnerCode { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
}

public class WmsWarehouse
{
    public long WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public long? OwnerId { get; set; }   // P11：所属货主
    public string? Address { get; set; }
    public string? Manager { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "active";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<WmsLocation> Locations { get; set; } = new();
    public List<WmsZone> Zones { get; set; } = new();
}

// P11：库区（拣货/存储/退货/质检/不良品）
public class WmsZone
{
    public long ZoneId { get; set; }
    public long WarehouseId { get; set; }
    public string ZoneCode { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    /// <summary>picking / storage / returns / qc / defect</summary>
    public string ZoneType { get; set; } = "storage";
    public string Status { get; set; } = "active";

    public WmsWarehouse? Warehouse { get; set; }
}

public class WmsLocation
{
    public long LocationId { get; set; }
    public long WarehouseId { get; set; }
    public long? ZoneId { get; set; }     // P11：所属库区
    public string LocationCode { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string? Aisle { get; set; }
    public string? Shelf { get; set; }
    public decimal Capacity { get; set; }
    public string Status { get; set; } = "available";
    /// <summary>P11：ABC 分类（A=高频 B=中频 C=低频）按 30 天出库频次自动计算</summary>
    public string? AbcClass { get; set; }
    /// <summary>P11：是否锁定（盘点/检修期间临时禁用）</summary>
    public bool IsLocked { get; set; }

    public WmsWarehouse? Warehouse { get; set; }
}

// P11：批次/有效期管理（FEFO 出库依据）
public class WmsBatch
{
    public long BatchId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpireDate { get; set; }
    public string QcStatus { get; set; } = "passed";
    public DateTime CreatedAt { get; set; }
}

public class WmsInventory
{
    public long InventoryId { get; set; }
    public long WarehouseId { get; set; }
    public long? LocationId { get; set; }
    public long? PlmProductId { get; set; }
    public long? BatchId { get; set; }            // P11：批次（可为空，无批次管理时不填）
    public long? OwnerId { get; set; }            // P11：货主（多货主场景）
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string? Spec { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal LockedQuantity { get; set; }
    public decimal SafetyStock { get; set; }
    public DateTime UpdatedAt { get; set; }

    public WmsWarehouse? Warehouse { get; set; }
    public WmsBatch? Batch { get; set; }
}

// P11：跨仓调拨单（三段式：出库 → 在途 → 入库）
public class WmsTransfer
{
    public long TransferId { get; set; }
    public string TransferCode { get; set; } = string.Empty;
    public long FromWarehouseId { get; set; }
    public long ToWarehouseId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    /// <summary>pending / in_transit / received / cancelled</summary>
    public string Status { get; set; } = "pending";
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
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

// 服装行业：面料库存（按面料编号 + 色号 + 批次维度）
public class WmsFabricInventory
{
    public long FabricInventoryId { get; set; }
    public long WarehouseId { get; set; }
    /// <summary>面料编号(梭织/针织等通用编码)</summary>
    public string FabricCode { get; set; } = string.Empty;
    public string? FabricName { get; set; }
    /// <summary>色号</summary>
    public string? ColorCode { get; set; }
    /// <summary>批次号</summary>
    public string? BatchCode { get; set; }
    /// <summary>单位: 米/码/公斤</summary>
    public string Unit { get; set; } = "米";
    public decimal Quantity { get; set; }
    public decimal LockedQuantity { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// 服装行业：回布登记（关联 MES 排程）
public class WmsFabricArrival
{
    public long ArrivalId { get; set; }
    public string ArrivalCode { get; set; } = string.Empty;
    /// <summary>关联 MES 排程 ID</summary>
    public long? MesScheduleId { get; set; }
    /// <summary>关联 MES 订单池 ID</summary>
    public long? MesOrderId { get; set; }
    public string FabricCode { get; set; } = string.Empty;
    public string? ColorCode { get; set; }
    public string? BatchCode { get; set; }
    public decimal ExpectedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public DateTime? ActualArrivalDate { get; set; }
    /// <summary>状态: pending(待到) / partial(部分到货) / ok(已到齐) / late(逾期未到)</summary>
    public string Status { get; set; } = "pending";
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
