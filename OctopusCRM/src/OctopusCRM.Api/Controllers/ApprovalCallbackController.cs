using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusCRM.Api.Services;
using System.Security.Cryptography;
using System.Text;

namespace OctopusCRM.Api.Controllers;

[ApiController]
[Route("api/approval-callback")]
[AllowAnonymous]
public class ApprovalCallbackController(
    QuoteService quoteService,
    ContractService contractService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Callback([FromBody] ApprovalCallbackRequest req)
    {
        // HMAC 签名验证（基于原始请求体，避免重新序列化导致的大小写差异）
        if (!Request.Headers.TryGetValue("X-Crm-Signature", out var signatureHeader))
            return Unauthorized(new { code = 401, msg = "缺少签名" });

        Request.Body.Seek(0, System.IO.SeekOrigin.Begin);
        using var reader = new System.IO.StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();

        var sharedSecret = configuration["CrmSync:SharedSecret"] ?? "crm-shared-secret-dev";
        var expectedSig = "sha256=" + ComputeHmacSha256(rawBody, sharedSecret);

        if (!string.Equals(signatureHeader.ToString(), expectedSig, StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { code = 401, msg = "签名验证失败" });

        await quoteService.ApprovalCallbackAsync(req.OaApprovalId, req.Approved);
        await contractService.ApprovalCallbackAsync(req.OaApprovalId, req.Approved);

        return Ok(new { code = 200, msg = "ok", data = (object?)null });
    }

    private static string ComputeHmacSha256(string message, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hash).ToLower();
    }
}

public record ApprovalCallbackRequest(long OaApprovalId, bool Approved, string? Comment);
