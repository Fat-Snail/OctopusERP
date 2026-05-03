using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Services;

namespace OctopusOA.Api.Controllers;

/// <summary>公告同步接口（由 UMC 调用，HMAC 签名验证）</summary>
[ApiController]
[AllowAnonymous]
public class NoticeSyncController : ControllerBase
{
    private readonly NoticeService _service;
    private readonly IConfiguration _config;

    public NoticeSyncController(NoticeService service, IConfiguration config)
    {
        _service = service;
        _config = config;
    }

    [HttpPost("/api/notice/sync")]
    public async Task<IActionResult> Sync()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        var secret = _config["UserSync:SharedSecret"];
        if (!string.IsNullOrEmpty(secret))
        {
            var signatureHeader = Request.Headers["X-Sync-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256="))
                return Unauthorized(new { code = 401, msg = "缺少签名" });

            var expected = signatureHeader["sha256=".Length..];
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var actual = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { code = 401, msg = "签名验证失败" });
        }

        var payload = JsonSerializer.Deserialize<NoticeSyncPayload>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload == null) return BadRequest(new { code = 400, msg = "无效的请求体" });

        _service.Sync(payload);
        return Ok(new { code = 200, msg = payload.Action == "delete" ? "删除成功" : "同步成功" });
    }
}
