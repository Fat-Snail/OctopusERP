using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services.PlmSync;

public sealed class PlmSyncException(string code, string message, bool retryable) : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}

public sealed class PlmManufacturingSyncService(MesDbContext db, IPlmManufacturingClient client)
{
    public async Task<MesManufacturingSnapshot> SyncAsync(long syncRecordId, CancellationToken ct)
    {
        var record = await db.PlmProductSyncRecords.AsNoTracking().SingleOrDefaultAsync(x => x.SyncRecordId == syncRecordId, ct)
            ?? throw new PlmSyncException("SYNC_RECORD_NOT_FOUND", "同步记录不存在", false);
        if (!record.PlmManufacturingVersionId.HasValue)
            throw new PlmSyncException("MANUFACTURING_VERSION_MISSING", "同步记录缺少 PLM 制造版本 ID", false);
        var payload = await client.GetReleasedAsync(record.PlmProductId, record.PlmManufacturingVersionId.Value, ct);
        return await PersistPayloadAsync(syncRecordId, payload, ct);
    }

    public async Task<MesManufacturingSnapshot> PersistPayloadAsync(long syncRecordId, ManufacturingPayload payload, CancellationToken ct)
    {
        var record = await db.PlmProductSyncRecords.SingleOrDefaultAsync(x => x.SyncRecordId == syncRecordId, ct)
            ?? throw new PlmSyncException("SYNC_RECORD_NOT_FOUND", "同步记录不存在", false);
        var attemptNumber = await db.PlmSyncAttempts.Where(x => x.SyncRecordId == syncRecordId).Select(x => (int?)x.AttemptNumber).MaxAsync(ct) ?? 0;
        var startedAt = DateTime.UtcNow;

        if (!ManufacturingPayloadHasher.MatchesDeclaredHash(payload))
            return await FailAsync<MesManufacturingSnapshot>(record, attemptNumber + 1, startedAt, "PAYLOAD_HASH_MISMATCH", "PLM 载荷哈希校验失败", false, null, ct);

        var issues = ManufacturingPayloadValidator.Validate(payload);
        if (issues.Count > 0)
            return await FailAsync<MesManufacturingSnapshot>(record, attemptNumber + 1, startedAt, "PAYLOAD_INVALID", "PLM 制造资料不完整", false, issues, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var snapshot = await db.ManufacturingSnapshots
                .Include(x => x.BomItems).Include(x => x.Operations)
                .SingleOrDefaultAsync(x => x.PlmProductId == payload.Product.ProductId &&
                    x.PlmManufacturingVersionId == payload.ManufacturingVersionId && x.PayloadHash == payload.PayloadHash, ct);
            if (snapshot is null)
            {
                snapshot = new MesManufacturingSnapshot
                {
                    PlmProductId = payload.Product.ProductId,
                    PlmManufacturingVersionId = payload.ManufacturingVersionId,
                    ProductCode = payload.Product.ProductCode,
                    ProductName = payload.Product.ProductName,
                    ProductVersion = payload.Product.Version,
                    BomVersion = payload.BomVersion,
                    RoutingVersion = payload.RoutingVersion,
                    PayloadHash = payload.PayloadHash,
                    Status = "active",
                    SyncedAt = DateTime.UtcNow,
                    BomItems = payload.BomItems.Select(x => new MesSnapshotBomItem
                    {
                        LineNo = x.LineNo, MaterialCode = x.MaterialCode, MaterialName = x.MaterialName,
                        Quantity = x.Quantity, LossRate = x.LossRate,
                    }).ToList(),
                    Operations = payload.Operations.Select(x => new MesSnapshotOperation
                    {
                        Sequence = x.Sequence, OperationCode = x.OperationCode, OperationName = x.OperationName,
                        RequiredCapability = x.RequiredCapability, StandardCycleSec = x.StandardCycleSec,
                        SetupMinutes = x.SetupMinutes, IsSplittable = x.IsSplittable,
                    }).ToList(),
                };
                db.ManufacturingSnapshots.Add(snapshot);
                await db.SaveChangesAsync(ct);
            }

            await EnsureLegacyProjectionAsync(record, payload, ct);
            record.ManufacturingSnapshotId = snapshot.ManufacturingSnapshotId;
            record.PayloadHash = snapshot.PayloadHash;
            record.PlmManufacturingVersionId = snapshot.PlmManufacturingVersionId;
            record.PlmVersion = snapshot.ProductVersion;
            record.Status = "synced";
            record.ErrorMessage = null;
            record.RequiresManualAction = false;
            record.NextRetryAt = null;
            record.LeaseUntil = null;
            record.LastAttemptAt = startedAt;
            record.SyncedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
            db.PlmSyncAttempts.Add(new MesPlmSyncAttempt
            {
                SyncRecordId = syncRecordId,
                AttemptNumber = attemptNumber + 1,
                Status = "synced",
                Retryable = false,
                StartedAt = startedAt,
                FinishedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return snapshot;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var failedRecord = await db.PlmProductSyncRecords.SingleAsync(x => x.SyncRecordId == syncRecordId, ct);
            failedRecord.Status = "retry_waiting";
            failedRecord.ErrorMessage = "制造快照事务保存失败";
            failedRecord.RetryCount++;
            failedRecord.LastAttemptAt = startedAt;
            failedRecord.NextRetryAt = DateTime.UtcNow.AddMinutes(1);
            failedRecord.UpdatedAt = DateTime.UtcNow;
            db.PlmSyncAttempts.Add(new MesPlmSyncAttempt
            {
                SyncRecordId = syncRecordId,
                AttemptNumber = attemptNumber + 1,
                Status = "failed",
                ErrorCode = "SNAPSHOT_TRANSACTION_FAILED",
                ErrorMessage = ex.Message,
                Retryable = true,
                StartedAt = startedAt,
                FinishedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            throw new PlmSyncException("SNAPSHOT_TRANSACTION_FAILED", "制造快照事务保存失败", true);
        }
    }

    private async Task EnsureLegacyProjectionAsync(MesPlmProductSyncRecord record, ManufacturingPayload payload, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.ProductCode == payload.Product.ProductCode && x.Version == payload.Product.Version, ct);
        if (product is null)
        {
            product = new MesProduct { ProductCode = payload.Product.ProductCode, ProductName = payload.Product.ProductName, Version = payload.Product.Version, Status = "active", CreatedAt = DateTime.UtcNow };
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
        }
        var bom = await db.Boms.SingleOrDefaultAsync(x => x.ProductId == product.ProductId && x.Version == payload.BomVersion, ct);
        if (bom is null)
        {
            bom = new MesBom { ProductId = product.ProductId, BomCode = $"PLM-BOM-{payload.Product.ProductId}-{payload.ManufacturingVersionId}", Version = payload.BomVersion, Status = "active", CreatedAt = DateTime.UtcNow, Items = payload.BomItems.Select(x => new MesBomItem { LineNo = x.LineNo, MaterialCode = x.MaterialCode, MaterialName = x.MaterialName, Quantity = x.Quantity, LossRate = x.LossRate }).ToList() };
            db.Boms.Add(bom);
        }
        var routing = await db.Routings.SingleOrDefaultAsync(x => x.ProductId == product.ProductId && x.Version == payload.RoutingVersion, ct);
        if (routing is null)
        {
            routing = new MesRouting { ProductId = product.ProductId, RoutingCode = $"PLM-ROUTE-{payload.Product.ProductId}-{payload.ManufacturingVersionId}", Version = payload.RoutingVersion, Status = "active", CreatedAt = DateTime.UtcNow, Operations = payload.Operations.Select(x => new MesRoutingOperation { Sequence = x.Sequence, OperationCode = x.OperationCode, OperationName = x.OperationName, RequiredCapability = x.RequiredCapability, StandardCycleSec = x.StandardCycleSec, SetupMinutes = x.SetupMinutes, IsSplittable = x.IsSplittable }).ToList() };
            db.Routings.Add(routing);
        }
        await db.SaveChangesAsync(ct);
        record.MesProductId = product.ProductId;
        record.MesBomId = bom.BomId;
        record.MesRoutingId = routing.RoutingId;
    }

    private async Task<T> FailAsync<T>(MesPlmProductSyncRecord record, int attemptNumber, DateTime startedAt, string code, string message, bool retryable, IReadOnlyList<ManufacturingPayloadIssue>? issues, CancellationToken ct)
    {
        record.Status = retryable ? "retry_waiting" : "manual_action";
        record.ErrorMessage = message;
        record.RequiresManualAction = !retryable;
        record.LastAttemptAt = startedAt;
        record.UpdatedAt = DateTime.UtcNow;
        db.PlmSyncAttempts.Add(new MesPlmSyncAttempt
        {
            SyncRecordId = record.SyncRecordId,
            AttemptNumber = attemptNumber,
            Status = "failed",
            ErrorCode = code,
            ErrorMessage = message,
            ValidationIssuesJson = issues is null ? null : JsonSerializer.Serialize(issues),
            Retryable = retryable,
            StartedAt = startedAt,
            FinishedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        throw new PlmSyncException(code, message, retryable);
    }
}
