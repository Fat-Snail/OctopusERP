using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services.PlmSync;

namespace OctopusMES.Api.Tests;

public sealed class PlmSyncRetryWorkerTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 30)]
    [InlineData(5, 60)]
    public void RetryPolicy_UsesApprovedBackoff(int failedAttempt, int expectedMinutes)
    {
        PlmSyncRetryPolicy.GetDelay(failedAttempt).Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void RetryPolicy_StopsAfterFiveFailures()
    {
        PlmSyncRetryPolicy.GetDelay(6).Should().BeNull();
    }

    [Fact]
    public async Task Coordinator_ProcessesDueRecordAndSkipsActiveLease()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var due = new MesPlmProductSyncRecord { PlmProductId = 2, PlmManufacturingVersionId = 3, PlmVersion = "V1", Status = "retry_waiting", NextRetryAt = now.UtcDateTime.AddMinutes(-1), CreatedAt = now.UtcDateTime, UpdatedAt = now.UtcDateTime };
        var leased = new MesPlmProductSyncRecord { PlmProductId = 4, PlmManufacturingVersionId = 5, PlmVersion = "V2", Status = "retry_waiting", NextRetryAt = now.UtcDateTime.AddMinutes(-1), LeaseUntil = now.UtcDateTime.AddMinutes(5), CreatedAt = now.UtcDateTime, UpdatedAt = now.UtcDateTime };
        db.PlmProductSyncRecords.AddRange(due, leased);
        await db.SaveChangesAsync();
        var payload = new ManufacturingPayload(new(2, "P", "手机", "V1"), 3, "B1", "R1", "", [new(10, "M", "物料", 1, 0)], [new(10, "O", "装配", "CAP", 1, 0, true)]);
        payload = payload with { PayloadHash = ManufacturingPayloadHasher.ComputeHash(payload) };
        var sync = new PlmManufacturingSyncService(db, new FakeClient(payload));
        var coordinator = new PlmSyncRetryCoordinator(db, sync, new FixedTimeProvider(now));

        (await coordinator.RunOnceAsync(CancellationToken.None)).Should().BeTrue();
        (await db.PlmProductSyncRecords.FindAsync(due.SyncRecordId))!.Status.Should().Be("synced");
        (await db.PlmProductSyncRecords.FindAsync(leased.SyncRecordId))!.Status.Should().Be("retry_waiting");
    }

    [Fact]
    public async Task Coordinator_PreservesNonRetryableFailureAndRaisesOneManualAlert()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var record = new MesPlmProductSyncRecord
        {
            PlmProductId = 2, PlmManufacturingVersionId = 3, PlmVersion = "V1", Status = "pending",
            CreatedAt = now.UtcDateTime, UpdatedAt = now.UtcDateTime
        };
        db.PlmProductSyncRecords.Add(record);
        await db.SaveChangesAsync();
        var invalid = new ManufacturingPayload(new(2, "P", "手机", "V1"), 3, "B1", "R1", "invalid", [], []);
        var alerts = new CapturingAlertSink();
        var coordinator = new PlmSyncRetryCoordinator(db, new PlmManufacturingSyncService(db, new FakeClient(invalid)), new FixedTimeProvider(now), alerts);

        (await coordinator.RunOnceAsync(CancellationToken.None)).Should().BeTrue();

        db.ChangeTracker.Clear();
        var saved = await db.PlmProductSyncRecords.SingleAsync();
        saved.Status.Should().Be("manual_action");
        saved.RequiresManualAction.Should().BeTrue();
        alerts.Notifications.Should().Be(1);
    }

    private sealed class FakeClient(ManufacturingPayload payload) : IPlmManufacturingClient
    {
        public Task<ManufacturingPayload> GetReleasedAsync(long productId, long manufacturingVersionId, CancellationToken ct) => Task.FromResult(payload);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CapturingAlertSink : IPlmSyncAlertSink
    {
        public int Notifications { get; private set; }
        public void Notify(MesPlmProductSyncRecord record, int failedAttempts) => Notifications++;
    }
}
