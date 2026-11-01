using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

public sealed record CreateProductionOrderRequest(string? ProductionOrderCode, long ManufacturingSnapshotId, decimal PlannedQty, DateTime? DueDate, int Priority);

public sealed class ProductionOrderService(MesDbContext db)
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedTransitions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = ["planned", "cancelled"],
            ["planned"] = ["released", "cancelled"],
            ["released"] = ["in_progress", "cancelled"],
            ["in_progress"] = ["completed", "cancelled"],
            ["completed"] = [],
            ["cancelled"] = [],
        };

    public async Task<MesProductionOrder> CreateAsync(MesProductionOrder request, long createdBy)
    {
        if (request.PlannedQty <= 0)
            throw new InvalidOperationException("生产数量必须大于 0");

        if (request.PlmSyncRecordId.HasValue)
        {
            var snapshot = await db.PlmProductSyncRecords.AsNoTracking().FirstOrDefaultAsync(x => x.SyncRecordId == request.PlmSyncRecordId.Value)
                ?? throw new InvalidOperationException("PLM 制造快照不存在");
            if (snapshot.Status != "synced" || !snapshot.MesProductId.HasValue || !snapshot.MesRoutingId.HasValue)
                throw new InvalidOperationException("PLM 制造快照尚未同步完成");
            request.ProductId = snapshot.MesProductId;
            request.RoutingId = snapshot.MesRoutingId;
            request.ManufacturingSnapshotId = snapshot.ManufacturingSnapshotId;
        }
        else if (request.ProductId.HasValue && !await db.Products.AnyAsync(x => x.ProductId == request.ProductId.Value))
            throw new InvalidOperationException("产品不存在");

        request.ProductionOrderCode = string.IsNullOrWhiteSpace(request.ProductionOrderCode)
            ? $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24].ToUpperInvariant()
            : request.ProductionOrderCode;
        request.Status = "draft";
        request.CreatedBy = createdBy;
        request.CreatedAt = DateTime.UtcNow;
        request.UpdatedAt = request.CreatedAt;

        db.ProductionOrders.Add(request);
        await db.SaveChangesAsync();
        return request;
    }

    public async Task<MesProductionOrder> CreateFromSnapshotAsync(CreateProductionOrderRequest request, long createdBy)
    {
        if (request.PlannedQty <= 0) throw new InvalidOperationException("生产数量必须大于 0");
        var snapshot = await db.ManufacturingSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.ManufacturingSnapshotId == request.ManufacturingSnapshotId)
            ?? throw new InvalidOperationException("PLM 制造快照不存在");
        if (snapshot.Status != "active") throw new InvalidOperationException("该 PLM 制造快照不可用于新订单");
        return await CreateAsync(new MesProductionOrder
        {
            ProductionOrderCode = request.ProductionOrderCode ?? string.Empty,
            ManufacturingSnapshotId = snapshot.ManufacturingSnapshotId,
            PlannedQty = request.PlannedQty,
            DueDate = request.DueDate,
            Priority = request.Priority,
        }, createdBy);
    }

    public async Task<IReadOnlyList<MesOperationTask>> ExplodeOperationsAsync(long productionOrderId)
    {
        var order = await db.ProductionOrders
            .Include(x => x.Routing)
            .ThenInclude(x => x!.Operations)
            .Include(x => x.ManufacturingSnapshot)
            .ThenInclude(x => x!.Operations)
            .Include(x => x.Tasks)
            .SingleOrDefaultAsync(x => x.ProductionOrderId == productionOrderId)
            ?? throw new InvalidOperationException("生产订单不存在");

        if (order.Tasks.Count > 0)
            throw new InvalidOperationException("生产订单已经展开工序，不能重复展开");
        var sourceOperations = order.ManufacturingSnapshot?.Operations
            .Select(x => new OperationSource(x.Sequence, x.OperationCode, x.OperationName, x.RequiredCapability, x.StandardCycleSec, x.IsSplittable, null))
            .ToList()
            ?? order.Routing?.Operations.Select(x => new OperationSource(x.Sequence, x.OperationCode, x.OperationName, x.RequiredCapability, x.StandardCycleSec, x.IsSplittable, x.RoutingOperationId)).ToList();
        if (sourceOperations?.Count is not > 0)
            throw new InvalidOperationException("生产订单没有有效工艺路线");

        var tasks = new List<MesOperationTask>();
        foreach (var operation in sourceOperations.OrderBy(x => x.Sequence))
        {
            var task = new MesOperationTask
            {
                ProductionOrderId = order.ProductionOrderId,
                RoutingOperationId = operation.RoutingOperationId,
                Sequence = operation.Sequence,
                OperationCode = operation.OperationCode,
                OperationName = operation.OperationName,
                RequiredCapability = operation.RequiredCapability,
                PlannedQty = order.PlannedQty,
                StandardCycleSec = operation.StandardCycleSec,
                IsSplittable = operation.IsSplittable,
                Status = "pending",
            };
            tasks.Add(task);
            db.OperationTasks.Add(task);
        }

        // 让数据库先生成 ID，再回写每个后置任务的前置任务 ID。
        await db.SaveChangesAsync();
        var previousTaskId = (long?)null;
        foreach (var task in tasks)
        {
            task.PredecessorTaskId = previousTaskId;
            previousTaskId = task.OperationTaskId;
        }
        await db.SaveChangesAsync();
        return tasks;
    }

    public async Task SwitchSnapshotAsync(long productionOrderId, long snapshotId, string reason, long changedBy)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("切换制造版本必须填写原因");
        var order = await db.ProductionOrders.Include(x => x.Tasks).SingleOrDefaultAsync(x => x.ProductionOrderId == productionOrderId)
            ?? throw new InvalidOperationException("生产订单不存在");
        if (order.Status is not ("draft" or "planned") || order.Tasks.Any(x => x.ActualStart.HasValue || x.Status is "in_progress" or "completed"))
            throw new InvalidOperationException("只有未开工生产订单可以切换制造版本");
        var snapshot = await db.ManufacturingSnapshots.Include(x => x.Operations).SingleOrDefaultAsync(x => x.ManufacturingSnapshotId == snapshotId)
            ?? throw new InvalidOperationException("目标 PLM 制造快照不存在");
        if (snapshot.Status != "active") throw new InvalidOperationException("目标 PLM 制造快照不可用于新订单");
        var oldSnapshotId = order.ManufacturingSnapshotId ?? throw new InvalidOperationException("生产订单尚未绑定制造快照");
        if (oldSnapshotId == snapshotId) throw new InvalidOperationException("生产订单已经使用该制造版本");

        await using var tx = await db.Database.BeginTransactionAsync();
        db.OperationTasks.RemoveRange(order.Tasks);
        await db.SaveChangesAsync();
        order.Tasks.Clear();
        order.ManufacturingSnapshotId = snapshotId;
        order.UpdatedAt = DateTime.UtcNow;
        db.ProductionOrderVersionAudits.Add(new MesProductionOrderVersionAudit
        {
            ProductionOrderId = order.ProductionOrderId,
            OldSnapshotId = oldSnapshotId,
            NewSnapshotId = snapshotId,
            Reason = reason.Trim(),
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await ExplodeOperationsAsync(order.ProductionOrderId);
        await tx.CommitAsync();
    }

    public async Task TransitionAsync(long productionOrderId, string nextStatus)
    {
        if (!AllowedTransitions.ContainsKey(nextStatus))
            throw new InvalidOperationException($"未知生产订单状态：{nextStatus}");

        var order = await db.ProductionOrders.FindAsync(productionOrderId)
            ?? throw new InvalidOperationException("生产订单不存在");
        var allowed = AllowedTransitions.GetValueOrDefault(order.Status, []);
        if (!allowed.Contains(nextStatus, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"生产订单不能从 {order.Status} 流转到 {nextStatus}");

        order.Status = nextStatus;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private sealed record OperationSource(int Sequence, string OperationCode, string OperationName, string? RequiredCapability, int StandardCycleSec, bool IsSplittable, long? RoutingOperationId);
}
