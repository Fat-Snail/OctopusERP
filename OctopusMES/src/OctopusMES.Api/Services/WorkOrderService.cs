using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

public class WorkOrderService(MesDbContext db)
{
    public async Task<object> GetListAsync(string? status, int pageNum, int pageSize)
    {
        var q = db.WorkOrders.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(x => x.CreatedAt).Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return new { rows = list, total };
    }

    public async Task<MesWorkOrder?> GetByIdAsync(long id) =>
        await db.WorkOrders.Include(x => x.Processes).FirstOrDefaultAsync(x => x.WorkOrderId == id);

    public async Task<MesWorkOrder> CreateAsync(MesWorkOrder req, long userId)
    {
        req.WorkOrderCode = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
        req.Status = "draft";
        req.CompletedQty = 0;
        req.CreatedBy = userId;
        req.CreatedAt = DateTime.UtcNow;
        req.UpdatedAt = DateTime.UtcNow;
        db.WorkOrders.Add(req);
        await db.SaveChangesAsync();
        return req;
    }

    public async Task<MesWorkOrder?> StartAsync(long id)
    {
        var wo = await db.WorkOrders.FindAsync(id);
        if (wo is null || wo.Status != "draft") return null;
        wo.Status = "in_progress";
        wo.ActualStart = DateTime.UtcNow;
        wo.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return wo;
    }

    public async Task<MesWorkOrder?> CompleteAsync(long id, decimal completedQty)
    {
        var wo = await db.WorkOrders.FindAsync(id);
        if (wo is null || wo.Status != "in_progress") return null;
        wo.CompletedQty = completedQty;
        wo.Status = "completed";
        wo.ActualEnd = DateTime.UtcNow;
        wo.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return wo;
    }

    public async Task<MesWorkOrderProcess?> UpdateProcessAsync(long processId, string status)
    {
        var process = await db.WorkOrderProcesses.FindAsync(processId);
        if (process is null) return null;
        process.Status = status;
        if (status == "in_progress" && process.StartedAt is null)
            process.StartedAt = DateTime.UtcNow;
        if (status == "completed" && process.FinishedAt is null)
            process.FinishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return process;
    }
}
