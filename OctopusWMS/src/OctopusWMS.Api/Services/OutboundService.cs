using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;

namespace OctopusWMS.Api.Services;

public class OutboundService(WmsDbContext db, WebhookSender webhook)
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
            // P11：FEFO 出库 — 同仓 + 同 PlmProductId 的库存按批次有效期升序优先消耗
            var invs = await db.Inventories
                .Where(x => x.WarehouseId == order.WarehouseId
                    && (x.PlmProductId == item.PlmProductId || (x.PlmProductId == null && x.ProductName == item.ProductName)))
                .ToListAsync();

            if (invs.Count == 0) continue;

            var batchById = invs.Where(x => x.BatchId.HasValue).Select(x => x.BatchId!.Value).Distinct().ToArray();
            var batches = batchById.Length == 0
                ? new Dictionary<long, DateTime?>()
                : await db.Batches
                    .Where(b => batchById.Contains(b.BatchId))
                    .ToDictionaryAsync(b => b.BatchId, b => b.ExpireDate);

            var ordered = invs
                .OrderBy(x => x.BatchId.HasValue && batches.TryGetValue(x.BatchId.Value, out var d) && d.HasValue
                    ? d.Value : DateTime.MaxValue)
                .ToList();

            var remaining = item.RequestedQty;
            foreach (var inv in ordered)
            {
                if (remaining <= 0) break;
                var take = Math.Min(inv.Quantity, remaining);
                inv.Quantity -= take;
                inv.UpdatedAt = DateTime.UtcNow;
                remaining -= take;
            }
            item.ShippedQty = item.RequestedQty - remaining;
        }

        order.Status = "shipped";
        order.ShippedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (order.CrmContractId.HasValue)
        {
            webhook.PostToCrm("/api/webhook/ship-confirm", new
            {
                CrmContractId = order.CrmContractId.Value,
                ShippedAt = order.ShippedAt
            });
        }
        return order;
    }
}
