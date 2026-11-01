using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Api.Services;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;

namespace OctopusUMC.Api.Controllers;

/// <summary>账户认证接口</summary>
[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _email;
    private readonly IMemoryCache _cache;

    public AccountController(ApplicationDbContext context, IEmailService email, IMemoryCache cache)
    {
        _context = context;
        _email = email;
        _cache = cache;
    }

    /// <summary>登录（颁发 Cookie）</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ApiResponse<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        // 纵深防御：显式拒绝空/空白密码。
        // 历史漏洞：重置密码接口未校验长度，把 BCrypt("") 写进了库，
        // 结果任何人用空密码就能登录并拿到会话 Cookie（实测复现）。
        if (string.IsNullOrWhiteSpace(request.Password))
            return ApiResponse<LoginResponse>.Fail("用户名或密码错误", 401);

        var user = _context.Users.FirstOrDefault(u => u.UserName == request.UserName);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return ApiResponse<LoginResponse>.Fail("用户名或密码错误", 401);

        if (user.Status != 1)
            return ApiResponse<LoginResponse>.Fail("账号已被禁用", 403);

        // 通过 UserRole 关联表查询角色
        var roleIds = _context.UserRoles.Where(ur => ur.UserId == user.UserId).Select(ur => ur.RoleId).ToList();
        var userRoles = _context.Roles.Where(r => roleIds.Contains(r.RoleId)).ToList();

        // 计算权限标识（通过 RoleMenu 关联表）
        var permissions = new List<string>();
        if (userRoles.Any(r => r.RoleKey == "admin"))
        {
            permissions.Add("*:*:*");
        }
        else
        {
            var roleIdList = userRoles.Select(r => r.RoleId).ToList();
            var menuIds = _context.RoleMenus
                .Where(rm => roleIdList.Contains(rm.RoleId))
                .Select(rm => rm.MenuId)
                .Distinct()
                .ToList();
            permissions = _context.Menus
                .Where(m => menuIds.Contains(m.MenuId) && m.MenuType == "F" && m.Permission != null && m.Permission != "")
                .Select(m => m.Permission!)
                .Distinct()
                .ToList();
        }

        // 将权限写入 Cookie Claims，供 HasPermissionAttribute 直接读取（零 DB 开销）
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
        };
        foreach (var role in userRoles) claims.Add(new(ClaimTypes.Role, role.RoleKey));
        foreach (var perm in permissions) claims.Add(new("permission", perm));

        var identity = new ClaimsIdentity(claims, "Cookies");
        await HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(identity));

        // 记录访问日志
        _context.LoginInfos.Add(new OctopusUMC.Core.Domain.Entities.LoginInfo
        {
            UserName = user.UserName,
            Ipaddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            LoginLocation = "内网IP",
            Browser = Request.Headers.UserAgent.ToString().Split(' ')[0],
            Os = "Unknown",
            Status = 1,
            Msg = "登录成功",
            LoginTime = DateTime.UtcNow
        });
        _context.SaveChanges();

        return ApiResponse<LoginResponse>.Success(new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            NickName = user.NickName,
            Avatar = user.Avatar,
            Roles = userRoles.Select(r => r.RoleKey).ToList(),
            Permissions = permissions,
        }, "登录成功");
    }

    /// <summary>登出</summary>
    [HttpPost("logout")]
    public async Task<ApiResponse<object?>> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return ApiResponse<object?>.Success(null, "退出成功");
    }

    /// <summary>忘记密码（发送重置邮件）</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ApiResponse<object?>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
        if (user is null)
            return ApiResponse<object?>.Fail("未找到该邮箱对应的账户", 404);

        var token = Guid.NewGuid().ToString("N");
        _cache.Set($"pwd_reset:{token}", user.UserId, TimeSpan.FromMinutes(30));

        var frontendBase = "http://localhost:5173";
        var resetLink = $"{frontendBase}/reset-password?token={token}";
        var html = $@"<p>您好 {user.NickName}，</p>
<p>请点击以下链接重置密码（链接 30 分钟内有效）：</p>
<p><a href=""{resetLink}"">{resetLink}</a></p>
<p>如果您没有发起此请求，请忽略本邮件。</p>";

        await _email.SendAsync(user.Email, user.NickName, "重置您的 OctopusERP 密码", html);
        return ApiResponse<object?>.Success(null, "重置邮件已发送，请查收");
    }

    /// <summary>重置密码（凭邮件 token）</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public ApiResponse<object?> ResetPassword([FromBody] SelfResetPasswordRequest request)
    {
        if (!_cache.TryGetValue($"pwd_reset:{request.Token}", out long userId))
            return ApiResponse<object?>.Fail("重置链接已失效或不存在", 400);

        var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
        if (user is null)
            return ApiResponse<object?>.Fail("用户不存在", 404);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        _context.SaveChanges();
        _cache.Remove($"pwd_reset:{request.Token}");
        return ApiResponse<object?>.Success(null, "密码已重置");
    }

    /// <summary>获取当前登录用户信息</summary>
    [HttpGet("me")]
    [Authorize]
    public ApiResponse<LoginResponse> Me()
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
        if (user == null) return ApiResponse<LoginResponse>.Fail("用户不存在", 404);

        // 通过 UserRole 关联表查询角色
        var roleIds = _context.UserRoles.Where(ur => ur.UserId == user.UserId).Select(ur => ur.RoleId).ToList();
        var userRoles = _context.Roles.Where(r => roleIds.Contains(r.RoleId)).ToList();

        var permissions = new List<string>();
        if (userRoles.Any(r => r.RoleKey == "admin"))
        {
            permissions.Add("*:*:*");
        }
        else
        {
            var roleIdList = userRoles.Select(r => r.RoleId).ToList();
            var menuIds = _context.RoleMenus
                .Where(rm => roleIdList.Contains(rm.RoleId))
                .Select(rm => rm.MenuId)
                .Distinct()
                .ToList();
            permissions = _context.Menus
                .Where(m => menuIds.Contains(m.MenuId) && m.MenuType == "F" && m.Permission != null && m.Permission != "")
                .Select(m => m.Permission!).Distinct().ToList();
        }

        return ApiResponse<LoginResponse>.Success(new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            NickName = user.NickName,
            Avatar = user.Avatar,
            Roles = userRoles.Select(r => r.RoleKey).ToList(),
            Permissions = permissions,
        });
    }
}
