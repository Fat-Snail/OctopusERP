using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Api.DTOs;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Controllers;

/// <summary>用户同步接口（由 UMC 调用）</summary>
[ApiController]
public class UserSyncController : ControllerBase
{
    private readonly PlmDbContext _db;
    private readonly IConfiguration _config;

    public UserSyncController(PlmDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>接收 UMC 推送的用户同步数据</summary>
    [HttpPost("/api/users/sync")]
    [AllowAnonymous]
    public async Task<IActionResult> SyncUser()
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

        var payload = JsonSerializer.Deserialize<UserSyncPayload>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload == null)
            return BadRequest(new { code = 400, msg = "无效的请求体" });

        var existing = _db.SyncUsers.FirstOrDefault(u => u.UmcUserId == payload.UserId);
        if (existing != null)
        {
            existing.UserName = payload.UserName;
            existing.NickName = payload.NickName;
            existing.Email = payload.Email;
            existing.PhoneNumber = payload.PhoneNumber;
            existing.Status = payload.Status;
            existing.LastSyncAt = DateTime.UtcNow;
            _db.SaveChanges();
            return Ok(new { code = 200, msg = "同步成功（更新）" });
        }

        _db.SyncUsers.Add(new Core.Entities.SyncUser
        {
            UmcUserId = payload.UserId,
            UserName = payload.UserName,
            NickName = payload.NickName,
            Email = payload.Email,
            PhoneNumber = payload.PhoneNumber,
            Status = payload.Status,
            PlmRoles = "plm_user",
            LastSyncAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
        return Ok(new { code = 200, msg = "同步成功（新增）" });
    }

    /// <summary>获取已同步用户列表</summary>
    [HttpGet("/api/users/sync")]
    [AllowAnonymous]
    public IActionResult GetSyncUsers()
    {
        return Ok(new { code = 200, msg = "ok", data = _db.SyncUsers.ToList() });
    }
}
