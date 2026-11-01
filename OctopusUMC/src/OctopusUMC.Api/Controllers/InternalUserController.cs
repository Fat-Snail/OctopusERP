using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Api.Services;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using OctopusCommon.Security;

namespace OctopusUMC.Api.Controllers;

/// <summary>
/// 内部用户登记接口（供其它子系统调用，不对浏览器开放）。
///
/// ADR-010 的核心：**UMC 是唯一用户主数据源**。
/// 零售域邀请注册不再直接调 Keycloak 建号，而是调这里建号，
/// 再由 UMC 同步到 Keycloak —— 这样在 5173 的用户管理里能看到、能停用、能改密。
///
/// 鉴权：共享密钥头 <c>X-Internal-Secret</c>（配置 InternalApi:SharedSecret）。
/// 这些用户一律标记 <c>user_type=customer</c> + <c>source=retail</c>，
/// 因此不会被 UserSyncService 下发到 OA 通讯录等下游。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/users")]
public class InternalUserController(
    ApplicationDbContext db,
    KeycloakSyncService keycloak,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<InternalUserController> logger) : ControllerBase
{
    /// <summary>
    /// 解析内部密钥。**fail-closed**：非 Development/Test 且未配置时返回 null，
    /// 此时一律拒绝请求，绝不回退到仓库里公开的开发密钥。
    /// </summary>
    private string? SharedSecret => InternalApiSecret.Resolve(config, env);

    private bool Authorized() =>
        InternalApiSecret.Matches(Request.Headers["X-Internal-Secret"].FirstOrDefault(), SharedSecret);

    private const string NotConfigured = "服务端未配置内部接口密钥（InternalApi:SharedSecret），已拒绝请求";

    /// <summary>零售域邀请注册：在 UMC 建号并同步到 Keycloak</summary>
    [HttpPost("retail")]
    public async Task<ApiResponse<object?>> CreateForRetail(
        [FromBody] InternalCreateUserRequest req, CancellationToken ct)
    {
        if (SharedSecret is null) return ApiResponse<object?>.Fail(NotConfigured, 403);
        if (!Authorized()) return ApiResponse<object?>.Fail("无权访问", 403);

        var userName = (req.UserName ?? string.Empty).Trim();
        if (userName.Length < 3)
            return ApiResponse<object?>.Fail("用户名至少 3 位", 400);
        if (string.IsNullOrEmpty(req.Password) || req.Password.Length < 8)
            return ApiResponse<object?>.Fail("密码至少 8 位", 400);

        if (await db.Users.AnyAsync(u => u.UserName == userName, ct))
            return ApiResponse<object?>.Fail("用户名已被占用", 409);

        var user = new User
        {
            UserName = userName,
            NickName = string.IsNullOrWhiteSpace(req.NickName) ? userName : req.NickName.Trim(),
            Email = req.Email ?? string.Empty,
            PhoneNumber = req.PhoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Status = 1,
            UserType = "customer",     // 不进员工目录
            Source = req.Source ?? "retail",
            CreateTime = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        // 等待 Keycloak 建号（零售方马上要拿 kcSub 写成员关系，不能 fire-and-forget）
        await keycloak.SyncUserAsync(user, ct);
        var kcSub = await db.Users.Where(u => u.UserId == user.UserId)
            .Select(u => u.KcSub).FirstOrDefaultAsync(ct);

        if (keycloak.Enabled && string.IsNullOrEmpty(kcSub))
        {
            // Keycloak 建号失败：回滚 UMC 用户，避免"UMC 有、Keycloak 没有"的半成品
            db.Users.Remove(user);
            await db.SaveChangesAsync(ct);
            logger.LogWarning("零售建号失败（Keycloak 未成功），已回滚 UserName={UserName}", userName);
            return ApiResponse<object?>.Fail("建号失败：Keycloak 不可用，请稍后重试", 502);
        }

        // 同步登录密码到 Keycloak（SyncUserAsync 只同步基础信息，不含密码）
        if (keycloak.Enabled)
            await keycloak.ResetPasswordAsync(user.UserId, user.UserName, req.Password, ct);

        logger.LogInformation("零售用户已登记 UserId={UserId} UserName={UserName} KcSub={KcSub}",
            user.UserId, user.UserName, kcSub);

        return ApiResponse<object?>.Success(new { userId = user.UserId, kcSub, userName = user.UserName });
    }

    /// <summary>
    /// 迁移用：把已在 Keycloak 存在的账号登记进 UMC（不设密码）。
    ///
    /// 用于 ADR-010 改造前的存量零售用户——他们已有 Keycloak 账号，
    /// 这里只补 UMC 侧的用户记录，密码留空（随机哈希），需管理员重置后才能登 UMC。
    /// </summary>
    [HttpPost("adopt")]
    public async Task<ApiResponse<object?>> AdoptExisting(
        [FromBody] InternalAdoptUserRequest req, CancellationToken ct)
    {
        if (SharedSecret is null) return ApiResponse<object?>.Fail(NotConfigured, 403);
        if (!Authorized()) return ApiResponse<object?>.Fail("无权访问", 403);

        var userName = (req.UserName ?? string.Empty).Trim();
        if (userName.Length == 0)
            return ApiResponse<object?>.Fail("用户名不能为空", 400);
        if (string.IsNullOrEmpty(req.KcSub))
            return ApiResponse<object?>.Fail("kcSub 不能为空", 400);

        var existing = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName, ct);
        if (existing is not null)
        {
            // 幂等：已存在就只补齐 kcSub / 来源
            existing.KcSub ??= req.KcSub;
            existing.Source = string.IsNullOrEmpty(existing.Source) ? (req.Source ?? "retail") : existing.Source;
            // 存量接管时经常缺手机号（旧流程把手机号当成一次性校验值后丢弃），
            // 允许后续补一次：只在本地为空时写入，不覆盖已有值。
            if (string.IsNullOrWhiteSpace(existing.PhoneNumber) && !string.IsNullOrWhiteSpace(req.PhoneNumber))
                existing.PhoneNumber = req.PhoneNumber;
            await db.SaveChangesAsync(ct);
            return ApiResponse<object?>.Success(new { userId = existing.UserId, kcSub = existing.KcSub, adopted = false });
        }

        var user = new User
        {
            UserName = userName,
            NickName = string.IsNullOrWhiteSpace(req.NickName) ? userName : req.NickName.Trim(),
            Email = req.Email ?? string.Empty,
            PhoneNumber = req.PhoneNumber,
            // 随机密码：无法猜解，管理员重置后才能用 UMC 登录
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            Status = req.Status,
            UserType = req.UserType ?? "customer",
            Source = req.Source ?? "retail",
            KcSub = req.KcSub,
            CreateTime = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("已接管存量用户 UserId={UserId} UserName={UserName}", user.UserId, user.UserName);
        return ApiResponse<object?>.Success(new { userId = user.UserId, kcSub = user.KcSub, adopted = true });
    }
}
