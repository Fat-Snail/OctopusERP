using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusRetail.Api.Dtos;
using OctopusRetail.Api.Multitenancy;
using OctopusRetail.Api.Persistence;
using OctopusCommon.Security;

namespace OctopusRetail.Api.Controllers;

/// <summary>
/// 内部成员资料同步（供 UMC 调用，不对浏览器开放）。
///
/// ADR-010 之后用户主数据在 UMC，零售库的 `UserName` / `DisplayName` / `Phone`
/// 只是成员列表用的**快照**。管理员在 UMC 改了手机号或姓名，必须回推到这些快照，
/// 否则成员列表会长期显示旧值（实测：feiwoniu001 的手机号一度两边对不上）。
///
/// 鉴权：共享密钥头 <c>X-Internal-Secret</c>（配置 InternalApi:SharedSecret）。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/retail/internal/members")]
[AllowCrossTenant]   // UMC 不知道租户头，按 kcSub 跨组织更新
public class InternalMemberController(
    RetailDbContext db,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<InternalMemberController> logger) : ControllerBase
{
    /// <summary>非 Development/Test 且未配置时返回 null → 一律拒绝（fail-closed）</summary>
    private string? SharedSecret => InternalApiSecret.Resolve(config, env);

    private bool Authorized() =>
        InternalApiSecret.Matches(Request.Headers["X-Internal-Secret"].FirstOrDefault(), SharedSecret);

    /// <summary>
    /// 按 Keycloak sub 更新该用户在所有组织里的资料快照。
    /// 只覆盖**非空**字段，未传的字段保持原值。
    /// </summary>
    [HttpPost("sync-profile")]
    public async Task<ApiResponse<object?>> SyncProfile(
        [FromBody] SyncMemberProfileRequest req, CancellationToken ct)
    {
        if (SharedSecret is null)
            return ApiResponse<object?>.Fail("服务端未配置内部接口密钥（InternalApi:SharedSecret），已拒绝请求", 403);
        if (!Authorized()) return ApiResponse<object?>.Fail("无权访问", 403);
        if (string.IsNullOrWhiteSpace(req.KcSub))
            return ApiResponse<object?>.Fail("kcSub 不能为空", 400);

        var rows = await db.Memberships
            .IgnoreQueryFilters()          // 跨组织更新：这个人在几个组织里就有几条
            .Where(m => m.KcSub == req.KcSub)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return ApiResponse<object?>.Success(new { updated = 0 });

        foreach (var m in rows)
        {
            if (!string.IsNullOrWhiteSpace(req.UserName)) m.UserName = req.UserName.Trim();
            if (!string.IsNullOrWhiteSpace(req.DisplayName)) m.DisplayName = req.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(req.Phone)) m.Phone = req.Phone.Trim();
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("成员资料已由 UMC 同步 KcSub={KcSub} 影响 {Count} 条", req.KcSub, rows.Count);

        return ApiResponse<object?>.Success(new { updated = rows.Count });
    }
}
