namespace OctopusMES.Api.Persistence;

public static class MesDbSeeder
{
    public static void Seed(MesDbContext db)
    {
        if (db.SyncUsers.Any()) return;

        db.SyncUsers.AddRange(
            new MesSyncUser { Id = 1, UmcUserId = 1, UserName = "admin", NickName = "超级管理员", Email = "admin@octopus.com", Status = 1, LastSyncAt = DateTime.UtcNow },
            new MesSyncUser { Id = 2, UmcUserId = 2, UserName = "zhangsan", NickName = "张三", Email = "zhangsan@octopus.com", Status = 1, LastSyncAt = DateTime.UtcNow },
            new MesSyncUser { Id = 3, UmcUserId = 3, UserName = "lisi", NickName = "李四", Email = "lisi@octopus.com", Status = 1, LastSyncAt = DateTime.UtcNow }
        );

        db.Suppliers.AddRange(
            new MesSupplier { SupplierId = 1, SupplierCode = "SUP-001", SupplierName = "富士康科技集团", ContactName = "郭明", Phone = "13800138001", Email = "gm@foxconn.com", Address = "广东省深圳市龙华区", TaxNumber = "914403001925627787", Status = "active", Level = 1, Remark = "A级战略供应商", CreatedAt = DateTime.UtcNow.AddDays(-365) },
            new MesSupplier { SupplierId = 2, SupplierCode = "SUP-002", SupplierName = "伟创力制造", ContactName = "李强", Phone = "13800138002", Email = "lq@flextronics.com", Address = "广东省深圳市宝安区", TaxNumber = "914403002234567890", Status = "active", Level = 2, Remark = "B级供应商", CreatedAt = DateTime.UtcNow.AddDays(-180) },
            new MesSupplier { SupplierId = 3, SupplierCode = "SUP-003", SupplierName = "比亚迪精密制造", ContactName = "王芳", Phone = "13800138003", Email = "wf@byd.com", Address = "广东省深圳市坪山区", TaxNumber = "914403003345678901", Status = "active", Level = 1, Remark = "A级战略供应商", CreatedAt = DateTime.UtcNow.AddDays(-200) },
            new MesSupplier { SupplierId = 4, SupplierCode = "SUP-004", SupplierName = "蓝思科技", ContactName = "张美", Phone = "13800138004", Email = "zm@lens.cn", Address = "湖南省长沙市浏阳市", TaxNumber = "914300004456789012", Status = "active", Level = 2, Remark = "屏幕玻璃供应商", CreatedAt = DateTime.UtcNow.AddDays(-90) }
        );

        var now = DateTime.UtcNow;

        db.PurchaseOrders.AddRange(
            new MesPurchaseOrder { PurchaseId = 1, PurchaseCode = "PO-2024-0001", SupplierId = 1, Status = "approved", TotalAmount = 5000000, Currency = "CNY", ExpectedDelivery = now.AddDays(7), OaApprovalId = 3, WmsInboundOrderId = 1, CreatedBy = 2, CreatedAt = now.AddDays(-12), UpdatedAt = now.AddDays(-10) },
            new MesPurchaseOrder { PurchaseId = 2, PurchaseCode = "PO-2024-0002", SupplierId = 2, Status = "submitted", TotalAmount = 2000000, Currency = "CNY", ExpectedDelivery = now.AddDays(14), OaApprovalId = null, WmsInboundOrderId = 2, CreatedBy = 2, CreatedAt = now.AddDays(-5), UpdatedAt = now.AddDays(-4) },
            new MesPurchaseOrder { PurchaseId = 3, PurchaseCode = "PO-2024-0003", SupplierId = 3, Status = "draft", TotalAmount = 800000, Currency = "CNY", ExpectedDelivery = now.AddDays(21), OaApprovalId = null, WmsInboundOrderId = null, CreatedBy = 3, CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) }
        );

        db.PurchaseItems.AddRange(
            new MesPurchaseItem { ItemId = 1, PurchaseId = 1, PlmProductId = 1, ProductName = "iPhone 15 Pro 组件套装", Spec = "256GB 版本", Unit = "套", Quantity = 2000, UnitPrice = 2000, Amount = 4000000 },
            new MesPurchaseItem { ItemId = 2, PurchaseId = 1, PlmProductId = null, ProductName = "铝合金外壳", Spec = "6061-T6", Unit = "kg", Quantity = 5000, UnitPrice = 200, Amount = 1000000 },
            new MesPurchaseItem { ItemId = 3, PurchaseId = 2, PlmProductId = 2, ProductName = "小米 14 组件套装", Spec = "256GB 版本", Unit = "套", Quantity = 2000, UnitPrice = 800, Amount = 1600000 },
            new MesPurchaseItem { ItemId = 4, PurchaseId = 2, PlmProductId = null, ProductName = "屏幕玻璃", Spec = "6.36寸 AMOLED", Unit = "片", Quantity = 2500, UnitPrice = 160, Amount = 400000 },
            new MesPurchaseItem { ItemId = 5, PurchaseId = 3, PlmProductId = null, ProductName = "蓝宝石玻璃", Spec = "高硬度防划伤", Unit = "片", Quantity = 4000, UnitPrice = 200, Amount = 800000 }
        );

        db.WorkOrders.AddRange(
            new MesWorkOrder { WorkOrderId = 1, WorkOrderCode = "WO-2024-0001", PlmProductId = 1, ProductName = "iPhone 15 Pro 整机装配", Spec = "256GB 钛金色", PlannedQty = 1000, CompletedQty = 850, Unit = "台", Status = "in_progress", PlannedStart = now.AddDays(-15), PlannedEnd = now.AddDays(5), ActualStart = now.AddDays(-14), WorkshopName = "华南装配车间A", CreatedBy = 2, CreatedAt = now.AddDays(-20), UpdatedAt = now },
            new MesWorkOrder { WorkOrderId = 2, WorkOrderCode = "WO-2024-0002", PlmProductId = 2, ProductName = "小米 14 整机装配", Spec = "256GB 黑色", PlannedQty = 2000, CompletedQty = 2000, Unit = "台", Status = "completed", PlannedStart = now.AddDays(-30), PlannedEnd = now.AddDays(-10), ActualStart = now.AddDays(-29), ActualEnd = now.AddDays(-11), WorkshopName = "华南装配车间B", CreatedBy = 2, CreatedAt = now.AddDays(-35), UpdatedAt = now.AddDays(-11) },
            new MesWorkOrder { WorkOrderId = 3, WorkOrderCode = "WO-2024-0003", PlmProductId = 1, ProductName = "iPhone 15 Pro 整机装配", Spec = "128GB 黑色", PlannedQty = 500, CompletedQty = 0, Unit = "台", Status = "draft", PlannedStart = now.AddDays(10), PlannedEnd = now.AddDays(25), WorkshopName = "华南装配车间A", CreatedBy = 3, CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-2) }
        );

        db.WorkOrderProcesses.AddRange(
            new MesWorkOrderProcess { ProcessId = 1, WorkOrderId = 1, StepOrder = 1, ProcessName = "来料检验", WorkCenter = "QC中心", Status = "completed", StartedAt = now.AddDays(-14), FinishedAt = now.AddDays(-13) },
            new MesWorkOrderProcess { ProcessId = 2, WorkOrderId = 1, StepOrder = 2, ProcessName = "SMT贴片", WorkCenter = "SMT车间", Status = "completed", StartedAt = now.AddDays(-13), FinishedAt = now.AddDays(-8) },
            new MesWorkOrderProcess { ProcessId = 3, WorkOrderId = 1, StepOrder = 3, ProcessName = "整机装配", WorkCenter = "装配车间", Status = "in_progress", StartedAt = now.AddDays(-8) },
            new MesWorkOrderProcess { ProcessId = 4, WorkOrderId = 1, StepOrder = 4, ProcessName = "整机测试", WorkCenter = "测试中心", Status = "pending" },
            new MesWorkOrderProcess { ProcessId = 5, WorkOrderId = 2, StepOrder = 1, ProcessName = "来料检验", WorkCenter = "QC中心", Status = "completed", StartedAt = now.AddDays(-29), FinishedAt = now.AddDays(-28) },
            new MesWorkOrderProcess { ProcessId = 6, WorkOrderId = 2, StepOrder = 2, ProcessName = "SMT贴片", WorkCenter = "SMT车间", Status = "completed", StartedAt = now.AddDays(-28), FinishedAt = now.AddDays(-20) },
            new MesWorkOrderProcess { ProcessId = 7, WorkOrderId = 2, StepOrder = 3, ProcessName = "整机装配", WorkCenter = "装配车间", Status = "completed", StartedAt = now.AddDays(-20), FinishedAt = now.AddDays(-12) },
            new MesWorkOrderProcess { ProcessId = 8, WorkOrderId = 2, StepOrder = 4, ProcessName = "整机测试", WorkCenter = "测试中心", Status = "completed", StartedAt = now.AddDays(-12), FinishedAt = now.AddDays(-11) }
        );

        db.SaveChanges();
    }
}
