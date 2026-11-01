using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;

namespace OctopusWMS.Api.Services;

public class InventoryService(WmsDbContext db)
{
    public async Task<object> GetListAsync(long? warehouseId, string? keyword, int pageNum, int pageSize)
    {
        var q = db.Inventories.AsQueryable();
        if (warehouseId.HasValue)
            q = q.Where(x => x.WarehouseId == warehouseId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(x => x.ProductName.Contains(keyword) || (x.ProductCode != null && x.ProductCode.Contains(keyword)));

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(x => x.UpdatedAt).Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return new { rows = list, total };
    }

    public async Task<object> GetSummaryAsync()
    {
        var warehouseCount = await db.Warehouses.CountAsync(x => x.Status == "active");
        var skuCount = await db.Inventories.Select(x => x.PlmProductId ?? -x.InventoryId).Distinct().CountAsync();
        var totalQty = await db.Inventories.SumAsync(x => x.Quantity);
        var lowStockCount = await db.Inventories.CountAsync(x => x.Quantity <= x.SafetyStock);
        return new { warehouseCount, skuCount, totalQty, lowStockCount };
    }

    public async Task<List<WmsInventory>> GetLowStockAsync() =>
        await db.Inventories.Where(x => x.Quantity <= x.SafetyStock).ToListAsync();

    public async Task<WmsInventory?> AdjustAsync(long inventoryId, decimal delta, string reason)
    {
        var inv = await db.Inventories.FindAsync(inventoryId);
        if (inv is null) return null;
        inv.Quantity += delta;
        if (inv.Quantity < 0) inv.Quantity = 0;
        inv.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return inv;
    }

    /// <summary>批量按 PlmProductId 汇总库存（跨仓相加；返回 totalQty / availableQty / lockedQty / warehouseCount）</summary>
    public async Task<List<ProductStockSummary>> GetByProductsAsync(IEnumerable<long> productIds)
    {
        var ids = productIds.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await db.Inventories
            .Where(x => x.PlmProductId.HasValue && ids.Contains(x.PlmProductId!.Value))
            .ToListAsync();

        return rows
            .GroupBy(x => x.PlmProductId!.Value)
            .Select(g => new ProductStockSummary
            {
                PlmProductId = g.Key,
                TotalQty = g.Sum(x => x.Quantity),
                AvailableQty = g.Sum(x => x.Quantity - x.LockedQuantity),
                LockedQty = g.Sum(x => x.LockedQuantity),
                WarehouseCount = g.Select(x => x.WarehouseId).Distinct().Count(),
            })
            .ToList();
    }
}

public record ProductStockSummary
{
    public long PlmProductId { get; set; }
    public decimal TotalQty { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal LockedQty { get; set; }
    public int WarehouseCount { get; set; }
}
