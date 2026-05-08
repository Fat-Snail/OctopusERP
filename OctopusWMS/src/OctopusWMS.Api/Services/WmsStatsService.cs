using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;

namespace OctopusWMS.Api.Services;

public class WmsStatsService(WmsDbContext db)
{
    public async Task<object> GetSummaryAsync()
    {
        var warehouseCount = await db.Warehouses.CountAsync(x => x.Status == "active");
        var inboundToday = await db.InboundOrders.CountAsync(x => x.CreatedAt.Date == DateTime.UtcNow.Date);
        var outboundPending = await db.OutboundOrders.CountAsync(x => x.Status == "pending");
        var lowStockCount = await db.Inventories.CountAsync(x => x.Quantity <= x.SafetyStock);
        var totalInventoryItems = await db.Inventories.CountAsync();
        return new { warehouseCount, inboundToday, outboundPending, lowStockCount, totalInventoryItems };
    }
}
