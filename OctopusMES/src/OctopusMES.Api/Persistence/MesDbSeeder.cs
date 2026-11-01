using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctopusMES.Api.Persistence;

public static class MesDbSeeder
{
    public static void Seed(MesDbContext db)
    {
        // P12 后新增 mes_garment_team / mes_garment_schedule 表。
        // 旧 octopus_mes.db 文件下 EnsureCreated 不会增量加表，此处补齐避免崩溃。
        EnsureGarmentTablesExist(db);

        // 列级自愈：B/C/D/E 阶段新增字段(LeaderUmcUserId, OrderId, CustomerId 等)
        // 在已存在的旧表上做 ALTER TABLE ADD COLUMN
        EnsureCompatibleSchema(db);

        // 服装排程种子在 SyncUsers 检查之前调用 — 即使主种子已存在也会幂等补充
        TrySeedGarmentScheduleSafe(db);

        // 主种子 gate：检测 admin (UmcUserId=1) 是否存在；不存在才走下面的全量主种子。
        // 注意：服装组长用 UmcUserId 100-105，不会与 admin/zhangsan/lisi(1/2/3) 冲突
        var hasMainSeed = db.SyncUsers.Any(u => u.UmcUserId == 1);
        if (!hasMainSeed)
        {
            SeedMainData(db);
        }

        // 服装组长 SyncUser 缓存(幂等：以 UmcUserId=100 为 marker) — 主种子后再补
        TrySeedGarmentLeadersSafe(db);

        // 通用离散制造主数据演示种子（幂等）
        EnsureDiscreteManufacturingCapabilities(db);
        TryExtendResourceCalendarsSafe(db);
    }

    public static int EnsureDiscreteManufacturingCapabilities(MesDbContext db)
    {
        var capabilitiesByResource = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["RES-PHONE-ASSY-01"] = ["SMT_LINE", "PHONE_ASSEMBLY"],
            ["RES-PHONE-TEST-01"] = ["SOFTWARE_FLASH", "PHONE_TEST", "AGING_TEST", "PACKAGING"]
        };
        var resources = db.Resources.Where(x => capabilitiesByResource.Keys.Contains(x.ResourceCode)).ToList();
        var changed = 0;
        foreach (var resource in resources)
        {
            var codes = (resource.CapabilityCodes ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!capabilitiesByResource[resource.ResourceCode].Aggregate(false, (added, code) => codes.Add(code) || added)) continue;
            resource.CapabilityCodes = string.Join(',', codes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            changed++;
        }
        if (changed > 0) db.SaveChanges();
        return changed;
    }

    public static int EnsureRollingResourceCalendars(MesDbContext db, DateTime today, int horizonDays = 30)
    {
        var start = today.Date;
        var end = start.AddDays(horizonDays);
        var calendars = db.ResourceCalendars.OrderBy(x => x.WorkDate).ToList();
        var added = 0;

        foreach (var resourceGroup in calendars.GroupBy(x => x.ResourceId))
        {
            var template = resourceGroup.LastOrDefault(x => x.IsAvailable && !x.IsMaintenance);
            if (template is null) continue;

            var existingDates = resourceGroup.Select(x => x.WorkDate.Date).ToHashSet();
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (!existingDates.Add(date)) continue;
                db.ResourceCalendars.Add(new MesResourceCalendar
                {
                    ResourceId = template.ResourceId,
                    WorkDate = date,
                    ShiftId = template.ShiftId,
                    IsAvailable = true,
                    AvailableFrom = template.AvailableFrom,
                    AvailableTo = template.AvailableTo,
                    IsMaintenance = false,
                    Reason = "滚动产能日历"
                });
                added++;
            }
        }

        if (added > 0) db.SaveChanges();
        return added;
    }

    private static void SeedMainData(MesDbContext db)
    {

        db.SyncUsers.AddRange(
            new MesSyncUser { Id = 1, UmcUserId = 1, UserName = "admin", NickName = "超级管理员", Email = "admin@octopus.com", Status = 1, LastSyncAt = DateTime.UtcNow },
            new MesSyncUser { Id = 2, UmcUserId = 2, UserName = "zhangsan", NickName = "张三", Email = "zhangsan@octopus.com", Status = 1, LastSyncAt = DateTime.UtcNow },
            new MesSyncUser { Id = 3, UmcUserId = 3, UserName = "lisi", NickName = "李四", Email = "lisi@octopus.com", Status = 1, LastSyncAt = DateTime.UtcNow }
        );

        db.Suppliers.AddRange(
            new MesSupplier { SupplierId = 1, SupplierCode = "SUP-001", SupplierName = "示例供应商1", ContactName = "联系人1", Phone = "13800138001", Email = "supplier1@example.invalid", Address = "示例省示例市示例区", TaxNumber = "91330100MA00000001", Status = "active", Level = 1, Remark = "A级战略供应商", CreatedAt = DateTime.UtcNow.AddDays(-365) },
            new MesSupplier { SupplierId = 2, SupplierCode = "SUP-002", SupplierName = "示例供应商2", ContactName = "联系人2", Phone = "13800138002", Email = "supplier2@example.invalid", Address = "示例省示例市示例区", TaxNumber = "91330100MA00000002", Status = "active", Level = 2, Remark = "B级供应商", CreatedAt = DateTime.UtcNow.AddDays(-180) },
            new MesSupplier { SupplierId = 3, SupplierCode = "SUP-003", SupplierName = "示例供应商3", ContactName = "联系人3", Phone = "13800138003", Email = "supplier3@example.invalid", Address = "示例省示例市示例区", TaxNumber = "91330100MA00000003", Status = "active", Level = 1, Remark = "A级战略供应商", CreatedAt = DateTime.UtcNow.AddDays(-200) },
            new MesSupplier { SupplierId = 4, SupplierCode = "SUP-004", SupplierName = "示例供应商4", ContactName = "联系人4", Phone = "13800138004", Email = "supplier4@example.invalid", Address = "示例省示例市示例区", TaxNumber = "91330100MA00000004", Status = "active", Level = 2, Remark = "屏幕玻璃供应商", CreatedAt = DateTime.UtcNow.AddDays(-90) }
        );

        var now = DateTime.UtcNow;

        db.PurchaseOrders.AddRange(
            new MesPurchaseOrder { PurchaseId = 1, PurchaseCode = "PO-2024-0001", SupplierId = 1, Status = "approved", TotalAmount = 5000000, Currency = "CNY", ExpectedDelivery = now.AddDays(7), OaApprovalId = 3, WmsInboundOrderId = 1, CreatedBy = 2, CreatedAt = now.AddDays(-12), UpdatedAt = now.AddDays(-10) },
            new MesPurchaseOrder { PurchaseId = 2, PurchaseCode = "PO-2024-0002", SupplierId = 2, Status = "submitted", TotalAmount = 2000000, Currency = "CNY", ExpectedDelivery = now.AddDays(14), OaApprovalId = null, WmsInboundOrderId = 2, CreatedBy = 2, CreatedAt = now.AddDays(-5), UpdatedAt = now.AddDays(-4) },
            new MesPurchaseOrder { PurchaseId = 3, PurchaseCode = "PO-2024-0003", SupplierId = 3, Status = "draft", TotalAmount = 800000, Currency = "CNY", ExpectedDelivery = now.AddDays(21), OaApprovalId = null, WmsInboundOrderId = null, CreatedBy = 3, CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) }
        );

        db.PurchaseItems.AddRange(
            new MesPurchaseItem { ItemId = 1, PurchaseId = 1, PlmProductId = 1, ProductName = "iPhone 15 Pro 组件套装", Spec = "256GB 版本", Unit = "套", Quantity = 2000, UnitPrice = 2000, Amount = 4000000 },
            new MesPurchaseItem { ItemId = 2, PurchaseId = 1, PlmProductId = null, ProductName = "铝合金外壳", Spec = "6061-T6", Unit = "kg", Quantity = 5000, UnitPrice = 200, Amount = 1000000 },
            new MesPurchaseItem { ItemId = 3, PurchaseId = 2, PlmProductId = 2, ProductName = "小米 14 组件套装", Spec = "256GB 版本", Unit = "套", Quantity = 2000, UnitPrice = 800, Amount = 1600000 },
            new MesPurchaseItem { ItemId = 4, PurchaseId = 2, PlmProductId = null, ProductName = "屏幕玻璃", Spec = "6.36寸 AMOLED", Unit = "片", Quantity = 2500, UnitPrice = 160, Amount = 400000 },
            new MesPurchaseItem { ItemId = 5, PurchaseId = 3, PlmProductId = null, ProductName = "蓝宝石玻璃", Spec = "高硬度防划伤", Unit = "片", Quantity = 4000, UnitPrice = 200, Amount = 800000 }
        );

        db.WorkOrders.AddRange(
            new MesWorkOrder { WorkOrderId = 1, WorkOrderCode = "WO-2024-0001", PlmProductId = 1, ProductName = "iPhone 15 Pro 整机装配", Spec = "256GB 钛金色", PlannedQty = 1000, CompletedQty = 850, Unit = "台", Status = "in_progress", PlannedStart = now.AddDays(-15), PlannedEnd = now.AddDays(5), ActualStart = now.AddDays(-14), WorkshopName = "华南装配车间A", CreatedBy = 2, CreatedAt = now.AddDays(-20), UpdatedAt = now },
            new MesWorkOrder { WorkOrderId = 2, WorkOrderCode = "WO-2024-0002", PlmProductId = 2, ProductName = "小米 14 整机装配", Spec = "256GB 黑色", PlannedQty = 2000, CompletedQty = 2000, Unit = "台", Status = "completed", PlannedStart = now.AddDays(-30), PlannedEnd = now.AddDays(-10), ActualStart = now.AddDays(-29), ActualEnd = now.AddDays(-11), LeadTimeHr = 18 * 24, ScheduleVarianceHr = -1 * 24, WorkshopName = "华南装配车间B", CreatedBy = 2, CreatedAt = now.AddDays(-35), UpdatedAt = now.AddDays(-11) },
            new MesWorkOrder { WorkOrderId = 3, WorkOrderCode = "WO-2024-0003", PlmProductId = 1, ProductName = "iPhone 15 Pro 整机装配", Spec = "128GB 黑色", PlannedQty = 500, CompletedQty = 0, Unit = "台", Status = "draft", PlannedStart = now.AddDays(10), PlannedEnd = now.AddDays(25), WorkshopName = "华南装配车间A", CreatedBy = 3, CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-2) }
        );

        db.Equipments.AddRange(
            new MesEquipment { EquipmentId = 1, EquipmentCode = "EQ-SMT-01", EquipmentName = "SMT 贴片机 A", Model = "DEK Horizon iX", WorkCenter = "SMT车间", Status = "running", CreatedAt = now },
            new MesEquipment { EquipmentId = 2, EquipmentCode = "EQ-SMT-02", EquipmentName = "SMT 贴片机 B", Model = "ASM Siplace SX", WorkCenter = "SMT车间", Status = "idle", CreatedAt = now },
            new MesEquipment { EquipmentId = 3, EquipmentCode = "EQ-ASS-01", EquipmentName = "整机装配线", Model = "FANUC R-2000iC", WorkCenter = "装配车间", Status = "running", CreatedAt = now },
            new MesEquipment { EquipmentId = 4, EquipmentCode = "EQ-TST-01", EquipmentName = "整机测试台", Model = "Keysight U3811", WorkCenter = "测试中心", Status = "running", CreatedAt = now }
        );

        db.WorkOrderProcesses.AddRange(
            new MesWorkOrderProcess { ProcessId = 1, WorkOrderId = 1, StepOrder = 1, ProcessName = "来料检验", WorkCenter = "QC中心", Status = "completed", StartedAt = now.AddDays(-14), FinishedAt = now.AddDays(-13), QualifiedQty = 1000, DefectQty = 0 },
            new MesWorkOrderProcess { ProcessId = 2, WorkOrderId = 1, StepOrder = 2, ProcessName = "SMT贴片", WorkCenter = "SMT车间", Status = "completed", StartedAt = now.AddDays(-13), FinishedAt = now.AddDays(-8), EquipmentId = 1, OperatorId = 2, OperatorName = "zhangsan", QualifiedQty = 990, DefectQty = 10, StandardCycleSec = 30, ActualCycleSec = 32 },
            new MesWorkOrderProcess { ProcessId = 3, WorkOrderId = 1, StepOrder = 3, ProcessName = "整机装配", WorkCenter = "装配车间", Status = "in_progress", StartedAt = now.AddDays(-8), EquipmentId = 3, OperatorId = 3, OperatorName = "lisi", QualifiedQty = 850, DefectQty = 5, StandardCycleSec = 60, ActualCycleSec = 75, DowntimeMinutes = 30, DowntimeReason = "设备调试" },
            new MesWorkOrderProcess { ProcessId = 4, WorkOrderId = 1, StepOrder = 4, ProcessName = "整机测试", WorkCenter = "测试中心", Status = "pending", EquipmentId = 4 },
            new MesWorkOrderProcess { ProcessId = 5, WorkOrderId = 2, StepOrder = 1, ProcessName = "来料检验", WorkCenter = "QC中心", Status = "completed", StartedAt = now.AddDays(-29), FinishedAt = now.AddDays(-28), QualifiedQty = 2000, DefectQty = 0 },
            new MesWorkOrderProcess { ProcessId = 6, WorkOrderId = 2, StepOrder = 2, ProcessName = "SMT贴片", WorkCenter = "SMT车间", Status = "completed", StartedAt = now.AddDays(-28), FinishedAt = now.AddDays(-20), EquipmentId = 2, OperatorId = 2, OperatorName = "zhangsan", QualifiedQty = 1980, DefectQty = 20, StandardCycleSec = 30, ActualCycleSec = 28 },
            new MesWorkOrderProcess { ProcessId = 7, WorkOrderId = 2, StepOrder = 3, ProcessName = "整机装配", WorkCenter = "装配车间", Status = "completed", StartedAt = now.AddDays(-20), FinishedAt = now.AddDays(-12), EquipmentId = 3, OperatorId = 3, OperatorName = "lisi", QualifiedQty = 2000, DefectQty = 0, StandardCycleSec = 60, ActualCycleSec = 60 },
            new MesWorkOrderProcess { ProcessId = 8, WorkOrderId = 2, StepOrder = 4, ProcessName = "整机测试", WorkCenter = "测试中心", Status = "completed", StartedAt = now.AddDays(-12), FinishedAt = now.AddDays(-11), EquipmentId = 4, OperatorId = 2, OperatorName = "zhangsan", QualifiedQty = 1995, DefectQty = 5, StandardCycleSec = 45, ActualCycleSec = 45 }
        );

        db.SaveChanges();
    }

    // 服装春夏季度排程：装载 22春夏 真实数据 + 演示种子（幂等）
    private static void SeedGarmentSchedule(MesDbContext db)
    {
        if (db.GarmentTeams.Any()) return;

        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Persistence", "seed_garment_22ss.json");
        GarmentSeedPayload? payload = null;
        if (File.Exists(jsonPath))
        {
            try
            {
                var raw = File.ReadAllText(jsonPath);
                payload = JsonSerializer.Deserialize<GarmentSeedPayload>(raw, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MesDbSeeder] 解析 seed_garment_22ss.json 失败：{ex.Message}");
            }
        }

        // 组长名 → UMC UserId 映射(A1 任务在 UMC seed 时已植入 UserId 100-105)
        var leaderToUmcUserId = new Dictionary<string, long>
        {
            ["员工100"] = 100, ["员工101"] = 101, ["员工102"] = 102,
            ["员工103"] = 103, ["员工104"] = 104, ["员工105"] = 105,
        };
        // 团队代码 → UMC 部门 ID(A1 任务植入)
        var codeToDeptId = new Dictionary<string, long>
        {
            ["A"] = 101, ["B"] = 102, ["C"] = 103, ["D"] = 104,
            ["L-104"] = 106, ["L-105"] = 107,
        };
        // 外发组按「非员工工号」识别：匿名化之后不再用具体人名判断
        var outsourceLeaders = new HashSet<string> { "外协01", "外协02", "外协03" };

        var teams = new List<MesGarmentTeam>();
        if (payload?.Teams is { Count: > 0 })
        {
            int order = 0;
            foreach (var t in payload.Teams)
            {
                var isOutsource = outsourceLeaders.Contains(t.Leader);
                teams.Add(new MesGarmentTeam
                {
                    TeamCode = t.Code,
                    TeamName = t.Name,
                    GroupLabel = t.GroupLabel,
                    Leader = t.Leader,
                    LeaderUmcUserId = leaderToUmcUserId.TryGetValue(t.Leader, out var uid) ? uid : null,
                    UmcDeptId = codeToDeptId.TryGetValue(t.Code, out var did) ? did : null,
                    IsOutsource = isOutsource,
                    OutsourcePartnerName = isOutsource ? t.Leader : null,
                    Status = "active",
                    SortOrder = ++order,
                    CreatedAt = DateTime.UtcNow.AddDays(-365),
                });
            }
        }
        else
        {
            // 演示种子（无 JSON 时回退）
            teams.AddRange(new[]
            {
                new MesGarmentTeam { TeamCode = "A", TeamName = "A组-员工100", GroupLabel = "A组", Leader = "员工100", LeaderUmcUserId = 100, UmcDeptId = 101, Status = "active", SortOrder = 1, CreatedAt = DateTime.UtcNow.AddDays(-365) },
                new MesGarmentTeam { TeamCode = "B", TeamName = "B组-员工101", GroupLabel = "B组", Leader = "员工101", LeaderUmcUserId = 101, UmcDeptId = 102, Status = "active", SortOrder = 2, CreatedAt = DateTime.UtcNow.AddDays(-365) },
                new MesGarmentTeam { TeamCode = "C", TeamName = "C组-员工102", GroupLabel = "C组", Leader = "员工102", LeaderUmcUserId = 102, UmcDeptId = 103, Status = "active", SortOrder = 3, CreatedAt = DateTime.UtcNow.AddDays(-365) },
            });
        }
        db.GarmentTeams.AddRange(teams);
        db.SaveChanges();

        var teamIdByCode = db.GarmentTeams.ToDictionary(t => t.TeamCode, t => t.TeamId);
        var schedules = new List<MesGarmentSchedule>();
        var season = payload?.Season ?? "22春夏";
        var now = DateTime.UtcNow;

        if (payload?.Schedules is { Count: > 0 })
        {
            foreach (var s in payload.Schedules)
            {
                if (!teamIdByCode.TryGetValue(s.TeamCode, out var teamId)) continue;
                if (!DateTime.TryParse(s.ScheduleDate, out var date)) continue;
                DateTime? due = null;
                if (!string.IsNullOrWhiteSpace(s.DueDate) && DateTime.TryParse(s.DueDate, out var d)) due = d;

                schedules.Add(new MesGarmentSchedule
                {
                    TeamId = teamId,
                    ScheduleDate = date,
                    Season = season,
                    Customer = string.IsNullOrWhiteSpace(s.Customer) ? null : s.Customer,
                    OrderNo = string.IsNullOrWhiteSpace(s.OrderNo) ? null : s.OrderNo,
                    StyleCode = string.IsNullOrWhiteSpace(s.StyleCode) ? null : s.StyleCode,
                    DueDate = due,
                    PlannedQty = s.PlannedQty,
                    SewingQty = s.SewingQty,
                    CuttingQty = s.CuttingQty,
                    TotalHours = s.TotalHours,
                    Headcount = s.Headcount,
                    AvgWage = s.AvgWage,
                    FabricStatus = string.IsNullOrWhiteSpace(s.FabricStatus) ? null : s.FabricStatus,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
        }
        else
        {
            // 演示种子：未来 5 天的小批量数据
            var demoStart = DateTime.Today;
            var aId = teamIdByCode.GetValueOrDefault("A", 0);
            var bId = teamIdByCode.GetValueOrDefault("B", 0);
            for (int i = 0; i < 5 && aId > 0; i++)
                schedules.Add(new MesGarmentSchedule
                {
                    TeamId = aId, ScheduleDate = demoStart.AddDays(i), Season = season,
                    Customer = "客户001", OrderNo = "SO00001", StyleCode = "SYN00001",
                    DueDate = demoStart.AddDays(20),
                    PlannedQty = 500, SewingQty = 320 + i * 20, CuttingQty = 400 + i * 30,
                    TotalHours = 10, Headcount = 12, AvgWage = 150.00m, FabricStatus = "ok",
                    CreatedAt = now, UpdatedAt = now,
                });
            for (int i = 0; i < 5 && bId > 0; i++)
                schedules.Add(new MesGarmentSchedule
                {
                    TeamId = bId, ScheduleDate = demoStart.AddDays(i), Season = season,
                    Customer = "客户002", OrderNo = "SO00002", StyleCode = "SYN00002",
                    DueDate = demoStart.AddDays(25),
                    PlannedQty = 600, SewingQty = 380 + i * 25, CuttingQty = 500 + i * 30,
                    TotalHours = 10, Headcount = 14, AvgWage = 160.00m, FabricStatus = "ok",
                    CreatedAt = now, UpdatedAt = now,
                });
        }

        if (schedules.Count > 0)
        {
            db.GarmentSchedules.AddRange(schedules);
            db.SaveChanges();
        }
    }

    private class GarmentSeedPayload
    {
        [JsonPropertyName("season")] public string Season { get; set; } = "";
        [JsonPropertyName("teams")] public List<TeamDto> Teams { get; set; } = new();
        [JsonPropertyName("schedules")] public List<ScheduleDto> Schedules { get; set; } = new();
    }

    private class TeamDto
    {
        [JsonPropertyName("code")] public string Code { get; set; } = "";
        [JsonPropertyName("groupLabel")] public string GroupLabel { get; set; } = "";
        [JsonPropertyName("leader")] public string Leader { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }

    private class ScheduleDto
    {
        [JsonPropertyName("teamCode")] public string TeamCode { get; set; } = "";
        [JsonPropertyName("scheduleDate")] public string ScheduleDate { get; set; } = "";
        [JsonPropertyName("customer")] public string? Customer { get; set; }
        [JsonPropertyName("orderNo")] public string? OrderNo { get; set; }
        [JsonPropertyName("styleCode")] public string? StyleCode { get; set; }
        [JsonPropertyName("dueDate")] public string? DueDate { get; set; }
        [JsonPropertyName("plannedQty")] public decimal? PlannedQty { get; set; }
        [JsonPropertyName("sewingQty")] public decimal? SewingQty { get; set; }
        [JsonPropertyName("cuttingQty")] public decimal? CuttingQty { get; set; }
        [JsonPropertyName("totalHours")] public decimal? TotalHours { get; set; }
        [JsonPropertyName("headcount")] public int? Headcount { get; set; }
        [JsonPropertyName("avgWage")] public decimal? AvgWage { get; set; }
        [JsonPropertyName("fabricStatus")] public string? FabricStatus { get; set; }
    }

    // ── Schema 自愈 ───────────────────────────────────────────────────
    // EnsureCreated 仅在数据库不存在时建表。对于已存在的旧 db 文件，新增的实体表会缺失。
    // 这里显式检测并按 EF Core 模型补建缺失的表，避免阻塞 Seed 流程。

    private static void EnsureGarmentTablesExist(MesDbContext db)
    {
        if (db.Database.IsRelational() == false) return;

        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) conn.Open();
        try
        {
            var requiredTables = new[]
            {
                "mes_garment_team", "mes_garment_schedule", "mes_product", "mes_bom",
                "mes_bom_item", "mes_routing", "mes_routing_operation", "mes_work_center",
                "mes_resource", "mes_shift", "mes_resource_calendar", "mes_production_order",
                "mes_operation_task", "mes_schedule_plan", "mes_schedule_slot", "mes_plm_product_sync_record", "mes_plm_sync_event"
            };
            if (requiredTables.Any(table => !TableExists(conn, table)))
            {
                // 用 EF Core 内置 IRelationalDatabaseCreator 重新生成全部 CREATE TABLE 脚本，
                // 然后只对当前不存在的表执行（其它表 IF NOT EXISTS 等价跳过）。
                var creator = (IRelationalDatabaseCreator)db.GetService<IDatabaseCreator>();
                var script = creator.GenerateCreateScript();

                // SQLite 的 GenerateCreateScript 输出形如 "CREATE TABLE \"xxx\" (...);"
                // 我们逐条拆分并加 IF NOT EXISTS 防御
                foreach (var raw in script.Split(';'))
                {
                    var stmt = raw.Trim();
                    if (stmt.Length == 0) continue;
                    if (!stmt.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase) &&
                        !stmt.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase) &&
                        !stmt.StartsWith("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var safe = stmt
                        .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase)
                        .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase)
                        .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase);

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = safe;
                    try { cmd.ExecuteNonQuery(); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MesDbSeeder] 自愈建表跳过：{ex.Message}");
                    }
                }
                Console.WriteLine("[MesDbSeeder] Schema 自愈完成（已补齐缺失的表/索引）");
            }
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    private static bool TableExists(System.Data.Common.DbConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@n LIMIT 1";
        var p = cmd.CreateParameter();
        p.ParameterName = "@n";
        p.Value = tableName;
        cmd.Parameters.Add(p);
        return cmd.ExecuteScalar() != null;
    }

    // 列级自愈：检查旧数据库是否缺少后续版本新增列，缺则 ALTER TABLE ADD COLUMN。
    public static void EnsureCompatibleSchema(MesDbContext db)
    {
        if (!db.Database.IsRelational()) return;

        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) conn.Open();
        try
        {
            // (TableName, ColumnName, ColumnDef)
            var expectedColumns = new (string Table, string Col, string Def)[]
            {
                // mes_garment_team 新增列
                ("mes_garment_team", "LeaderUmcUserId",      "INTEGER NULL"),
                ("mes_garment_team", "UmcDeptId",            "INTEGER NULL"),
                ("mes_garment_team", "IsOutsource",          "INTEGER NOT NULL DEFAULT 0"),
                ("mes_garment_team", "OutsourcePartnerName", "TEXT NULL"),

                // mes_garment_schedule 新增列
                ("mes_garment_schedule", "OrderId",        "INTEGER NULL"),
                ("mes_garment_schedule", "CustomerId",     "INTEGER NULL"),
                ("mes_garment_schedule", "ContractId",     "INTEGER NULL"),
                ("mes_garment_schedule", "OaApprovalId",   "INTEGER NULL"),
                ("mes_garment_schedule", "WorkOrderId",    "INTEGER NULL"),
                ("mes_garment_schedule", "ScheduleStatus", "TEXT NOT NULL DEFAULT 'draft'"),

                // PLM 制造快照接入后，生产订单需要保留创建时使用的同步记录。
                ("mes_production_order", "PlmSyncRecordId", "INTEGER NULL"),
                ("mes_production_order", "ManufacturingSnapshotId", "INTEGER NULL"),
                ("mes_plm_product_sync_record", "ManufacturingSnapshotId", "INTEGER NULL"),
                ("mes_plm_product_sync_record", "NextRetryAt", "TEXT NULL"),
                ("mes_plm_product_sync_record", "LeaseUntil", "TEXT NULL"),
                ("mes_plm_product_sync_record", "LastAttemptAt", "TEXT NULL"),
                ("mes_plm_product_sync_record", "RequiresManualAction", "INTEGER NOT NULL DEFAULT 0"),
            };

            var addedAny = false;
            foreach (var (table, col, def) in expectedColumns)
            {
                if (!TableExists(conn, table)) continue;       // 表不存在前面会建; 此处跳过
                if (ColumnExists(conn, table, col)) continue;
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {col} {def}";
                    cmd.ExecuteNonQuery();
                    addedAny = true;
                    Console.WriteLine($"[MesDbSeeder] 列级自愈：{table}.{col} 已补齐 ({def})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MesDbSeeder] ALTER TABLE 失败 {table}.{col}：{ex.Message}");
                }
            }

            if (!TableExists(conn, "mes_manufacturing_snapshot"))
            {
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS mes_manufacturing_snapshot (
    ManufacturingSnapshotId INTEGER PRIMARY KEY AUTOINCREMENT,
    PlmProductId INTEGER NOT NULL,
    PlmManufacturingVersionId INTEGER NOT NULL,
    ProductCode TEXT NOT NULL,
    ProductName TEXT NOT NULL,
    ProductVersion TEXT NOT NULL,
    BomVersion TEXT NOT NULL,
    RoutingVersion TEXT NOT NULL,
    PayloadHash TEXT NOT NULL,
    Status TEXT NOT NULL,
    SyncedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_mes_manufacturing_snapshot_source ON mes_manufacturing_snapshot(PlmProductId, PlmManufacturingVersionId, PayloadHash);
CREATE INDEX IF NOT EXISTS IX_mes_manufacturing_snapshot_product_status ON mes_manufacturing_snapshot(ProductCode, Status);
CREATE TABLE IF NOT EXISTS mes_snapshot_bom_item (
    SnapshotBomItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    ManufacturingSnapshotId INTEGER NOT NULL,
    LineNo INTEGER NOT NULL,
    MaterialCode TEXT NOT NULL,
    MaterialName TEXT NOT NULL,
    Quantity decimal(18,4) NOT NULL,
    LossRate decimal(9,6) NOT NULL,
    FOREIGN KEY(ManufacturingSnapshotId) REFERENCES mes_manufacturing_snapshot(ManufacturingSnapshotId) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_mes_snapshot_bom_item_line ON mes_snapshot_bom_item(ManufacturingSnapshotId, LineNo);
CREATE TABLE IF NOT EXISTS mes_snapshot_operation (
    SnapshotOperationId INTEGER PRIMARY KEY AUTOINCREMENT,
    ManufacturingSnapshotId INTEGER NOT NULL,
    Sequence INTEGER NOT NULL,
    OperationCode TEXT NOT NULL,
    OperationName TEXT NOT NULL,
    RequiredCapability TEXT NOT NULL,
    StandardCycleSec INTEGER NOT NULL,
    SetupMinutes INTEGER NOT NULL,
    IsSplittable INTEGER NOT NULL,
    FOREIGN KEY(ManufacturingSnapshotId) REFERENCES mes_manufacturing_snapshot(ManufacturingSnapshotId) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_mes_snapshot_operation_sequence ON mes_snapshot_operation(ManufacturingSnapshotId, Sequence);
CREATE TABLE IF NOT EXISTS mes_plm_sync_attempt (
    SyncAttemptId INTEGER PRIMARY KEY AUTOINCREMENT,
    SyncRecordId INTEGER NOT NULL,
    AttemptNumber INTEGER NOT NULL,
    Status TEXT NOT NULL,
    ErrorCode TEXT NULL,
    ErrorMessage TEXT NULL,
    ValidationIssuesJson TEXT NULL,
    Retryable INTEGER NOT NULL,
    StartedAt TEXT NOT NULL,
    FinishedAt TEXT NOT NULL,
    FOREIGN KEY(SyncRecordId) REFERENCES mes_plm_product_sync_record(SyncRecordId) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_mes_plm_sync_attempt_number ON mes_plm_sync_attempt(SyncRecordId, AttemptNumber);
CREATE TABLE IF NOT EXISTS mes_production_order_version_audit (
    VersionAuditId INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductionOrderId INTEGER NOT NULL,
    OldSnapshotId INTEGER NOT NULL,
    NewSnapshotId INTEGER NOT NULL,
    Reason TEXT NOT NULL,
    ChangedBy INTEGER NOT NULL,
    ChangedAt TEXT NOT NULL
);";
                    cmd.ExecuteNonQuery();
                    addedAny = true;
                    Console.WriteLine("[MesDbSeeder] PLM 制造快照与重试表已补齐");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MesDbSeeder] CREATE PLM 制造快照表失败：{ex.Message}");
                }
            }

            // 同时确保 mes_garment_order 表存在(C 阶段加的)
            if (!TableExists(conn, "mes_garment_order"))
            {
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS mes_garment_order (
    OrderId            INTEGER PRIMARY KEY AUTOINCREMENT,
    ContractId         INTEGER NOT NULL,
    CustomerId         INTEGER NOT NULL,
    CustomerName       TEXT NOT NULL,
    OrderNo            TEXT NOT NULL,
    PlmProductId       INTEGER NULL,
    StyleCode          TEXT NULL,
    Quantity           decimal(18,4) NOT NULL,
    ScheduledQty       decimal(18,4) NOT NULL,
    Season             TEXT NULL,
    DueDate            TEXT NULL,
    IsAdditional       INTEGER NOT NULL DEFAULT 0,
    OriginalContractId INTEGER NULL,
    Status             TEXT NOT NULL,
    Remark             TEXT NULL,
    CreatedAt          TEXT NOT NULL,
    UpdatedAt          TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_mes_garment_order_ContractId ON mes_garment_order(ContractId);
CREATE INDEX IF NOT EXISTS IX_mes_garment_order_CustomerId_OrderNo ON mes_garment_order(CustomerId, OrderNo);
CREATE INDEX IF NOT EXISTS IX_mes_garment_order_Status ON mes_garment_order(Status);";
                    cmd.ExecuteNonQuery();
                    addedAny = true;
                    Console.WriteLine("[MesDbSeeder] 列级自愈：mes_garment_order 表已补齐");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MesDbSeeder] CREATE mes_garment_order 失败：{ex.Message}");
                }
            }

            if (addedAny)
            {
                Console.WriteLine("[MesDbSeeder] 列级自愈完成，建议重启 API 让 EF Core 重建模型缓存");
            }
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    private static bool ColumnExists(System.Data.Common.DbConnection conn, string tableName, string columnName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // PRAGMA table_info 第 2 列是列名(index=1)
            var name = reader.GetString(1);
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void TrySeedGarmentScheduleSafe(MesDbContext db)
    {
        try { SeedGarmentSchedule(db); }
        catch (Exception ex)
        {
            Console.WriteLine($"[MesDbSeeder] SeedGarmentSchedule 失败已跳过：{ex.Message}");
        }
    }

    // 服装组长用户缓存（与 UMC seed 中的 UserId 100-105 一致）
    private static void TrySeedGarmentLeadersSafe(MesDbContext db)
    {
        try
        {
            if (db.SyncUsers.Any(u => u.UmcUserId == 100)) return;
            var now = DateTime.UtcNow;
            db.SyncUsers.AddRange(
                new MesSyncUser { UmcUserId = 100, UserName = "worker100", NickName = "员工100", Email = "worker100@example.invalid", Status = 1, LastSyncAt = now },
                new MesSyncUser { UmcUserId = 101, UserName = "worker101", NickName = "员工101", Email = "worker101@example.invalid",  Status = 1, LastSyncAt = now },
                new MesSyncUser { UmcUserId = 102, UserName = "worker102", NickName = "员工102", Email = "worker102@example.invalid", Status = 1, LastSyncAt = now },
                new MesSyncUser { UmcUserId = 103, UserName = "worker103", NickName = "员工103", Email = "worker103@example.invalid",  Status = 1, LastSyncAt = now },
                new MesSyncUser { UmcUserId = 104, UserName = "worker104", NickName = "员工104", Email = "worker104@example.invalid",     Status = 1, LastSyncAt = now },
                new MesSyncUser { UmcUserId = 105, UserName = "worker105", NickName = "员工105", Email = "worker105@example.invalid",   Status = 1, LastSyncAt = now }
            );
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MesDbSeeder] SeedGarmentLeaders 失败已跳过：{ex.Message}");
        }
    }

    private static void TryExtendResourceCalendarsSafe(MesDbContext db)
    {
        try { EnsureRollingResourceCalendars(db, DateTime.Today); }
        catch (Exception ex)
        {
            Console.WriteLine($"[MesDbSeeder] 滚动产能日历补齐失败已跳过：{ex.Message}");
        }
    }
}
