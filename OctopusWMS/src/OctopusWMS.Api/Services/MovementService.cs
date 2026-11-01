using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;

namespace OctopusWMS.Api.Services;

/// <summary>P11：库内移库 + 跨仓调拨</summary>
public class MovementService(WmsDbContext db)
{
    /// <summary>库内移库（同仓库不同库位）</summary>
    public async Task<(WmsInventory? toInventory, string? error)> MoveAsync(long inventoryId, long toLocationId, decimal quantity)
    {
        if (quantity <= 0) return (null, "数量必须大于 0");

        var src = await db.Inventories.FirstOrDefaultAsync(x => x.InventoryId == inventoryId);
        if (src is null) return (null, "源库存不存在");
        if (src.Quantity - src.LockedQuantity < quantity)
            return (null, $"可用库存不足（可用 {src.Quantity - src.LockedQuantity}，请求 {quantity}）");

        var toLocation = await db.Locations.FirstOrDefaultAsync(x => x.LocationId == toLocationId);
        if (toLocation is null) return (null, "目标库位不存在");
        if (toLocation.WarehouseId != src.WarehouseId)
            return (null, "目标库位与源库位必须在同一仓库（跨仓请用调拨）");
        if (toLocation.IsLocked) return (null, "目标库位已锁定");

        // 减源
        src.Quantity -= quantity;
        src.UpdatedAt = DateTime.UtcNow;

        // 加目标（按 仓库+库位+商品+批次 合并）
        var dst = await db.Inventories.FirstOrDefaultAsync(x =>
            x.WarehouseId == src.WarehouseId &&
            x.LocationId == toLocationId &&
            x.PlmProductId == src.PlmProductId &&
            x.BatchId == src.BatchId);

        if (dst is null)
        {
            dst = new WmsInventory
            {
                WarehouseId = src.WarehouseId,
                LocationId = toLocationId,
                PlmProductId = src.PlmProductId,
                BatchId = src.BatchId,
                OwnerId = src.OwnerId,
                ProductName = src.ProductName,
                ProductCode = src.ProductCode,
                Spec = src.Spec,
                Unit = src.Unit,
                Quantity = quantity,
                LockedQuantity = 0,
                SafetyStock = src.SafetyStock,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Inventories.Add(dst);
        }
        else
        {
            dst.Quantity += quantity;
            dst.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return (dst, null);
    }

    /// <summary>创建跨仓调拨单（pending）</summary>
    public async Task<WmsTransfer> CreateTransferAsync(long fromWarehouseId, long toWarehouseId, long? plmProductId,
        string productName, decimal quantity, long createdBy, string? remark)
    {
        var transfer = new WmsTransfer
        {
            TransferCode = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            FromWarehouseId = fromWarehouseId,
            ToWarehouseId = toWarehouseId,
            PlmProductId = plmProductId,
            ProductName = productName,
            Quantity = quantity,
            Status = "pending",
            Remark = remark,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();
        return transfer;
    }

    /// <summary>调拨发货（pending → in_transit）：从源仓扣减库存</summary>
    public async Task<(WmsTransfer? transfer, string? error)> ShipTransferAsync(long transferId)
    {
        var t = await db.Transfers.FirstOrDefaultAsync(x => x.TransferId == transferId);
        if (t is null) return (null, "调拨单不存在");
        if (t.Status != "pending") return (null, $"当前状态 {t.Status} 不能发货");

        var src = await db.Inventories
            .Where(x => x.WarehouseId == t.FromWarehouseId && x.PlmProductId == t.PlmProductId)
            .OrderBy(x => x.Quantity)
            .ToListAsync();

        var available = src.Sum(x => x.Quantity - x.LockedQuantity);
        if (available < t.Quantity) return (null, $"源仓可用库存不足（可用 {available}，请求 {t.Quantity}）");

        var remaining = t.Quantity;
        foreach (var inv in src.OrderByDescending(x => x.Quantity))
        {
            if (remaining <= 0) break;
            var take = Math.Min(inv.Quantity - inv.LockedQuantity, remaining);
            inv.Quantity -= take;
            inv.UpdatedAt = DateTime.UtcNow;
            remaining -= take;
        }

        t.Status = "in_transit";
        t.ShippedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (t, null);
    }

    /// <summary>调拨收货（in_transit → received）：在目标仓增加库存</summary>
    public async Task<(WmsTransfer? transfer, string? error)> ReceiveTransferAsync(long transferId)
    {
        var t = await db.Transfers.FirstOrDefaultAsync(x => x.TransferId == transferId);
        if (t is null) return (null, "调拨单不存在");
        if (t.Status != "in_transit") return (null, $"当前状态 {t.Status} 不能收货");

        var dst = await db.Inventories.FirstOrDefaultAsync(x =>
            x.WarehouseId == t.ToWarehouseId && x.PlmProductId == t.PlmProductId);

        if (dst is null)
        {
            dst = new WmsInventory
            {
                WarehouseId = t.ToWarehouseId,
                PlmProductId = t.PlmProductId,
                ProductName = t.ProductName,
                Quantity = t.Quantity,
                LockedQuantity = 0,
                SafetyStock = 0,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Inventories.Add(dst);
        }
        else
        {
            dst.Quantity += t.Quantity;
            dst.UpdatedAt = DateTime.UtcNow;
        }

        t.Status = "received";
        t.ReceivedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (t, null);
    }

    public async Task<List<WmsTransfer>> GetListAsync(string? status)
    {
        var q = db.Transfers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        return await q.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }
}
