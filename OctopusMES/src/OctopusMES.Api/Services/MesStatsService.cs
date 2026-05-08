using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

public class MesStatsService(MesDbContext db)
{
    public async Task<object> GetSummaryAsync()
    {
        var supplierCount = await db.Suppliers.CountAsync(x => x.Status == "active");
        var purchaseInProgress = await db.PurchaseOrders.CountAsync(x => x.Status == "submitted" || x.Status == "approved");
        var workOrderInProgress = await db.WorkOrders.CountAsync(x => x.Status == "in_progress");
        var workOrderCompleted = await db.WorkOrders.CountAsync(x => x.Status == "completed");
        var totalPurchaseAmount = await db.PurchaseOrders.Where(x => x.Status != "rejected").SumAsync(x => x.TotalAmount);
        return new { supplierCount, purchaseInProgress, workOrderInProgress, workOrderCompleted, totalPurchaseAmount };
    }
}
