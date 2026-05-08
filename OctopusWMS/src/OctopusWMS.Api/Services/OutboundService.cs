using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;

namespace OctopusWMS.Api.Services;

public class OutboundService(WmsDbContext db)
{
    public async Task<object> GetListAsync(string? status, int pageNum, int pageSize)
    {
        var q = db.OutboundOrders.Include(x => x.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(x => x.CreatedAt).Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return new { rows = list, total };
    }

    public async Task<WmsOutboundOrder?> GetByIdAsync(long id) =>
        await db.OutboundOrders.Include(x => x.Items).Include(x => x.Warehouse).FirstOrDefaultAsync(x => x.OutboundId == id);

    public async Task<WmsOutboundOrder> CreateAsync(WmsOutboundOrder req, long userId)
    {
        req.OutboundCode = $"OUT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
        req.Status = "pending";
        req.CreatedBy = userId;
        req.CreatedAt = DateTime.UtcNow;
        db.OutboundOrders.Add(req);
        await db.SaveChangesAsync();
        return req;
    }

    public async Task<WmsOutboundOrder?> ShipAsync(long id)
    {
        var order = await db.OutboundOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.OutboundId == id);
        if (order is null || order.Status != "pending") return null;

        foreach (var item in order.Items)
        {
            var inv = await db.Inventories.FirstOrDefaultAsync(x => x.WarehouseId == order.WarehouseId && x.ProductName == item.ProductName);
            if (inv is not null)
            {
                item.ShippedQty = item.RequestedQty;
                inv.Quantity = Math.Max(0, inv.Quantity - item.RequestedQty);
                inv.UpdatedAt = DateTime.UtcNow;
            }
        }

        order.Status = "shipped";
        order.ShippedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return order;
    }
}
