using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services.PlmSync;

public sealed class PlmSyncRetryCoordinator(MesDbContext db, PlmManufacturingSyncService sync, TimeProvider timeProvider, IPlmSyncAlertSink? alerts = null)
{
    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var candidateId = await db.PlmProductSyncRecords.AsNoTracking()
            .Where(x => (x.Status == "pending" || x.Status == "retry_waiting") &&
                (!x.NextRetryAt.HasValue || x.NextRetryAt <= now) && (!x.LeaseUntil.HasValue || x.LeaseUntil <= now))
            .OrderBy(x => x.NextRetryAt).ThenBy(x => x.CreatedAt)
            .Select(x => (long?)x.SyncRecordId).FirstOrDefaultAsync(ct);
        if (!candidateId.HasValue) return false;

        var claimed = await db.PlmProductSyncRecords
            .Where(x => x.SyncRecordId == candidateId.Value && (!x.LeaseUntil.HasValue || x.LeaseUntil <= now))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "syncing")
                .SetProperty(x => x.LeaseUntil, now.AddMinutes(2)).SetProperty(x => x.UpdatedAt, now), ct);
        if (claimed == 0) return false;

        try { await sync.SyncAsync(candidateId.Value, ct); }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            var record = await db.PlmProductSyncRecords.SingleAsync(x => x.SyncRecordId == candidateId.Value, ct);
            if (record.Status == "syncing")
            {
                record.RetryCount++;
                var delay = PlmSyncRetryPolicy.GetDelay(record.RetryCount);
                record.Status = delay.HasValue ? "retry_waiting" : "manual_action";
                record.RequiresManualAction = !delay.HasValue;
                record.NextRetryAt = delay.HasValue ? now.Add(delay.Value) : null;
            }
            else if (record.Status == "manual_action")
            {
                record.RequiresManualAction = true;
                record.NextRetryAt = null;
            }
            record.LeaseUntil = null;
            record.ErrorMessage = ex.Message;
            record.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            var failedAttempts = await db.PlmSyncAttempts.CountAsync(x => x.SyncRecordId == record.SyncRecordId && x.Status == "failed", ct);
            if (record.Status == "manual_action" || failedAttempts == 3)
                alerts?.Notify(record, failedAttempts);
        }
        return true;
    }
}

public sealed class PlmSyncRetryWorker(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<PlmSyncRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Plm:RetryWorkerEnabled", true)) return;
        var seconds = Math.Clamp(config.GetValue("Plm:RetryPollSeconds", 10), 2, 300);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                if (!await scope.ServiceProvider.GetRequiredService<PlmSyncRetryCoordinator>().RunOnceAsync(stoppingToken))
                    await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "PLM 同步重试后台任务执行失败");
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
        }
    }
}
