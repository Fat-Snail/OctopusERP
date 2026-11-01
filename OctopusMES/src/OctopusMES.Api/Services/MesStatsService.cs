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

    public async Task<PlmSyncMetrics> GetPlmSyncMetricsAsync()
    {
        var records = await db.PlmProductSyncRecords.AsNoTracking().ToListAsync();
        var attempts = await db.PlmSyncAttempts.AsNoTracking().OrderBy(x => x.SyncRecordId).ThenBy(x => x.AttemptNumber).ToListAsync();
        var failed = attempts.Count(x => x.Status == "failed");
        var durations = attempts.Select(x => Math.Max(0, (x.FinishedAt - x.StartedAt).TotalSeconds)).ToList();
        var maxConsecutive = 0;
        foreach (var group in attempts.GroupBy(x => x.SyncRecordId))
        {
            var streak = 0;
            foreach (var attempt in group)
            {
                streak = attempt.Status == "failed" ? streak + 1 : 0;
                maxConsecutive = Math.Max(maxConsecutive, streak);
            }
        }
        return new PlmSyncMetrics(
            records.Count(x => x.Status == "pending"), records.Count(x => x.Status == "syncing"),
            records.Count(x => x.Status == "retry_waiting"), records.Count(x => x.Status == "manual_action"),
            records.Count(x => x.Status == "synced"), attempts.Count == 0 ? 0 : (double)failed / attempts.Count,
            durations.Count == 0 ? 0 : Math.Round(durations.Average(), 2),
            records.Where(x => x.SyncedAt.HasValue).Select(x => x.SyncedAt).Max(), maxConsecutive);
    }

    /// <summary>P12-3：工单时效统计（按期完工率/平均交期/排程依从度/合格率）</summary>
    public async Task<WoEfficiencyResponse> GetWoEfficiencyAsync()
    {
        var completed = await db.WorkOrders.Where(x => x.Status == "completed").ToListAsync();
        var totalDone = completed.Count;
        if (totalDone == 0)
            return new WoEfficiencyResponse(0, 0, 0, 0, 0, 0);

        var onTime = completed.Count(x => x.ActualEnd.HasValue && x.PlannedEnd.HasValue && x.ActualEnd <= x.PlannedEnd);
        var avgLeadTime = completed.Where(x => x.LeadTimeHr.HasValue).Select(x => x.LeadTimeHr!.Value).DefaultIfEmpty(0).Average();
        var avgVariance = completed.Where(x => x.ScheduleVarianceHr.HasValue).Select(x => x.ScheduleVarianceHr!.Value).DefaultIfEmpty(0).Average();
        var inProgress = await db.WorkOrders.CountAsync(x => x.Status == "in_progress");

        // 合格率（基于已完工工单的工序）
        var doneIds = completed.Select(c => c.WorkOrderId).ToList();
        var processes = await db.WorkOrderProcesses.Where(p => doneIds.Contains(p.WorkOrderId)).ToListAsync();
        var totalQ = processes.Sum(p => p.QualifiedQty);
        var totalD = processes.Sum(p => p.DefectQty);
        var qualifyRate = (totalQ + totalD) == 0 ? 0 : (double)totalQ / (double)(totalQ + totalD);

        return new WoEfficiencyResponse(
            OnTimeRate: totalDone == 0 ? 0 : (double)onTime / totalDone,
            AvgLeadTimeHr: Math.Round(avgLeadTime, 2),
            AvgScheduleVarianceHr: Math.Round(avgVariance, 2),
            QualifyRate: Math.Round(qualifyRate, 4),
            CompletedCount: totalDone,
            InProgressCount: inProgress);
    }

    /// <summary>P12-3：6 个月工单趋势（按 ActualEnd 月份汇总）</summary>
    public async Task<List<WoTrendPoint>> GetWoTrendAsync(int months = 6)
    {
        var since = DateTime.UtcNow.AddMonths(-months);
        var completed = await db.WorkOrders
            .Where(x => x.Status == "completed" && x.ActualEnd >= since)
            .ToListAsync();

        var grouped = completed
            .GroupBy(x => $"{x.ActualEnd!.Value.Year:D4}-{x.ActualEnd.Value.Month:D2}")
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var total = g.Count();
                var onTime = g.Count(x => x.PlannedEnd.HasValue && x.ActualEnd <= x.PlannedEnd);
                return new WoTrendPoint(g.Key, total, onTime, total == 0 ? 0 : Math.Round((double)onTime / total, 4));
            })
            .ToList();

        return grouped;
    }

    /// <summary>P12-3：设备/工序负载（按工序数 + 总投入工时）</summary>
    public async Task<List<CapacityLoadRow>> GetCapacityLoadAsync()
    {
        var rows = await db.WorkOrderProcesses
            .Where(p => p.EquipmentId.HasValue)
            .GroupBy(p => p.EquipmentId!.Value)
            .Select(g => new
            {
                EquipmentId = g.Key,
                ProcessCount = g.Count(),
                ActiveCount = g.Count(p => p.Status == "in_progress"),
                CompletedCount = g.Count(p => p.Status == "completed"),
                TotalDowntimeMin = g.Sum(p => p.DowntimeMinutes),
            })
            .ToListAsync();

        var equips = await db.Equipments.ToDictionaryAsync(x => x.EquipmentId);

        return rows.Select(r =>
        {
            equips.TryGetValue(r.EquipmentId, out var eq);
            return new CapacityLoadRow(
                EquipmentId: r.EquipmentId,
                EquipmentCode: eq?.EquipmentCode ?? "?",
                EquipmentName: eq?.EquipmentName ?? "?",
                ProcessCount: r.ProcessCount,
                ActiveCount: r.ActiveCount,
                CompletedCount: r.CompletedCount,
                DowntimeMinutes: r.TotalDowntimeMin);
        }).OrderByDescending(x => x.ProcessCount).ToList();
    }

    /// <summary>P12-3：瓶颈识别（按平均节拍偏差排序，ActualCycleSec / StandardCycleSec 比值最大）</summary>
    public async Task<List<BottleneckRow>> GetBottleneckAsync()
    {
        var rows = await db.WorkOrderProcesses
            .Where(p => p.StandardCycleSec.HasValue && p.StandardCycleSec > 0 && p.ActualCycleSec.HasValue)
            .ToListAsync();

        return rows
            .GroupBy(p => p.ProcessName)
            .Select(g =>
            {
                var avgStd = g.Average(p => (double)p.StandardCycleSec!.Value);
                var avgAct = g.Average(p => (double)p.ActualCycleSec!.Value);
                var ratio = avgStd == 0 ? 0 : avgAct / avgStd;
                var totalDownMin = g.Sum(p => p.DowntimeMinutes);
                return new BottleneckRow(g.Key, Math.Round(avgStd, 1), Math.Round(avgAct, 1), Math.Round(ratio, 3), totalDownMin, g.Count());
            })
            .OrderByDescending(x => x.CycleDeviationRatio)
            .ToList();
    }

    /// <summary>P12-3：操作工合格率排名（按 QualifiedQty / (QualifiedQty + DefectQty)）</summary>
    public async Task<List<OperatorRankRow>> GetOperatorRankingAsync()
    {
        var rows = await db.WorkOrderProcesses
            .Where(p => p.OperatorId.HasValue)
            .GroupBy(p => new { p.OperatorId, p.OperatorName })
            .Select(g => new
            {
                g.Key.OperatorId,
                g.Key.OperatorName,
                Qualified = g.Sum(p => p.QualifiedQty),
                Defect = g.Sum(p => p.DefectQty),
                Rework = g.Sum(p => p.ReworkQty),
                Tasks = g.Count(),
            })
            .ToListAsync();

        return rows
            .Select(r => new OperatorRankRow(
                r.OperatorId!.Value,
                r.OperatorName ?? r.OperatorId.Value.ToString(),
                (decimal)r.Qualified,
                (decimal)r.Defect,
                (decimal)r.Rework,
                r.Tasks,
                (r.Qualified + r.Defect) == 0 ? 0 : Math.Round((double)r.Qualified / (double)(r.Qualified + r.Defect), 4)))
            .OrderByDescending(x => x.QualifyRate)
            .ThenByDescending(x => x.QualifiedQty)
            .ToList();
    }

    /// <summary>P12-3：不良品 Pareto（按工序聚合，含累计占比，前端可直接绘制 Pareto 图）</summary>
    public async Task<List<DefectParetoRow>> GetDefectParetoAsync()
    {
        var grouped = await db.WorkOrderProcesses
            .Where(p => p.DefectQty > 0 || p.ReworkQty > 0)
            .GroupBy(p => p.ProcessName)
            .Select(g => new
            {
                ProcessName = g.Key,
                Defect = g.Sum(p => p.DefectQty),
                Rework = g.Sum(p => p.ReworkQty),
                Sample = g.Count(),
            })
            .ToListAsync();

        var total = grouped.Sum(x => x.Defect + x.Rework);
        if (total == 0) return new List<DefectParetoRow>();

        var ordered = grouped
            .OrderByDescending(x => x.Defect + x.Rework)
            .ToList();

        var result = new List<DefectParetoRow>();
        double cum = 0;
        foreach (var r in ordered)
        {
            var loss = (double)(r.Defect + r.Rework);
            var share = Math.Round(loss / (double)total, 4);
            cum = Math.Round(cum + share, 4);
            result.Add(new DefectParetoRow(
                r.ProcessName,
                r.Defect,
                r.Rework,
                r.Defect + r.Rework,
                share,
                cum,
                r.Sample));
        }
        return result;
    }
}

public record WoEfficiencyResponse(
    double OnTimeRate,
    double AvgLeadTimeHr,
    double AvgScheduleVarianceHr,
    double QualifyRate,
    int CompletedCount,
    int InProgressCount);

public record WoTrendPoint(string Month, int Total, int OnTime, double OnTimeRate);

public record CapacityLoadRow(
    long EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    int ProcessCount,
    int ActiveCount,
    int CompletedCount,
    int DowntimeMinutes);

public record BottleneckRow(
    string ProcessName,
    double AvgStandardCycleSec,
    double AvgActualCycleSec,
    double CycleDeviationRatio,
    int TotalDowntimeMinutes,
    int SampleCount);

public record OperatorRankRow(
    long OperatorId,
    string OperatorName,
    decimal QualifiedQty,
    decimal DefectQty,
    decimal ReworkQty,
    int TasksCompleted,
    double QualifyRate);

public record DefectParetoRow(
    string ProcessName,
    decimal DefectQty,
    decimal ReworkQty,
    decimal TotalLoss,
    double Share,          // 当前条目占总损失的比例
    double CumulativeShare,// 累计占比（Pareto 80/20 用）
    int SampleCount);

public sealed record PlmSyncMetrics(int Pending, int Syncing, int RetryWaiting, int ManualAction, int Synced,
    double FailureRate, double AverageDurationSeconds, DateTime? LastSuccessAt, int MaxConsecutiveFailures);
