namespace OctopusWMS.Api.Persistence;

public static class WmsDbSeeder
{
    public static void Seed(WmsDbContext db)
    {
        if (db.SyncUsers.Any()) return;

        db.SyncUsers.AddRange(
            new WmsSyncUser { Id = 1, UmcUserId = 1, UserName = "admin", NickName = "超级管理员", Email = "admin@octopus.com", Status = 1, LastSyncAt = DateTime.UtcNow },
            new WmsSyncUser { Id = 2, UmcUserId = 2, UserName = "zhangsan", NickName = "张三", Email = "zhangsan@octopus.com", Status = 1, LastSyncAt = DateTime.UtcNow },
            new WmsSyncUser { Id = 3, UmcUserId = 3, UserName = "lisi", NickName = "李四", Email = "lisi@octopus.com", Status = 1, LastSyncAt = DateTime.UtcNow }
        );

        db.Warehouses.AddRange(
            new WmsWarehouse { WarehouseId = 1, WarehouseCode = "WH-001", WarehouseName = "华南主仓", Address = "广东省深圳市宝安区工业路1号", Manager = "张三", Phone = "13800138001", Status = "active", Remark = "主要存储成品", CreatedAt = DateTime.UtcNow },
            new WmsWarehouse { WarehouseId = 2, WarehouseCode = "WH-002", WarehouseName = "华东备货仓", Address = "上海市嘉定区仓储路2号", Manager = "李四", Phone = "13800138002", Status = "active", Remark = "备货仓库", CreatedAt = DateTime.UtcNow },
            new WmsWarehouse { WarehouseId = 3, WarehouseCode = "WH-003", WarehouseName = "原材料仓", Address = "广东省深圳市宝安区材料路3号", Manager = "admin", Phone = "13800138000", Status = "active", Remark = "存储原材料", CreatedAt = DateTime.UtcNow }
        );

        db.Locations.AddRange(
            new WmsLocation { LocationId = 1, WarehouseId = 1, LocationCode = "A-01-01", Zone = "A区", Aisle = "01", Shelf = "01", Capacity = 1000, Status = "available" },
            new WmsLocation { LocationId = 2, WarehouseId = 1, LocationCode = "A-01-02", Zone = "A区", Aisle = "01", Shelf = "02", Capacity = 1000, Status = "available" },
            new WmsLocation { LocationId = 3, WarehouseId = 1, LocationCode = "B-01-01", Zone = "B区", Aisle = "01", Shelf = "01", Capacity = 2000, Status = "available" },
            new WmsLocation { LocationId = 4, WarehouseId = 2, LocationCode = "A-01-01", Zone = "A区", Aisle = "01", Shelf = "01", Capacity = 500, Status = "available" },
            new WmsLocation { LocationId = 5, WarehouseId = 3, LocationCode = "R-01-01", Zone = "原料区", Aisle = "01", Shelf = "01", Capacity = 5000, Status = "available" }
        );

        var now = DateTime.UtcNow;

        db.Inventories.AddRange(
            new WmsInventory { InventoryId = 1, WarehouseId = 1, LocationId = 1, PlmProductId = 1, ProductName = "iPhone 15 Pro", ProductCode = "SKU-IPHONE15PRO-001", Spec = "256GB 钛金色", Unit = "台", Quantity = 500, LockedQuantity = 100, SafetyStock = 50, UpdatedAt = now },
            new WmsInventory { InventoryId = 2, WarehouseId = 1, LocationId = 2, PlmProductId = 2, ProductName = "小米 14", ProductCode = "SKU-MI14-001", Spec = "256GB 黑色", Unit = "台", Quantity = 800, LockedQuantity = 200, SafetyStock = 100, UpdatedAt = now },
            new WmsInventory { InventoryId = 3, WarehouseId = 2, LocationId = 4, PlmProductId = 1, ProductName = "iPhone 15 Pro", ProductCode = "SKU-IPHONE15PRO-001", Spec = "128GB 黑色", Unit = "台", Quantity = 200, LockedQuantity = 0, SafetyStock = 30, UpdatedAt = now },
            new WmsInventory { InventoryId = 4, WarehouseId = 3, LocationId = 5, PlmProductId = null, ProductName = "铝合金外壳", ProductCode = "MAT-ALU-001", Spec = "6061-T6", Unit = "kg", Quantity = 10000, LockedQuantity = 2000, SafetyStock = 1000, UpdatedAt = now }
        );

        db.InboundOrders.AddRange(
            new WmsInboundOrder { InboundId = 1, InboundCode = "IN-2024-0001", InboundType = "purchase", WarehouseId = 1, MesPurchaseOrderId = 1, Status = "completed", Supplier = "富士康科技集团", CreatedBy = 2, CreatedAt = now.AddDays(-10), ReceivedAt = now.AddDays(-8) },
            new WmsInboundOrder { InboundId = 2, InboundCode = "IN-2024-0002", InboundType = "purchase", WarehouseId = 1, MesPurchaseOrderId = 2, Status = "receiving", Supplier = "伟创力制造", CreatedBy = 2, CreatedAt = now.AddDays(-3) },
            new WmsInboundOrder { InboundId = 3, InboundCode = "IN-2024-0003", InboundType = "return", WarehouseId = 2, Status = "pending", Remark = "客户退货", CreatedBy = 3, CreatedAt = now.AddDays(-1) }
        );

        db.InboundItems.AddRange(
            new WmsInboundItem { ItemId = 1, InboundId = 1, PlmProductId = 1, ProductName = "iPhone 15 Pro", Spec = "256GB 钛金色", Unit = "台", ExpectedQty = 600, ReceivedQty = 600, LocationId = 1 },
            new WmsInboundItem { ItemId = 2, InboundId = 2, PlmProductId = 2, ProductName = "小米 14", Spec = "256GB 黑色", Unit = "台", ExpectedQty = 1000, ReceivedQty = 500, LocationId = 2 },
            new WmsInboundItem { ItemId = 3, InboundId = 3, PlmProductId = 1, ProductName = "iPhone 15 Pro", Spec = "128GB 黑色", Unit = "台", ExpectedQty = 50, ReceivedQty = 0, LocationId = null }
        );

        db.OutboundOrders.AddRange(
            new WmsOutboundOrder { OutboundId = 1, OutboundCode = "OUT-2024-0001", OutboundType = "sales", WarehouseId = 1, CrmContractId = 1, Status = "shipped", Recipient = "华为技术有限公司", ShipAddress = "广东省深圳市龙岗区", CreatedBy = 2, CreatedAt = now.AddDays(-7), ShippedAt = now.AddDays(-5) },
            new WmsOutboundOrder { OutboundId = 2, OutboundCode = "OUT-2024-0002", OutboundType = "sales", WarehouseId = 1, CrmContractId = 2, Status = "pending", Recipient = "阿里巴巴集团", ShipAddress = "浙江省杭州市余杭区", CreatedBy = 3, CreatedAt = now.AddDays(-2) }
        );

        db.OutboundItems.AddRange(
            new WmsOutboundItem { ItemId = 1, OutboundId = 1, PlmProductId = 1, ProductName = "iPhone 15 Pro", Spec = "256GB 钛金色", Unit = "台", RequestedQty = 100, ShippedQty = 100, LocationId = 1 },
            new WmsOutboundItem { ItemId = 2, OutboundId = 2, PlmProductId = 2, ProductName = "小米 14", Spec = "256GB 黑色", Unit = "台", RequestedQty = 200, ShippedQty = 0, LocationId = 2 }
        );

        db.Stocktakes.AddRange(
            new WmsStocktake { StocktakeId = 1, StocktakeCode = "ST-2024-0001", WarehouseId = 1, Status = "completed", CreatedBy = 1, CreatedAt = now.AddDays(-30), CompletedAt = now.AddDays(-28) },
            new WmsStocktake { StocktakeId = 2, StocktakeCode = "ST-2024-0002", WarehouseId = 1, Status = "in_progress", CreatedBy = 1, CreatedAt = now.AddDays(-1) }
        );

        db.StocktakeItems.AddRange(
            new WmsStocktakeItem { ItemId = 1, StocktakeId = 1, PlmProductId = 1, ProductName = "iPhone 15 Pro", Spec = "256GB 钛金色", Unit = "台", BookQty = 500, ActualQty = 498, DiffQty = -2 },
            new WmsStocktakeItem { ItemId = 2, StocktakeId = 1, PlmProductId = 2, ProductName = "小米 14", Spec = "256GB 黑色", Unit = "台", BookQty = 800, ActualQty = 800, DiffQty = 0 },
            new WmsStocktakeItem { ItemId = 3, StocktakeId = 2, PlmProductId = 1, ProductName = "iPhone 15 Pro", Spec = "256GB 钛金色", Unit = "台", BookQty = 500, ActualQty = 0, DiffQty = 0 }
        );

        db.SaveChanges();
    }
}
