using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services.Scheduling;

public sealed record SchedulingRequest(long ProductionOrderId, DateTime From, DateTime To, decimal? Quantity = null);
public sealed record UnscheduledTask(long TaskId, string Code, string Message);
public sealed record SchedulingSimulation(IReadOnlyList<CandidateSlot> Slots, IReadOnlyList<UnscheduledTask> Unscheduled, IReadOnlyList<SchedulingConflict> Conflicts);

public interface ISchedulingStrategy
{
    IEnumerable<MesOperationTask> OrderTasks(IEnumerable<MesOperationTask> tasks);
}

public sealed class DueDateFirstSchedulingStrategy : ISchedulingStrategy
{
    public IEnumerable<MesOperationTask> OrderTasks(IEnumerable<MesOperationTask> tasks) =>
        tasks.OrderBy(x => x.Sequence).ThenBy(x => x.OperationTaskId);
}

public sealed class SchedulingService
{
    private readonly MesDbContext _db;
    private readonly ResourceCalendarService _calendar;
    private readonly CapacityCalculator _capacity;
    private readonly ISchedulingStrategy _strategy;

    public SchedulingService(MesDbContext db, ResourceCalendarService calendar, CapacityCalculator capacity)
    {
        _db = db; _calendar = calendar; _capacity = capacity; _strategy = new DueDateFirstSchedulingStrategy();
    }

    public async Task<SchedulingSimulation> SimulateAsync(SchedulingRequest request, CancellationToken ct = default)
    {
        var order = await _db.ProductionOrders.FindAsync([request.ProductionOrderId], ct)
            ?? throw new KeyNotFoundException("生产订单不存在");
        var tasks = _db.OperationTasks.Where(x => x.ProductionOrderId == order.ProductionOrderId)
            .OrderBy(x => x.Sequence).ToList();
        var resources = _db.Resources.Where(x => x.Status == "available").ToList();
        var slots = new List<CandidateSlot>(); var unscheduled = new List<UnscheduledTask>();
        var predecessorEnd = new Dictionary<long, DateTime>();
        foreach (var task in _strategy.OrderTasks(tasks))
        {
            var resource = resources.FirstOrDefault(r => string.IsNullOrWhiteSpace(task.RequiredCapability) ||
                (r.CapabilityCodes ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(task.RequiredCapability, StringComparer.OrdinalIgnoreCase));
            if (resource is null) { unscheduled.Add(new(task.OperationTaskId, "NO_CAPABILITY", "没有匹配的资源能力")); continue; }
            var duration = TimeSpan.FromMinutes(task.StandardCycleSec > 0 ? (double)(task.PlannedQty * task.StandardCycleSec) / 60d : 0);
            var startAt = predecessorEnd.TryGetValue(task.PredecessorTaskId ?? 0, out var end) ? end : request.From;
            var available = await _calendar.GetAvailableSlotsAsync(resource.ResourceId, startAt, request.To, ct);
            var window = available.FirstOrDefault(x => x.End - x.Start >= duration);
            if (window is null) { unscheduled.Add(new(task.OperationTaskId, "NO_CAPACITY", "时间窗口内没有足够产能")); continue; }
            var slot = new CandidateSlot(
                task.OperationTaskId,
                resource.ResourceId,
                window.Start,
                window.Start + duration,
                task.OperationCode,
                task.OperationName,
                resource.ResourceCode,
                resource.ResourceName,
                resource.ResourceKind);
            slots.Add(slot); predecessorEnd[task.OperationTaskId] = slot.End;
        }
        var dependencies = tasks.Where(x => x.PredecessorTaskId.HasValue).Select(x => (x.OperationTaskId, x.PredecessorTaskId!.Value));
        return new(slots, unscheduled, new ConflictDetector().Validate(slots, dependencies, []));
    }
}
