using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;
using System.Security.Cryptography;
using System.Text;

namespace OctopusCRM.Api.Controllers;

[ApiController]
[Route("api/webhook")]
[AllowAnonymous]
public class WebhookController(CrmDbContext db, IConfiguration config) : ControllerBase
{
    private const string CrmSignatureHeader = "X-Crm-Signature";

    [HttpPost("ship-confirm")]
    public async Task<IActionResult> ShipConfirm([FromBody] ShipConfirmRequest req)
    {
        if (!VerifySignature(config["CrmSync:SharedSecret"] ?? "crm-shared-secret-dev", out var err))
            return Unauthorized(new { code = 401, msg = err });

        var contract = await db.Contracts.FindAsync(req.CrmContractId);
        if (contract is null)
            return Ok(new { code = 200, msg = "ok", data = (object?)null });

        if (contract.Status == "active")
        {
            contract.Status = "shipped";
            contract.ActualDeliveryDate = req.ShippedAt;
            contract.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Ok(new { code = 200, msg = "ok", data = (object?)null });
    }

    private bool VerifySignature(string secret, out string error)
    {
        error = string.Empty;
        if (!Request.Headers.TryGetValue(CrmSignatureHeader, out var sig))
        {
            error = "缺少签名";
            return false;
        }

        Request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = reader.ReadToEndAsync().GetAwaiter().GetResult();

        var expected = "sha256=" + ComputeHmac(body, secret);
        if (!string.Equals(sig.ToString(), expected, StringComparison.OrdinalIgnoreCase))
        {
            error = "签名验证失败";
            return false;
        }
        return true;
    }

    private static string ComputeHmac(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLower();
    }
}

public record ShipConfirmRequest(long CrmContractId, DateTime? ShippedAt);
