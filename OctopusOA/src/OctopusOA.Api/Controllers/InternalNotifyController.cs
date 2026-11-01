using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.Services;
using System.Security.Cryptography;
using System.Text;

namespace OctopusOA.Api.Controllers;

[ApiController]
[Route("api/internal")]
public class InternalNotifyController(NotificationService notificationService, IConfiguration config) : ControllerBase
{
    [HttpPost("notify")]
    public async Task<IActionResult> Notify()
    {
        var secret = config["InternalNotify:SharedSecret"] ?? "";
        if (!string.IsNullOrEmpty(secret))
        {
            var sig = Request.Headers["X-Notify-Signature"].ToString();
            Request.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            var expected = "sha256=" + ComputeHmac(body, secret);
            if (sig != expected)
                return Unauthorized(new { code = 401, msg = "签名验证失败" });

            var req = System.Text.Json.JsonSerializer.Deserialize<NotifyRequest>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req == null) return BadRequest();

            if (req.UserId.HasValue)
                await notificationService.NotifyUserAsync(req.UserId.Value, req.Type ?? "info", req.Message ?? "");
            else
                await notificationService.NotifyAllAsync(req.Type ?? "info", req.Message ?? "");
        }
        else
        {
            var req = await System.Text.Json.JsonSerializer.DeserializeAsync<NotifyRequest>(
                Request.Body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req == null) return BadRequest();
            if (req.UserId.HasValue)
                await notificationService.NotifyUserAsync(req.UserId.Value, req.Type ?? "info", req.Message ?? "");
            else
                await notificationService.NotifyAllAsync(req.Type ?? "info", req.Message ?? "");
        }

        return Ok(new { code = 200, msg = "ok" });
    }

    private static string ComputeHmac(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        return Convert.ToHexString(HMACSHA256.HashData(key, data)).ToLowerInvariant();
    }
}

public record NotifyRequest(long? UserId, string? Type, string? Message);
