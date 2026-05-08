using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;

namespace OctopusWMS.Api.Services;

public class StocktakeService(WmsDbContext db)
{
    public async Task<object> GetListAsync(long? warehouseId, string? status, int pageNum, int pageSize)
    {
        var q = db.Stocktakes.AsQueryable();
        if (warehouseId.HasValue) q = q.Where(x => x.WarehouseId == warehouseId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(x => x.CreatedAt).Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return new { rows = list, total };
    }

    public async Task<WmsStocktake?> GetByIdAsync(long id) =>
        await db.Stocktakes.Include(x => x.Items).Include(x => x.Warehouse).FirstOrDefaultAsync(x => x.StocktakeId == id);

    public async Task<WmsStocktake> CreateAsync(long warehouseId, string? remark, long userId)
    {
        var inventories = await db.Inventories.Where(x => x.WarehouseId == warehouseId).ToListAsync();
        var task = new WmsStocktake
        {
            StocktakeCode = $"ST-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            WarehouseId = warehouseId,
            Status = "in_progress",
            Remark = remark,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Items = inventories.Select(inv => new WmsStocktakeItem
            {
                PlmProductId = inv.PlmProductId,
                ProductName = inv.ProductName,
                Spec = inv.Spec,
                Unit = inv.Unit,
                BookQty = inv.Quantity,
                ActualQty = 0,
                DiffQty = 0,
            }).ToList()
        };
        db.Stocktakes.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    public async Task<WmsStocktake?> SubmitResultsAsync(long id, List<(long ItemId, decimal ActualQty)> results)
    {
        var task = await db.Stocktakes.Include(x => x.Items).FirstOrDefaultAsync(x => x.StocktakeId == id);
        if (task is null || task.Status != "in_progress") return null;

        foreach (var (itemId, actualQty) in results)
        {
            var item = task.Items.FirstOrDefault(i => i.ItemId == itemId);
            if (item is null) continue;
            item.ActualQty = actualQty;
            item.DiffQty = actualQty - item.BookQty;
        }

        task.Status = "completed";
        task.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return task;
    }
}
