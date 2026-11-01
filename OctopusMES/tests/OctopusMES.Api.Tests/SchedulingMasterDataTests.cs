using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services.Scheduling;

namespace OctopusMES.Api.Tests;

public class SchedulingMasterDataTests
{
    [Fact]
    public async Task UpgradesPhoneResourcesWithProductionCapabilitiesIdempotently()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Resources.AddRange(
            new MesResource { ResourceCode = "RES-PHONE-ASSY-01", ResourceName = "手机装配线 01", CapabilityCodes = "EX-CAP-FINAL-ASM" },
            new MesResource { ResourceCode = "RES-PHONE-TEST-01", ResourceName = "手机测试包装线 01", CapabilityCodes = "EX-CAP-FUNCTION-TEST" });
        await db.SaveChangesAsync();

        MesDbSeeder.EnsureDiscreteManufacturingCapabilities(db).Should().Be(2);
        MesDbSeeder.EnsureDiscreteManufacturingCapabilities(db).Should().Be(0);

        (await db.Resources.SingleAsync(x => x.ResourceCode == "RES-PHONE-ASSY-01")).CapabilityCodes.Should().ContainAll("SMT_LINE", "PHONE_ASSEMBLY");
        (await db.Resources.SingleAsync(x => x.ResourceCode == "RES-PHONE-TEST-01")).CapabilityCodes.Should().ContainAll("SOFTWARE_FLASH", "PHONE_TEST", "AGING_TEST", "PACKAGING");
    }

    [Fact]
    public async Task SchedulingSimulation_ReturnsTaskAndResourceDisplayFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options;
        await using var db = new MesDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var from = new DateTime(2026, 7, 26, 8, 0, 0, DateTimeKind.Utc);
        var order = new MesProductionOrder
        {
            ProductionOrderCode = "PO-DISPLAY-001",
            PlannedQty = 10,
            Status = "draft",
            CreatedAt = from,
            UpdatedAt = from,
        };
        var task = new MesOperationTask
        {
            ProductionOrder = order,
            Sequence = 10,
            OperationCode = "EX-OP-ASSY",
            OperationName = "【示例】整机装配",
            RequiredCapability = "assembly",
            PlannedQty = 10,
            StandardCycleSec = 60,
        };
        var resource = new MesResource
        {
            ResourceCode = "RES-ASSY-01",
            ResourceName = "装配工位 01",
            ResourceKind = "equipment",
            CapabilityCodes = "assembly",
            Status = "available",
        };
        var shift = new MesShift
        {
            ShiftCode = "DAY",
            ShiftName = "白班",
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
        };
        db.OperationTasks.Add(task);
        db.Resources.Add(resource);
        db.Shifts.Add(shift);
        await db.SaveChangesAsync();
        db.ResourceCalendars.Add(new MesResourceCalendar
        {
            ResourceId = resource.ResourceId,
            WorkDate = from.Date,
            ShiftId = shift.ShiftId,
            IsAvailable = true,
        });
        await db.SaveChangesAsync();

        var service = new SchedulingService(db, new ResourceCalendarService(db), new CapacityCalculator());
        var result = await service.SimulateAsync(new SchedulingRequest(order.ProductionOrderId, from, from.AddDays(1)));

        result.Slots.Should().ContainSingle();
        result.Slots[0].TaskCode.Should().Be("EX-OP-ASSY");
        result.Slots[0].TaskName.Should().Be("【示例】整机装配");
        result.Slots[0].ResourceCode.Should().Be("RES-ASSY-01");
        result.Slots[0].ResourceName.Should().Be("装配工位 01");
        result.Slots[0].ResourceKind.Should().Be("equipment");
    }

    [Fact]
    public async Task PersistsProductRoutingResourcesAndCalendar()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MesDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new MesDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var product = new MesProduct
        {
            ProductCode = "P-SCHED-001",
            ProductName = "通用装配件",
            Version = "1.0",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var routing = new MesRouting
        {
            ProductId = product.ProductId,
            RoutingCode = "RT-SCHED-001",
            Version = "1.0",
            Status = "active",
            Operations = new List<MesRoutingOperation>
            {
                new() { Sequence = 10, OperationCode = "OP-10", OperationName = "装配", RequiredCapability = "assembly", StandardCycleSec = 60 },
                new() { Sequence = 20, OperationCode = "OP-20", OperationName = "测试", RequiredCapability = "testing", StandardCycleSec = 45 },
            },
        };
        var workCenter = new MesWorkCenter { WorkCenterCode = "WC-ASSY", WorkCenterName = "装配中心", Status = "active" };
        db.Routings.Add(routing);
        db.WorkCenters.Add(workCenter);
        await db.SaveChangesAsync();

        var resource = new MesResource
        {
            ResourceCode = "RES-ASSY-01",
            ResourceName = "装配工位 01",
            ResourceKind = "equipment",
            WorkCenterId = workCenter.WorkCenterId,
            CapabilityCodes = "assembly",
            Status = "available",
        };
        var shift = new MesShift
        {
            ShiftCode = "DAY",
            ShiftName = "白班",
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            BreakMinutes = 60,
        };
        db.Resources.Add(resource);
        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        db.ResourceCalendars.Add(new MesResourceCalendar
        {
            ResourceId = resource.ResourceId,
            WorkDate = DateTime.Today,
            ShiftId = shift.ShiftId,
            IsAvailable = false,
            Reason = "设备维护",
        });
        await db.SaveChangesAsync();

        var savedRouting = await db.Routings
            .Include(x => x.Operations)
            .SingleAsync(x => x.RoutingId == routing.RoutingId);
        savedRouting.Operations.Select(x => x.Sequence).Should().Equal(10, 20);
        (await db.Resources.SingleAsync(x => x.ResourceCode == "RES-ASSY-01")).WorkCenterId.Should().Be(workCenter.WorkCenterId);
        (await db.ResourceCalendars.SingleAsync(x => x.ResourceId == resource.ResourceId)).IsAvailable.Should().BeFalse();
    }
}
