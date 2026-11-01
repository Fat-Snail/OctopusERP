using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

public static class PlmReconciliationRules
{
    public static bool IsSame(MesPlmProductSyncRecord? record, long manufacturingVersionId, string? payloadHash) =>
        record?.Status == "synced" && record.ManufacturingSnapshotId.HasValue &&
        record.PlmManufacturingVersionId == manufacturingVersionId &&
        string.Equals(record.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase);

    public static bool NeedsInitialization(MesPlmProductSyncRecord record) =>
        !record.ManufacturingSnapshotId.HasValue || record.Status is "failed" or "manual_action";
}
