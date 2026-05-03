using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;

namespace OctopusUMC.Api.Controllers;

/// <summary>账户认证接口</summary>
[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AccountController(ApplicationDbContext context) => _context = context;

    /// <summary>登录（颁发 Cookie）</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ApiResponse<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = _context.Users.FirstOrDefault(u => u.UserName == request.UserName);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return ApiResponse<LoginResponse>.Fail("用户名或密码错误", 401);

        if (user.Status != 1)
            return ApiResponse<LoginResponse>.Fail("账号已被禁用", 403);

        // 通过 UserRole 关联表查询角色
        var roleIds = _context.UserRoles.Where(ur => ur.UserId == user.UserId).Select(ur => ur.RoleId).ToList();
        var userRoles = _context.Roles.Where(r => roleIds.Contains(r.RoleId)).ToList();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
        };
        foreach (var role in userRoles) claims.Add(new(ClaimTypes.Role, role.RoleKey));

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

        // 计算权限标识（通过 RoleMenu 关联表）
        var permissions = new List<string>();
        if (userRoles.Any(r => r.RoleKey == "admin"))
        {
            permissions.Add("*:*:*"); // 超级管理员拥有所有权限
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
