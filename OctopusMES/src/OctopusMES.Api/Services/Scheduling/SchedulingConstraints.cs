using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services.Scheduling;

public sealed record AvailableSlot(DateTime Start, DateTime End);
public sealed record SchedulingConflict(string Code, string Message, long? TaskId = null, long? ResourceId = null);
public sealed record CandidateSlot(
    long TaskId,
    long ResourceId,
    DateTime Start,
    DateTime End,
    string? TaskCode = null,
    string? TaskName = null,
    string? ResourceCode = null,
    string? ResourceName = null,
    string? ResourceKind = null);

public sealed class ResourceCalendarService
{
    private readonly MesDbContext _db;
    public ResourceCalendarService(MesDbContext db) => _db = db;

    public async Task<IReadOnlyList<AvailableSlot>> GetAvailableSlotsAsync(long resourceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var calendars = await _db.ResourceCalendars.AsNoTracking()
            .Where(x => x.ResourceId == resourceId && x.WorkDate >= from.Date && x.WorkDate <= to.Date)
            .Include(x => x.Shift).ToListAsync(ct);
        var result = new List<AvailableSlot>();
        foreach (var day in calendars.GroupBy(x => x.WorkDate.Date))
        {
            foreach (var c in day.Where(x => x.IsAvailable && !x.IsMaintenance))
            {
                var start = day.Key.Add(c.AvailableFrom ?? c.Shift?.StartTime ?? TimeSpan.Zero);
                var end = day.Key.Add(c.AvailableTo ?? c.Shift?.EndTime ?? TimeSpan.FromHours(24));
                var s = start < from ? from : start; var e = end > to ? to : end;
                if (e > s) result.Add(new AvailableSlot(s, e));
            }
        }
        return result.OrderBy(x => x.Start).ToList();
    }
}

public sealed class CapacityCalculator
{
    public double CalculateDurationMinutes(MesRoutingOperation operation, decimal quantity, int resourceCount = 1)
    {
        if (quantity <= 0 || resourceCount <= 0) throw new ArgumentOutOfRangeException();
        return operation.SetupMinutes + (double)(quantity * operation.StandardCycleSec) / 60d / resourceCount;
    }
}

public sealed class ConflictDetector
{
    public IReadOnlyList<SchedulingConflict> Validate(IEnumerable<CandidateSlot> slots, IEnumerable<(long TaskId, long PredecessorTaskId)> dependencies, IEnumerable<CandidateSlot> frozenSlots)
    {
        var result = new List<SchedulingConflict>(); var list = slots.ToList();
        foreach (var pair in list.GroupBy(x => x.ResourceId))
            foreach (var a in pair.OrderBy(x => x.Start).Zip(pair.OrderBy(x => x.Start).Skip(1), (a,b) => (a,b)))
                if (a.b.Start < a.a.End) result.Add(new("RESOURCE_OVERLAP", "资源时间段重叠", a.b.TaskId, a.b.ResourceId));
        foreach (var d in dependencies)
        {
            var p = list.FirstOrDefault(x => x.TaskId == d.PredecessorTaskId); var t = list.FirstOrDefault(x => x.TaskId == d.TaskId);
            if (p is not null && t is not null && t.Start < p.End) result.Add(new("PREDECESSOR_VIOLATION", "前置工序尚未完成", t.TaskId));
        }
        foreach (var f in frozenSlots)
            foreach (var s in list.Where(x => x.ResourceId == f.ResourceId && x.Start < f.End && f.Start < x.End)) result.Add(new("FROZEN_SLOT", "命中冻结时段", s.TaskId, s.ResourceId));
        return result;
    }
}
