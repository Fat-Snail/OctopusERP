using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Tests;

public sealed class PlmSyncPersistenceTests
{
    private static async Task<(SqliteConnection Connection, MesDbContext Db)> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options;
        var db = new MesDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return (connection, db);
    }

    [Fact]
    public async Task Snapshot_SourceVersionAndHashAreUnique()
    {
        var (connection, db) = await CreateDbAsync();
        await using var _ = connection;
        await using var __ = db;
        db.ManufacturingSnapshots.AddRange(CreateSnapshot(), CreateSnapshot());

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DeletingSnapshot_CascadesOwnedBomAndOperations()
    {
        var (connection, db) = await CreateDbAsync();
        await using var _ = connection;
        await using var __ = db;
        var snapshot = CreateSnapshot();
        snapshot.BomItems.Add(new MesSnapshotBomItem { LineNo = 10, MaterialCode = "MAT-1", MaterialName = "屏幕", Quantity = 1 });
        snapshot.Operations.Add(new MesSnapshotOperation { Sequence = 10, OperationCode = "ASSY", OperationName = "装配", RequiredCapability = "PHONE_ASSY", StandardCycleSec = 60 });
        db.ManufacturingSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        db.ManufacturingSnapshots.Remove(snapshot);
        await db.SaveChangesAsync();

        (await db.SnapshotBomItems.CountAsync()).Should().Be(0);
        (await db.SnapshotOperations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeletingSnapshotReferencedByOrder_IsRestricted()
    {
        var (connection, db) = await CreateDbAsync();
        await using var _ = connection;
        await using var __ = db;
        var snapshot = CreateSnapshot();
        db.ManufacturingSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
        db.ProductionOrders.Add(new MesProductionOrder
        {
            ProductionOrderCode = "PO-SNAPSHOT-001",
            ManufacturingSnapshotId = snapshot.ManufacturingSnapshotId,
            PlannedQty = 1,
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var persistedSnapshot = await db.ManufacturingSnapshots.SingleAsync();
        db.ManufacturingSnapshots.Remove(persistedSnapshot);
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static MesManufacturingSnapshot CreateSnapshot() => new()
    {
        PlmProductId = 2,
        PlmManufacturingVersionId = 3,
        ProductCode = "PHONE-001",
        ProductName = "手机",
        ProductVersion = "V1.0",
        BomVersion = "BOM-V1.0",
        RoutingVersion = "ROUTE-V1.0",
        PayloadHash = new string('a', 64),
        Status = "active",
        SyncedAt = DateTime.UtcNow,
    };
}
