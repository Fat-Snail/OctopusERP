using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OctopusMES.Api.Services.PlmSync;

public sealed record PlmReleaseEvent(string EventId, string EventType, long ProductId, long ManufacturingVersionId, DateTime ReleasedAt, DateTime? SentAt, string? PayloadHash);
public sealed record PlmEventAuthenticationResult(bool Success, string? ErrorCode, string? Message, bool Unauthorized = false);

public static partial class PlmEventAuthenticator
{
    public static PlmEventAuthenticationResult Authenticate(PlmReleaseEvent evt, string configuredSecret, string suppliedSecret, string suppliedSignature, DateTime utcNow)
    {
        if (string.IsNullOrEmpty(configuredSecret) || suppliedSecret != configuredSecret)
            return new(false, "INVALID_SECRET", "PLM 共享密钥无效", true);
        if (!evt.SentAt.HasValue || string.IsNullOrWhiteSpace(evt.PayloadHash))
            return new(false, "MISSING_METADATA", "发布事件缺少 sentAt 或 payloadHash");
        if (!Sha256Regex().IsMatch(evt.PayloadHash))
            return new(false, "INVALID_PAYLOAD_HASH", "payloadHash 必须是 64 位十六进制 SHA-256");
        if (Math.Abs((utcNow - evt.SentAt.Value.ToUniversalTime()).TotalMinutes) > 5)
            return new(false, "STALE_TIMESTAMP", "发布事件时间戳已过期");

        var expected = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(configuredSecret), Encoding.UTF8.GetBytes(CreateCanonical(evt)))).ToLowerInvariant();
        if (suppliedSignature.Length != expected.Length ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(suppliedSignature), Encoding.ASCII.GetBytes(expected)))
            return new(false, "INVALID_SIGNATURE", "PLM 发布事件签名无效", true);
        return new(true, null, null);
    }

    public static string CreateCanonical(PlmReleaseEvent evt) =>
        $"{evt.EventId}\n{evt.ProductId}\n{evt.ManufacturingVersionId}\n{evt.SentAt!.Value.ToUniversalTime():O}\n{evt.PayloadHash}";

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();
}
