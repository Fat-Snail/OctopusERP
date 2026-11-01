using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Tests;

public sealed class ProductionOrderSnapshotTests
{
    [Fact]
    public async Task CreateAndExplode_UsesImmutableSnapshotOperations()
    {
        var (connection, db, service, snapshot) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;

        var order = await service.CreateFromSnapshotAsync(new(null, snapshot.ManufacturingSnapshotId, 12, DateTime.Today.AddDays(3), 0), 1);
        var tasks = await service.ExplodeOperationsAsync(order.ProductionOrderId);

        order.ManufacturingSnapshotId.Should().Be(snapshot.ManufacturingSnapshotId);
        tasks.Select(x => x.OperationCode).Should().Equal("ASSY", "TEST");
        tasks.Select(x => x.RequiredCapability).Should().Equal("PHONE_ASSY", "PHONE_TEST");
    }

    [Fact]
    public async Task Create_RejectsObsoleteSnapshot()
    {
        var (connection, db, service, snapshot) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;
        snapshot.Status = "obsolete";
        await db.SaveChangesAsync();

        var act = () => service.CreateFromSnapshotAsync(new(null, snapshot.ManufacturingSnapshotId, 1, null, 0), 1);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*不可用于新订单*");
    }

    [Fact]
    public async Task SwitchSnapshot_ReplacesDraftTasksAndWritesAudit()
    {
        var (connection, db, service, first) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;
        var second = Snapshot(4, "V2", "b", "PACK");
        db.ManufacturingSnapshots.Add(second);
        await db.SaveChangesAsync();
        var order = await service.CreateFromSnapshotAsync(new(null, first.ManufacturingSnapshotId, 2, null, 0), 1);
        await service.ExplodeOperationsAsync(order.ProductionOrderId);

        await service.SwitchSnapshotAsync(order.ProductionOrderId, second.ManufacturingSnapshotId, "采用新工艺", 9);

        var reloaded = await db.ProductionOrders.Include(x => x.Tasks).SingleAsync();
        reloaded.ManufacturingSnapshotId.Should().Be(second.ManufacturingSnapshotId);
        reloaded.Tasks.Select(x => x.OperationCode).Should().Equal("PACK");
        var audit = await db.ProductionOrderVersionAudits.SingleAsync();
        audit.OldSnapshotId.Should().Be(first.ManufacturingSnapshotId);
        audit.NewSnapshotId.Should().Be(second.ManufacturingSnapshotId);
        audit.ChangedBy.Should().Be(9);
    }

    [Fact]
    public async Task SwitchSnapshot_RejectsStartedOrder()
    {
        var (connection, db, service, first) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;
        var second = Snapshot(4, "V2", "b", "PACK");
        db.ManufacturingSnapshots.Add(second);
        var order = new MesProductionOrder { ProductionOrderCode = "PO-STARTED", ManufacturingSnapshotId = first.ManufacturingSnapshotId, PlannedQty = 1, Status = "in_progress", CreatedBy = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync();

        var act = () => service.SwitchSnapshotAsync(order.ProductionOrderId, second.ManufacturingSnapshotId, "尝试切换", 1);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*未开工*");
    }

    private static async Task<(SqliteConnection, MesDbContext, ProductionOrderService, MesManufacturingSnapshot)> CreateServiceAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var snapshot = Snapshot(3, "V1", "a", "ASSY", "TEST");
        db.ManufacturingSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
        return (connection, db, new ProductionOrderService(db), snapshot);
    }

    private static MesManufacturingSnapshot Snapshot(long versionId, string version, string hashChar, params string[] operations) => new()
    {
        PlmProductId = 2,
        PlmManufacturingVersionId = versionId,
        ProductCode = "PHONE",
        ProductName = "手机",
        ProductVersion = version,
        BomVersion = $"B-{version}",
        RoutingVersion = $"R-{version}",
        PayloadHash = new string(hashChar[0], 64),
        Status = "active",
        SyncedAt = DateTime.UtcNow,
        Operations = operations.Select((code, i) => new MesSnapshotOperation { Sequence = (i + 1) * 10, OperationCode = code, OperationName = code, RequiredCapability = $"PHONE_{code}", StandardCycleSec = 60, IsSplittable = true }).ToList(),
    };
}
