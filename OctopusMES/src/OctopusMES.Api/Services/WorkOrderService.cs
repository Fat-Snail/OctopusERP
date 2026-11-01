using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

public class WorkOrderService(MesDbContext db, WebhookSender webhook)
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

        // P12：完工时计算时效指标
        if (wo.ActualStart.HasValue)
            wo.LeadTimeHr = (wo.ActualEnd!.Value - wo.ActualStart.Value).TotalHours;
        if (wo.PlannedEnd.HasValue)
            wo.ScheduleVarianceHr = (wo.ActualEnd!.Value - wo.PlannedEnd.Value).TotalHours;

        await db.SaveChangesAsync();

        webhook.PostToWms("/api/webhook/receive-goods", new
        {
            MesWorkOrderId = wo.WorkOrderId,
            PlmProductId = wo.PlmProductId,
            ProductName = wo.ProductName,
            Spec = wo.Spec,
            Unit = wo.Unit,
            Quantity = completedQty
        });

        // P9：推送工单完工通知到 UMC 统一通知中心（创建人在所有子系统的铃铛都能看到）
        if (wo.CreatedBy > 0)
        {
            var msg = wo.ScheduleVarianceHr.HasValue
                ? $"实际完工 {wo.ActualEnd:yyyy-MM-dd HH:mm}，{(wo.ScheduleVarianceHr.Value > 0 ? $"延期 {wo.ScheduleVarianceHr.Value:F1}h" : "按期完成")}"
                : null;
            webhook.PostToUmcNotify(wo.CreatedBy, "mes", "workorder_done",
                $"工单完工：{wo.WorkOrderCode} {wo.ProductName}",
                message: msg,
                linkUrl: $"/workorder/detail/{wo.WorkOrderId}");
        }
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

    /// <summary>P12：工序报工（PDA 扫码用）— 提交合格/不良数量、操作工、停机原因等</summary>
    public async Task<MesWorkOrderProcess?> ReportProcessAsync(long processId, ReportProcessInput input)
    {
        var process = await db.WorkOrderProcesses.FindAsync(processId);
        if (process is null) return null;

        if (process.StartedAt is null) process.StartedAt = DateTime.UtcNow;

        process.QualifiedQty += input.QualifiedQty;
        process.DefectQty += input.DefectQty;
        process.ReworkQty += input.ReworkQty;
        process.DowntimeMinutes += input.DowntimeMinutes;
        if (!string.IsNullOrEmpty(input.DowntimeReason)) process.DowntimeReason = input.DowntimeReason;
        if (input.OperatorId.HasValue) process.OperatorId = input.OperatorId;
        if (!string.IsNullOrEmpty(input.OperatorName)) process.OperatorName = input.OperatorName;
        if (input.EquipmentId.HasValue) process.EquipmentId = input.EquipmentId;
        if (input.ActualCycleSec.HasValue) process.ActualCycleSec = input.ActualCycleSec;

        if (input.Finish)
        {
            process.Status = "completed";
            process.FinishedAt = DateTime.UtcNow;
        }
        else if (process.Status == "pending")
        {
            process.Status = "in_progress";
        }

        await db.SaveChangesAsync();
        return process;
    }

    public async Task<List<MesEquipment>> GetEquipmentsAsync() =>
        await db.Equipments.OrderBy(e => e.EquipmentId).ToListAsync();

    public async Task<MesEquipment> CreateEquipmentAsync(MesEquipment eq)
    {
        eq.CreatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(eq.EquipmentCode))
            eq.EquipmentCode = $"EQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
        db.Equipments.Add(eq);
        await db.SaveChangesAsync();
        return eq;
    }

    public async Task<MesEquipment?> UpdateEquipmentStatusAsync(long id, string status)
    {
        var eq = await db.Equipments.FindAsync(id);
        if (eq is null) return null;
        eq.Status = status;
        await db.SaveChangesAsync();
        return eq;
    }
}

public record ReportProcessInput(
    decimal QualifiedQty,
    decimal DefectQty,
    decimal ReworkQty,
    int DowntimeMinutes,
    string? DowntimeReason,
    long? OperatorId,
    string? OperatorName,
    long? EquipmentId,
    int? ActualCycleSec,
    bool Finish);
