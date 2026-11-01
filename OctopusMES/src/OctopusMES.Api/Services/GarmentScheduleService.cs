using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

public class GarmentScheduleService(MesDbContext db)
{
    /// <summary>
    /// 归档截止日提供者。默认：今天 - 1 年。
    /// 测试可覆盖：<c>GarmentScheduleService.CutoffProvider = () => new DateTime(2020,1,1);</c>
    /// </summary>
    public static Func<DateTime> CutoffProvider { get; set; } =
        () => DateTime.UtcNow.Date.AddYears(-1);

    /// <summary>归档截止日：早于此日的排程为归档，只读。</summary>
    public static DateTime ArchiveCutoff() => CutoffProvider();

    /// <summary>判断排程是否已归档</summary>
    public static bool IsArchived(DateTime scheduleDate) => scheduleDate.Date < ArchiveCutoff();

    /// <summary>
    /// 日历视图：按团队 × 日期返回一段时间内的排程数据。
    /// 返回结构：{ teams: [...], dates: [...], cells: { "{teamId}_{yyyy-MM-dd}": {...} }, archiveCutoff }
    /// </summary>
    public async Task<object> GetCalendarAsync(DateTime startDate, DateTime endDate, string? season)
    {
        var teams = await db.GarmentTeams
            .Where(t => t.Status == "active")
            .OrderBy(t => t.SortOrder)
            .Select(t => new
            {
                t.TeamId, t.TeamCode, t.TeamName, t.GroupLabel, t.Leader,
                t.LeaderUmcUserId, t.UmcDeptId, t.IsOutsource, t.OutsourcePartnerName,
            })
            .ToListAsync();

        var q = db.GarmentSchedules
            .Where(s => s.ScheduleDate >= startDate && s.ScheduleDate <= endDate);
        if (!string.IsNullOrWhiteSpace(season))
            q = q.Where(s => s.Season == season);

        var rows = await q.OrderBy(s => s.ScheduleDate).ToListAsync();

        // 日期轴
        var dates = new List<string>();
        for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
            dates.Add(d.ToString("yyyy-MM-dd"));

        var cutoff = ArchiveCutoff();
        var cells = rows.ToDictionary(
            s => $"{s.TeamId}_{s.ScheduleDate:yyyy-MM-dd}",
            s => (object)new
            {
                s.ScheduleId,
                s.TeamId,
                s.Customer,
                s.CustomerId,
                s.OrderNo,
                s.ContractId,
                s.StyleCode,
                s.PlmProductId,
                s.OaApprovalId,
                s.WorkOrderId,
                s.ScheduleStatus,
                dueDate = s.DueDate?.ToString("yyyy-MM-dd"),
                s.PlannedQty,
                s.SewingQty,
                s.CuttingQty,
                s.TotalHours,
                s.Headcount,
                s.AvgWage,
                s.FabricStatus,
                s.Remark,
                archived = s.ScheduleDate.Date < cutoff,
            });

        return new
        {
            teams,
            dates,
            cells,
            archiveCutoff = cutoff.ToString("yyyy-MM-dd"),
        };
    }

    /// <summary>
    /// 归档概览：按年→月聚合返回每月排程条数，用于年度归档浏览面板。
    /// 仅返回归档日（今天-1年）之前的数据。
    /// </summary>
    public async Task<object> GetArchiveSummaryAsync()
    {
        var cutoff = ArchiveCutoff();
        var archived = await db.GarmentSchedules
            .Where(s => s.ScheduleDate < cutoff)
            .Select(s => new { s.ScheduleDate, s.Season })
            .ToListAsync();

        var byYear = archived
            .GroupBy(x => x.ScheduleDate.Year)
            .OrderByDescending(g => g.Key)
            .Select(g => new
            {
                year = g.Key,
                total = g.Count(),
                seasons = g.Select(x => x.Season).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                months = g
                    .GroupBy(x => x.ScheduleDate.Month)
                    .OrderBy(mg => mg.Key)
                    .Select(mg => new { month = mg.Key, count = mg.Count() })
                    .ToList(),
            })
            .ToList();

        return new
        {
            archiveCutoff = cutoff.ToString("yyyy-MM-dd"),
            years = byYear,
            totalArchived = archived.Count,
        };
    }

    public async Task<object> ListTeamsAsync()
    {
        var teams = await db.GarmentTeams
            .OrderBy(t => t.SortOrder)
            .Select(t => new
            {
                t.TeamId,
                t.TeamCode,
                t.TeamName,
                t.GroupLabel,
                t.Leader,
                t.LeaderUmcUserId,
                t.UmcDeptId,
                t.IsOutsource,
                t.OutsourcePartnerName,
                t.Headcount,
                t.Status,
                t.SortOrder,
            })
            .ToListAsync();
        return new { rows = teams, total = teams.Count };
    }

    public async Task<object> ListSchedulesAsync(string? keyword, string? season, DateTime? from, DateTime? to,
        long? teamId, int pageNum, int pageSize)
    {
        var q = db.GarmentSchedules.Include(s => s.Team).AsQueryable();
        if (!string.IsNullOrWhiteSpace(season)) q = q.Where(s => s.Season == season);
        if (from.HasValue) q = q.Where(s => s.ScheduleDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(s => s.ScheduleDate <= to.Value.Date);
        if (teamId.HasValue) q = q.Where(s => s.TeamId == teamId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(s =>
                (s.Customer ?? "").Contains(keyword) ||
                (s.OrderNo ?? "").Contains(keyword) ||
                (s.StyleCode ?? "").Contains(keyword));

        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(s => s.ScheduleDate)
            .ThenBy(s => s.Team!.SortOrder)
            .Skip((pageNum - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.ScheduleId,
                s.TeamId,
                teamCode = s.Team!.TeamCode,
                teamName = s.Team.TeamName,
                leader = s.Team.Leader,
                isOutsource = s.Team.IsOutsource,
                outsourcePartnerName = s.Team.OutsourcePartnerName,
                scheduleDate = s.ScheduleDate.ToString("yyyy-MM-dd"),
                s.Season,
                s.Customer,
                s.CustomerId,
                s.OrderNo,
                s.ContractId,
                s.StyleCode,
                s.PlmProductId,
                s.OaApprovalId,
                s.WorkOrderId,
                s.ScheduleStatus,
                dueDate = s.DueDate.HasValue ? s.DueDate.Value.ToString("yyyy-MM-dd") : null,
                s.PlannedQty,
                s.SewingQty,
                s.CuttingQty,
                s.TotalHours,
                s.Headcount,
                s.AvgWage,
                s.FabricStatus,
                s.Remark,
            })
            .ToListAsync();

        return new { rows, total };
    }

    public async Task<MesGarmentSchedule?> GetByIdAsync(long id) =>
        await db.GarmentSchedules.Include(s => s.Team).FirstOrDefaultAsync(s => s.ScheduleId == id);

    /// <summary>
    /// 创建/更新结果（带错误信息），用于在归档/校验失败时返回业务错误而非抛异常。
    /// </summary>
    public record CreateResult(MesGarmentSchedule? Schedule, string? Error);

    public async Task<CreateResult> CreateAsync(MesGarmentSchedule req, long createdBy)
    {
        if (IsArchived(req.ScheduleDate))
            return new CreateResult(null, $"该日期 {req.ScheduleDate:yyyy-MM-dd} 已归档（早于 {ArchiveCutoff():yyyy-MM-dd}），不可创建排程");

        // 唯一索引 (TeamId, ScheduleDate)：upsert
        var exist = await db.GarmentSchedules
            .FirstOrDefaultAsync(s => s.TeamId == req.TeamId && s.ScheduleDate == req.ScheduleDate.Date);
        if (exist != null)
        {
            if (IsArchived(exist.ScheduleDate))
                return new CreateResult(exist, "该排程已归档，不可修改");
            ApplyUpdate(exist, req);
            exist.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return new CreateResult(exist, null);
        }

        req.ScheduleDate = req.ScheduleDate.Date;
        req.CreatedBy = createdBy;
        req.CreatedAt = DateTime.UtcNow;
        req.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(req.Season)) req.Season = "22春夏";
        db.GarmentSchedules.Add(req);
        await db.SaveChangesAsync();
        return new CreateResult(req, null);
    }

    public async Task<CreateResult> UpdateAsync(MesGarmentSchedule req)
    {
        var s = await db.GarmentSchedules.FindAsync(req.ScheduleId);
        if (s == null) return new CreateResult(null, "排程不存在");
        if (IsArchived(s.ScheduleDate))
            return new CreateResult(s, $"该排程 {s.ScheduleDate:yyyy-MM-dd} 已归档，不可修改");
        ApplyUpdate(s, req);
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new CreateResult(s, null);
    }

    /// <summary>
    /// 批量创建/更新排程：日历多选天数后一次保存。
    /// 对每个日期：(TeamId, Date) 已存在 → 更新；否则新建。
    /// </summary>
    public async Task<(int Created, int Updated, List<MesGarmentSchedule> Schedules, string? Error)>
        BatchCreateAsync(BatchCreateRequest req, long createdBy)
    {
        if (req.TeamId <= 0) return (0, 0, new(), "TeamId 必填");
        if (req.Dates == null || req.Dates.Count == 0) return (0, 0, new(), "至少选择一天");
        if (req.Dates.Count > 366) return (0, 0, new(), "一次最多 366 天");

        var team = await db.GarmentTeams.FindAsync(req.TeamId);
        if (team == null) return (0, 0, new(), "组别不存在");

        var distinctDates = req.Dates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();

        // 拒绝任何归档日期
        var archived = distinctDates.Where(IsArchived).ToList();
        if (archived.Count > 0)
            return (0, 0, new(),
                $"以下 {archived.Count} 个日期已归档（早于 {ArchiveCutoff():yyyy-MM-dd}），不可批量创建：" +
                string.Join("、", archived.Take(5).Select(d => d.ToString("yyyy-MM-dd"))) +
                (archived.Count > 5 ? "…" : ""));

        var existing = await db.GarmentSchedules
            .Where(s => s.TeamId == req.TeamId && distinctDates.Contains(s.ScheduleDate))
            .ToListAsync();

        var season = string.IsNullOrWhiteSpace(req.Season) ? "22春夏" : req.Season;
        var now = DateTime.UtcNow;
        var created = 0;
        var updated = 0;
        var result = new List<MesGarmentSchedule>();

        foreach (var date in distinctDates)
        {
            var existRow = existing.FirstOrDefault(x => x.ScheduleDate == date);
            if (existRow != null)
            {
                // 更新已存在
                existRow.Customer = req.Customer ?? existRow.Customer;
                existRow.CustomerId = req.CustomerId ?? existRow.CustomerId;
                existRow.OrderNo = req.OrderNo ?? existRow.OrderNo;
                existRow.ContractId = req.ContractId ?? existRow.ContractId;
                existRow.StyleCode = req.StyleCode ?? existRow.StyleCode;
                existRow.PlmProductId = req.PlmProductId ?? existRow.PlmProductId;
                existRow.DueDate = req.DueDate ?? existRow.DueDate;
                if (req.PlannedQty.HasValue) existRow.PlannedQty = req.PlannedQty;
                existRow.FabricStatus = req.FabricStatus ?? existRow.FabricStatus;
                existRow.Remark = req.Remark ?? existRow.Remark;
                existRow.Season = season;
                existRow.UpdatedAt = now;
                updated++;
                result.Add(existRow);
            }
            else
            {
                var fresh = new MesGarmentSchedule
                {
                    TeamId = req.TeamId,
                    ScheduleDate = date,
                    Season = season,
                    Customer = req.Customer,
                    CustomerId = req.CustomerId,
                    OrderNo = req.OrderNo,
                    ContractId = req.ContractId,
                    StyleCode = req.StyleCode,
                    PlmProductId = req.PlmProductId,
                    DueDate = req.DueDate,
                    PlannedQty = req.PlannedQty,
                    FabricStatus = req.FabricStatus,
                    Remark = req.Remark,
                    ScheduleStatus = "draft",
                    CreatedBy = createdBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.GarmentSchedules.Add(fresh);
                created++;
                result.Add(fresh);
            }
        }

        await db.SaveChangesAsync();
        return (created, updated, result, null);
    }

    public class BatchCreateRequest
    {
        public long TeamId { get; set; }
        public List<DateTime> Dates { get; set; } = new();
        public string? Season { get; set; }
        public string? Customer { get; set; }
        public long? CustomerId { get; set; }
        public string? OrderNo { get; set; }
        public long? ContractId { get; set; }
        public string? StyleCode { get; set; }
        public long? PlmProductId { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal? PlannedQty { get; set; }
        public string? FabricStatus { get; set; }
        public string? Remark { get; set; }
    }

    public record DeleteResult(bool Ok, string? Error);

    public async Task<DeleteResult> DeleteAsync(long id)
    {
        var s = await db.GarmentSchedules.FindAsync(id);
        if (s == null) return new DeleteResult(false, "排程不存在");
        if (IsArchived(s.ScheduleDate))
            return new DeleteResult(false, $"该排程 {s.ScheduleDate:yyyy-MM-dd} 已归档，不可删除");
        db.GarmentSchedules.Remove(s);
        await db.SaveChangesAsync();
        return new DeleteResult(true, null);
    }

    /// <summary>
    /// 发布排程：状态 draft → pending_approval，记录 OA 审批单 ID。
    /// 实际部署时由本方法发起 OA 审批 API 调用得到真实 ApprovalId；演示态用占位 ID。
    /// </summary>
    public async Task<(MesGarmentSchedule? Schedule, string? Error)> PublishAsync(long scheduleId, long oaApprovalId)
    {
        var s = await db.GarmentSchedules.FindAsync(scheduleId);
        if (s == null) return (null, "排程不存在");
        if (IsArchived(s.ScheduleDate))
            return (s, $"该排程 {s.ScheduleDate:yyyy-MM-dd} 已归档，不可发布");
        if (s.ScheduleStatus != "draft" && s.ScheduleStatus != "rejected")
            return (s, "仅草稿/驳回状态可发布");

        s.ScheduleStatus = "pending_approval";
        s.OaApprovalId = oaApprovalId;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (s, null);
    }

    /// <summary>
    /// 处理 OA 审批结果回调。approved=true → 创建工单 + 状态 approved；approved=false → rejected。
    /// </summary>
    public async Task<(MesGarmentSchedule? Schedule, MesWorkOrder? WorkOrder, string? Error)>
        HandleApprovalCallbackAsync(long oaApprovalId, bool approved)
    {
        var s = await db.GarmentSchedules.FirstOrDefaultAsync(x => x.OaApprovalId == oaApprovalId);
        if (s == null) return (null, null, "未找到对应排程");
        if (s.ScheduleStatus != "pending_approval")
            return (s, null, $"当前状态 {s.ScheduleStatus} 不可处理");

        if (!approved)
        {
            s.ScheduleStatus = "rejected";
            s.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return (s, null, null);
        }

        // approved → 自动建 MES 工单
        var wo = new MesWorkOrder
        {
            WorkOrderCode = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            PlmProductId = s.PlmProductId,
            ProductName = s.StyleCode ?? "服装款式",
            Spec = $"{s.Customer}-{s.OrderNo}",
            PlannedQty = s.PlannedQty ?? 0,
            CompletedQty = 0,
            Unit = "件",
            Status = "draft",
            PlannedStart = s.ScheduleDate,
            PlannedEnd = s.DueDate ?? s.ScheduleDate.AddDays(7),
            WorkshopName = $"组排-{s.TeamId}",
            CreatedBy = s.CreatedBy ?? 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.WorkOrders.Add(wo);
        await db.SaveChangesAsync();

        s.WorkOrderId = wo.WorkOrderId;
        s.ScheduleStatus = "approved";
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (s, wo, null);
    }

    /// <summary>月度汇总：按组×月度的车缝/裁床/預算汇总(对应汇总 sheet)</summary>
    public async Task<object> GetMonthlySummaryAsync(string? season)
    {
        var q = db.GarmentSchedules.Include(s => s.Team).AsQueryable();
        if (!string.IsNullOrWhiteSpace(season)) q = q.Where(s => s.Season == season);
        var rows = await q.ToListAsync();

        var summary = rows
            .GroupBy(s => new { s.TeamId, TeamName = s.Team!.TeamName, Year = s.ScheduleDate.Year, Month = s.ScheduleDate.Month })
            .Select(g => new
            {
                g.Key.TeamId,
                teamName = g.Key.TeamName,
                yearMonth = $"{g.Key.Year}-{g.Key.Month:D2}",
                plannedQty = g.Sum(x => x.PlannedQty ?? 0),
                sewingQty = g.Sum(x => x.SewingQty ?? 0),
                cuttingQty = g.Sum(x => x.CuttingQty ?? 0),
                achievementRate = g.Sum(x => x.PlannedQty ?? 0) == 0
                    ? 0
                    : Math.Round(g.Sum(x => x.SewingQty ?? 0) / g.Sum(x => x.PlannedQty ?? 0) * 100, 2),
            })
            .OrderBy(x => x.teamName).ThenBy(x => x.yearMonth)
            .ToList();

        return new { rows = summary, total = summary.Count };
    }

    private static void ApplyUpdate(MesGarmentSchedule target, MesGarmentSchedule src)
    {
        target.Customer = src.Customer;
        target.OrderNo = src.OrderNo;
        target.StyleCode = src.StyleCode;
        target.PlmProductId = src.PlmProductId;
        target.DueDate = src.DueDate;
        target.PlannedQty = src.PlannedQty;
        target.SewingQty = src.SewingQty;
        target.CuttingQty = src.CuttingQty;
        target.TotalHours = src.TotalHours;
        target.Headcount = src.Headcount;
        target.AvgWage = src.AvgWage;
        target.FabricStatus = src.FabricStatus;
        target.Remark = src.Remark;
        if (!string.IsNullOrWhiteSpace(src.Season)) target.Season = src.Season;
    }
}
