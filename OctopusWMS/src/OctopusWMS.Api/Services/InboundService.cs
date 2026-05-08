using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;

namespace OctopusWMS.Api.Services;

public class InboundService(WmsDbContext db)
{
    public async Task<object> GetListAsync(string? status, int pageNum, int pageSize)
    {
        var q = db.InboundOrders.Include(x => x.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(x => x.CreatedAt).Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return new { rows = list, total };
    }

    public async Task<WmsInboundOrder?> GetByIdAsync(long id) =>
        await db.InboundOrders.Include(x => x.Items).Include(x => x.Warehouse).FirstOrDefaultAsync(x => x.InboundId == id);

    public async Task<WmsInboundOrder> CreateAsync(WmsInboundOrder req, long userId)
    {
        req.InboundCode = $"IN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
        req.Status = "pending";
        req.CreatedBy = userId;
        req.CreatedAt = DateTime.UtcNow;
        db.InboundOrders.Add(req);
        await db.SaveChangesAsync();
        return req;
    }

    public async Task<WmsInboundOrder?> ReceiveAsync(long id, List<(long ItemId, decimal ReceivedQty, long? LocationId)> receipts)
    {
        var order = await db.InboundOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.InboundId == id);
        if (order is null || order.Status == "completed") return null;

        foreach (var (itemId, qty, locId) in receipts)
        {
            var item = order.Items.FirstOrDefault(i => i.ItemId == itemId);
            if (item is null) continue;
            item.ReceivedQty = qty;
            item.LocationId = locId;

            // Update inventory
            var inv = await db.Inventories.FirstOrDefaultAsync(x => x.WarehouseId == order.WarehouseId && x.PlmProductId == item.PlmProductId && x.ProductName == item.ProductName);
            if (inv is null)
            {
                db.Inventories.Add(new WmsInventory
                {
                    WarehouseId = order.WarehouseId,
                    LocationId = locId,
                    PlmProductId = item.PlmProductId,
                    ProductName = item.ProductName,
                    Spec = item.Spec,
                    Unit = item.Unit,
                    Quantity = qty,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                inv.Quantity += qty;
                inv.UpdatedAt = DateTime.UtcNow;
            }
        }

        order.Status = "completed";
        order.ReceivedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return order;
    }
}
