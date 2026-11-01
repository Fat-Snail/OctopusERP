using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusMES.Api.Services.PlmSync;

public static class ManufacturingPayloadHasher
{
    public static string ComputeHash(ManufacturingPayload payload)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            payload.Product.ProductId,
            Version = payload.Product.Version,
            payload.BomVersion,
            payload.RoutingVersion,
            bom = payload.BomItems.OrderBy(x => x.LineNo).Select(x => new { x.LineNo, x.MaterialCode, x.Quantity, x.LossRate }),
            operations = payload.Operations.OrderBy(x => x.Sequence).Select(x => new { x.Sequence, x.OperationCode, x.RequiredCapability, x.StandardCycleSec, x.SetupMinutes, x.IsSplittable }),
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static bool MatchesDeclaredHash(ManufacturingPayload payload) =>
        string.Equals(ComputeHash(payload), payload.PayloadHash, StringComparison.OrdinalIgnoreCase);
}
