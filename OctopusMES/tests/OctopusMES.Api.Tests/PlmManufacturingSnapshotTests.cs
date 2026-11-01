using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services.PlmSync;

namespace OctopusMES.Api.Tests;

public sealed class PlmManufacturingSnapshotTests
{
    [Fact]
    public async Task PersistPayload_CreatesCompleteSnapshotAndMarksLedgerSynced()
    {
        var (connection, db, service, record) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;
        var payload = WithHash(ValidPayload());

        var snapshot = await service.PersistPayloadAsync(record.SyncRecordId, payload, CancellationToken.None);

        snapshot.BomItems.Should().HaveCount(1);
        snapshot.Operations.Should().HaveCount(1);
        var ledger = await db.PlmProductSyncRecords.SingleAsync();
        ledger.Status.Should().Be("synced");
        ledger.ManufacturingSnapshotId.Should().Be(snapshot.ManufacturingSnapshotId);
        (await db.PlmSyncAttempts.SingleAsync()).Status.Should().Be("synced");
    }

    [Fact]
    public async Task PersistPayload_DuplicateHashReusesSnapshot()
    {
        var (connection, db, service, record) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;
        var payload = WithHash(ValidPayload());

        var first = await service.PersistPayloadAsync(record.SyncRecordId, payload, CancellationToken.None);
        var second = await service.PersistPayloadAsync(record.SyncRecordId, payload, CancellationToken.None);

        second.ManufacturingSnapshotId.Should().Be(first.ManufacturingSnapshotId);
        (await db.ManufacturingSnapshots.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PersistPayload_ChangedHashCreatesNewSnapshot()
    {
        var (connection, db, service, record) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;
        await service.PersistPayloadAsync(record.SyncRecordId, WithHash(ValidPayload()), CancellationToken.None);
        var changed = ValidPayload() with { BomItems = [new(10, "SCR", "屏幕", 2, 0)] };

        await service.PersistPayloadAsync(record.SyncRecordId, WithHash(changed), CancellationToken.None);

        (await db.ManufacturingSnapshots.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task PersistPayload_HashMismatchCreatesFailureAttemptWithoutSnapshot()
    {
        var (connection, db, service, record) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;
        var payload = ValidPayload() with { PayloadHash = new string('f', 64) };

        var act = () => service.PersistPayloadAsync(record.SyncRecordId, payload, CancellationToken.None);

        await act.Should().ThrowAsync<PlmSyncException>().WithMessage("*哈希*");
        (await db.ManufacturingSnapshots.CountAsync()).Should().Be(0);
        var attempt = await db.PlmSyncAttempts.SingleAsync();
        attempt.ErrorCode.Should().Be("PAYLOAD_HASH_MISMATCH");
        attempt.Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task PersistPayload_InvalidFieldsStoresIssuePathsWithoutSnapshot()
    {
        var (connection, db, service, record) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;
        var invalid = ValidPayload() with { Operations = [new(10, "", "", "", 0, -1, true)] };
        invalid = WithHash(invalid);

        var act = () => service.PersistPayloadAsync(record.SyncRecordId, invalid, CancellationToken.None);

        await act.Should().ThrowAsync<PlmSyncException>().WithMessage("*资料不完整*");
        (await db.ManufacturingSnapshots.CountAsync()).Should().Be(0);
        (await db.PlmSyncAttempts.SingleAsync()).ValidationIssuesJson.Should().Contain("requiredCapability");
    }

    [Fact]
    public async Task PersistPayload_DatabaseFailureRollsBackChildrenAndStoresRetryableAttempt()
    {
        var (connection, db, service, record) = await CreateServiceAsync();
        await using var _ = connection;
        await using var __ = db;
        var duplicateLines = ValidPayload() with
        {
            BomItems = [new(10, "SCR", "屏幕", 1, 0), new(10, "BAT", "电池", 1, 0)],
        };

        var act = () => service.PersistPayloadAsync(record.SyncRecordId, WithHash(duplicateLines), CancellationToken.None);

        await act.Should().ThrowAsync<PlmSyncException>().Where(x => x.Retryable);
        (await db.ManufacturingSnapshots.CountAsync()).Should().Be(0);
        (await db.SnapshotBomItems.CountAsync()).Should().Be(0);
        var attempt = await db.PlmSyncAttempts.SingleAsync();
        attempt.ErrorCode.Should().Be("SNAPSHOT_TRANSACTION_FAILED");
        attempt.Retryable.Should().BeTrue();
    }

    private static async Task<(SqliteConnection, MesDbContext, PlmManufacturingSyncService, MesPlmProductSyncRecord)> CreateServiceAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var record = new MesPlmProductSyncRecord
        {
            PlmProductId = 2,
            PlmManufacturingVersionId = 3,
            PlmVersion = "V1.0",
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.PlmProductSyncRecords.Add(record);
        await db.SaveChangesAsync();
        return (connection, db, new PlmManufacturingSyncService(db, new UnusedClient()), record);
    }

    private static ManufacturingPayload WithHash(ManufacturingPayload payload) =>
        payload with { PayloadHash = ManufacturingPayloadHasher.ComputeHash(payload) };

    private static ManufacturingPayload ValidPayload() => new(
        new(2, "PHONE-001", "手机", "V1.0"), 3, "BOM-V1.0", "ROUTE-V1.0", "",
        [new(10, "SCR", "屏幕", 1, 0)],
        [new(10, "ASSY", "装配", "PHONE_ASSY", 60, 2, true)]);

    private sealed class UnusedClient : IPlmManufacturingClient
    {
        public Task<ManufacturingPayload> GetReleasedAsync(long productId, long manufacturingVersionId, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
