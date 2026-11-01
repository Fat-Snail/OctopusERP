using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Tests;

public sealed class PlmSyncMetricsTests
{
    [Fact]
    public async Task Metrics_AggregateStatesAttemptsDurationAndLastSuccess()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var records = new[]
        {
            Record(1, "pending"), Record(2, "retry_waiting"), Record(3, "manual_action"), Record(4, "synced", now),
        };
        db.PlmProductSyncRecords.AddRange(records);
        await db.SaveChangesAsync();
        db.PlmSyncAttempts.AddRange(
            Attempt(records[1], 1, "failed", now.AddSeconds(-30), now.AddSeconds(-20)),
            Attempt(records[2], 1, "failed", now.AddSeconds(-20), now.AddSeconds(-10)),
            Attempt(records[3], 1, "synced", now.AddSeconds(-10), now));
        await db.SaveChangesAsync();

        var metrics = await new MesStatsService(db).GetPlmSyncMetricsAsync();

        metrics.Pending.Should().Be(1);
        metrics.RetryWaiting.Should().Be(1);
        metrics.ManualAction.Should().Be(1);
        metrics.Synced.Should().Be(1);
        metrics.FailureRate.Should().BeApproximately(2d / 3d, 0.0001);
        metrics.AverageDurationSeconds.Should().Be(10);
        metrics.LastSuccessAt.Should().Be(now);
        metrics.MaxConsecutiveFailures.Should().Be(1);
    }

    private static MesPlmProductSyncRecord Record(long id, string status, DateTime? syncedAt = null) => new()
    {
        PlmProductId = id, PlmVersion = $"V{id}", Status = status, SyncedAt = syncedAt,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };
    private static MesPlmSyncAttempt Attempt(MesPlmProductSyncRecord record, int number, string status, DateTime start, DateTime end) => new()
    {
        SyncRecordId = record.SyncRecordId, SyncRecord = record, AttemptNumber = number, Status = status,
        StartedAt = start, FinishedAt = end,
    };
}
