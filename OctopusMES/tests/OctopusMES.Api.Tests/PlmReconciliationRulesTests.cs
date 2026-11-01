using FluentAssertions;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Tests;

public sealed class PlmReconciliationRulesTests
{
    [Fact]
    public void LegacySyncedRecordWithoutImmutableSnapshot_IsNotReconciledAndNeedsInitialization()
    {
        var record = new MesPlmProductSyncRecord
        {
            PlmManufacturingVersionId = 1,
            Status = "synced",
            PayloadHash = new string('a', 64),
            ManufacturingSnapshotId = null,
        };

        PlmReconciliationRules.IsSame(record, 1, new string('a', 64)).Should().BeFalse();
        PlmReconciliationRules.NeedsInitialization(record).Should().BeTrue();
    }
}
