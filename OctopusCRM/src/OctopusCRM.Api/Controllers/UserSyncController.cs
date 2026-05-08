using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;
using System.Security.Cryptography;
using System.Text;

namespace OctopusCRM.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class UserSyncController(CrmDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpPost("api/users/sync")]
    public async Task<IActionResult> SyncUser([FromBody] UserSyncRequest req)
    {
        if (!Request.Headers.TryGetValue("X-Sync-Signature", out var signatureHeader))
            return Unauthorized(new { code = 401, msg = "缺少签名" });

        var body = System.Text.Json.JsonSerializer.Serialize(req);
        var sharedSecret = configuration["CrmSync:SharedSecret"] ?? "crm-shared-secret-dev";
        var expectedSig = "sha256=" + ComputeHmacSha256(body, sharedSecret);

        if (!string.Equals(signatureHeader.ToString(), expectedSig, StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { code = 401, msg = "签名验证失败" });

        if (req.Action == "delete")
        {
            var toDelete = await db.SyncUsers.FirstOrDefaultAsync(x => x.UmcUserId == req.UmcUserId);
            if (toDelete != null)
            {
                db.SyncUsers.Remove(toDelete);
                await db.SaveChangesAsync();
            }
            return Ok(new { code = 200, msg = "ok", data = (object?)null });
        }

        var existing = await db.SyncUsers.FirstOrDefaultAsync(x => x.UmcUserId == req.UmcUserId);
        var now = DateTime.UtcNow;

        if (existing == null)
        {
            db.SyncUsers.Add(new CrmSyncUser
            {
                UmcUserId = req.UmcUserId,
                UserName = req.UserName,
                NickName = req.NickName,
                Email = req.Email,
                PhoneNumber = req.PhoneNumber,
                Avatar = req.Avatar,
                Status = req.Status,
                LastSyncAt = now
            });
        }
        else
        {
            existing.UserName = req.UserName;
            existing.NickName = req.NickName;
            existing.Email = req.Email;
            existing.PhoneNumber = req.PhoneNumber;
            existing.Avatar = req.Avatar;
            existing.Status = req.Status;
            existing.LastSyncAt = now;
        }

        await db.SaveChangesAsync();
        return Ok(new { code = 200, msg = "ok", data = (object?)null });
    }

    [HttpPost("api/auth/backchannel-logout")]
    public IActionResult BackChannelLogout()
    {
        // CRM 无会话状态，直接返回 200
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

public record UserSyncRequest(
    long UmcUserId,
    string UserName,
    string? NickName,
    string? Email,
    string? PhoneNumber,
    string? Avatar,
    int Status,
    string Action);
