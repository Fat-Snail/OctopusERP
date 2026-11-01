using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services.Scheduling;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/scheduling")]
public sealed class SchedulingController(MesDbContext db, SchedulingService scheduling) : ControllerBase
{
    public sealed record SimulateRequest(long ProductionOrderId, DateTime From, DateTime To);
    public sealed record PlanSlotDto(long ScheduleSlotId, long TaskId, string TaskCode, string TaskName,
        long ResourceId, string ResourceCode, string ResourceName, string ResourceKind,
        DateTime Start, DateTime End, string Status);
    public sealed record PlanDto(long SchedulePlanId, string PlanCode, long ProductionOrderId,
        string ProductionOrderCode, string Status, string Strategy, DateTime CreatedAt,
        DateTime? PublishedAt, DateTime? FrozenUntil, IReadOnlyList<PlanSlotDto> Slots);

    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate(SimulateRequest request, CancellationToken ct)
    {
        if (request.To <= request.From) return Ok(new { code = 400, msg = "结束时间必须晚于开始时间", data = (object?)null });
        var result = await scheduling.SimulateAsync(new(request.ProductionOrderId, request.From, request.To), ct);
        return Ok(new { code = 200, msg = "ok", data = result });
    }

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan(SimulateRequest request, CancellationToken ct)
    {
        if (request.To <= request.From) return Ok(new { code = 400, msg = "结束时间必须晚于开始时间", data = (object?)null });
        var result = await scheduling.SimulateAsync(new(request.ProductionOrderId, request.From, request.To), ct);
        if (result.Slots.Count == 0)
            return Ok(new { code = 400, msg = "没有可保存的排程时段", data = new { result.Unscheduled, result.Conflicts } });
        var plan = new MesSchedulePlan { PlanCode = $"SIM-{DateTime.UtcNow:yyyyMMddHHmmssfff}", ProductionOrderId = request.ProductionOrderId, CreatedAt = DateTime.UtcNow };
        plan.Slots = result.Slots.Select(x => new MesScheduleSlot { OperationTaskId = x.TaskId, ResourceId = x.ResourceId, StartAt = x.Start, EndAt = x.End }).ToList();
        db.SchedulePlans.Add(plan); await db.SaveChangesAsync(ct);
        var data = await BuildPlanDtoAsync(plan.SchedulePlanId, ct);
        return Ok(new { code = 200, msg = "计划已创建", data = new
        {
            data!.SchedulePlanId, data.PlanCode, data.ProductionOrderId, data.ProductionOrderCode,
            data.Status, data.Strategy, data.CreatedAt, data.PublishedAt, data.FrozenUntil,
            data.Slots, result.Unscheduled, result.Conflicts,
        } });
    }

    [HttpGet("plans")]
    public async Task<IActionResult> List([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        if (to <= from) return Ok(new { code = 400, msg = "结束时间必须晚于开始时间", data = Array.Empty<PlanDto>() });
        var ids = await db.ScheduleSlots.AsNoTracking()
            .Where(x => x.StartAt < to && x.EndAt > from)
            .Select(x => x.SchedulePlanId).Distinct().ToListAsync(ct);
        var rows = new List<PlanDto>();
        foreach (var id in ids)
        {
            var row = await BuildPlanDtoAsync(id, ct);
            if (row is not null) rows.Add(row);
        }
        return Ok(new { code = 200, msg = "ok", data = rows.OrderBy(x => x.Slots.Min(s => s.Start)).ToList() });
    }

    [HttpPost("plans/{id:long}/publish")]
    public async Task<IActionResult> Publish(long id, CancellationToken ct)
    {
        var plan = await db.SchedulePlans.FindAsync([id], ct);
        if (plan is null) return Ok(new { code = 404, msg = "计划不存在", data = (object?)null });
        if (plan.Status != "simulation" && plan.Status != "draft") return Ok(new { code = 400, msg = "当前计划不可发布", data = plan });
        if (!await db.ScheduleSlots.AnyAsync(x => x.SchedulePlanId == id, ct))
            return Ok(new { code = 400, msg = "计划没有可发布的排程时段", data = (object?)null });
        plan.Status = "published"; plan.PublishedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        return Ok(new { code = 200, msg = "计划已发布", data = await BuildPlanDtoAsync(id, ct) });
    }

    [HttpPost("plans/{id:long}/freeze")]
    public async Task<IActionResult> Freeze(long id, [FromQuery] DateTime until, CancellationToken ct)
    {
        var plan = await db.SchedulePlans.FindAsync([id], ct);
        if (plan is null) return Ok(new { code = 404, msg = "计划不存在", data = (object?)null });
        if (plan.Status != "published") return Ok(new { code = 400, msg = "只有已发布计划可以冻结", data = plan });
        plan.Status = "frozen"; plan.FrozenUntil = until; await db.SaveChangesAsync(ct);
        return Ok(new { code = 200, msg = "计划已冻结", data = await BuildPlanDtoAsync(id, ct) });
    }

    [HttpGet("plans/{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var plan = await BuildPlanDtoAsync(id, ct);
        return plan is null ? Ok(new { code = 404, msg = "计划不存在", data = (object?)null }) : Ok(new { code = 200, msg = "ok", data = plan });
    }

    private async Task<PlanDto?> BuildPlanDtoAsync(long id, CancellationToken ct)
    {
        var plan = await db.SchedulePlans.AsNoTracking().FirstOrDefaultAsync(x => x.SchedulePlanId == id, ct);
        if (plan is null) return null;
        var order = await db.ProductionOrders.AsNoTracking().FirstAsync(x => x.ProductionOrderId == plan.ProductionOrderId, ct);
        var slots = await (
            from slot in db.ScheduleSlots.AsNoTracking()
            join task in db.OperationTasks.AsNoTracking() on slot.OperationTaskId equals task.OperationTaskId
            join resource in db.Resources.AsNoTracking() on slot.ResourceId equals resource.ResourceId
            where slot.SchedulePlanId == id
            orderby slot.StartAt
            select new PlanSlotDto(slot.ScheduleSlotId, task.OperationTaskId, task.OperationCode,
                task.OperationName, resource.ResourceId, resource.ResourceCode, resource.ResourceName,
                resource.ResourceKind, slot.StartAt, slot.EndAt, slot.Status)
        ).ToListAsync(ct);
        return new PlanDto(plan.SchedulePlanId, plan.PlanCode, plan.ProductionOrderId,
            order.ProductionOrderCode, plan.Status, plan.Strategy, plan.CreatedAt,
            plan.PublishedAt, plan.FrozenUntil, slots);
    }
}
