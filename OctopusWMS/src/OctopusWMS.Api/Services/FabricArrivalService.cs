using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;

namespace OctopusWMS.Api.Services;

public class FabricArrivalService(WmsDbContext db, WebhookSender webhook)
{
    public async Task<object> ListAsync(string? status, long? mesScheduleId, long? mesOrderId, int pageNum, int pageSize)
    {
        var q = db.FabricArrivals.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        if (mesScheduleId.HasValue) q = q.Where(x => x.MesScheduleId == mesScheduleId.Value);
        if (mesOrderId.HasValue) q = q.Where(x => x.MesOrderId == mesOrderId.Value);

        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((pageNum - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return new { rows, total };
    }

    public async Task<WmsFabricArrival?> GetByIdAsync(long id) =>
        await db.FabricArrivals.FindAsync(id);

    public async Task<WmsFabricArrival> CreateAsync(WmsFabricArrival req, long createdBy)
    {
        req.ArrivalCode = $"FA-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        req.Status = string.IsNullOrWhiteSpace(req.Status) ? "pending" : req.Status;
        req.CreatedBy = createdBy;
        req.CreatedAt = DateTime.UtcNow;
        req.UpdatedAt = DateTime.UtcNow;
        db.FabricArrivals.Add(req);
        await db.SaveChangesAsync();
        return req;
    }

    /// <summary>
    /// 标记到货完成。入库面料 + 通知 MES 更新 GarmentSchedule.FabricStatus
    /// </summary>
    public async Task<(WmsFabricArrival? Arrival, string? Error)> MarkArrivedAsync(
        long id, decimal receivedQty, long warehouseId)
    {
        var arrival = await db.FabricArrivals.FindAsync(id);
        if (arrival == null) return (null, "回布单不存在");
        if (arrival.Status == "ok") return (arrival, "已是已到齐状态");

        arrival.ReceivedQty = receivedQty;
        arrival.ActualArrivalDate = DateTime.UtcNow;
        arrival.Status = receivedQty >= arrival.ExpectedQty ? "ok" : "partial";
        arrival.UpdatedAt = DateTime.UtcNow;

        // 入面料库
        var inv = await db.FabricInventories.FirstOrDefaultAsync(x =>
            x.WarehouseId == warehouseId &&
            x.FabricCode == arrival.FabricCode &&
            x.ColorCode == arrival.ColorCode &&
            x.BatchCode == arrival.BatchCode);
        if (inv == null)
        {
            inv = new WmsFabricInventory
            {
                WarehouseId = warehouseId,
                FabricCode = arrival.FabricCode,
                ColorCode = arrival.ColorCode,
                BatchCode = arrival.BatchCode,
                Quantity = receivedQty,
                UpdatedAt = DateTime.UtcNow,
            };
            db.FabricInventories.Add(inv);
        }
        else
        {
            inv.Quantity += receivedQty;
            inv.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // 通知 MES 更新排程的 FabricStatus
        if (arrival.MesScheduleId.HasValue || arrival.MesOrderId.HasValue)
        {
            var statusText = arrival.Status == "ok"
                ? $"{arrival.ActualArrivalDate:M-d}/ok"
                : $"{arrival.ActualArrivalDate:M-d}/部分到货 {receivedQty}/{arrival.ExpectedQty}";

            webhook.PostToMes("/api/webhook/fabric-arrived", new
            {
                MesScheduleId = arrival.MesScheduleId,
                MesOrderId = arrival.MesOrderId,
                ArrivalCode = arrival.ArrivalCode,
                FabricStatus = statusText,
                IsComplete = arrival.Status == "ok",
                ActualArrivalDate = arrival.ActualArrivalDate,
            });
        }

        return (arrival, null);
    }
}
