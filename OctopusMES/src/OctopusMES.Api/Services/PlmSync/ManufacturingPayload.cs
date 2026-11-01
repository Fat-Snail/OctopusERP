namespace OctopusMES.Api.Services.PlmSync;

public sealed record ManufacturingPayload(
    ManufacturingProduct Product,
    long ManufacturingVersionId,
    string BomVersion,
    string RoutingVersion,
    string PayloadHash,
    IReadOnlyList<ManufacturingBomItem> BomItems,
    IReadOnlyList<ManufacturingOperation> Operations);

public sealed record ManufacturingProduct(long ProductId, string ProductCode, string ProductName, string Version);
public sealed record ManufacturingBomItem(int LineNo, string MaterialCode, string MaterialName, decimal Quantity, decimal LossRate);
public sealed record ManufacturingOperation(int Sequence, string OperationCode, string OperationName, string RequiredCapability, int StandardCycleSec, int SetupMinutes, bool IsSplittable);
public sealed record ManufacturingPayloadIssue(string Path, string Message);
