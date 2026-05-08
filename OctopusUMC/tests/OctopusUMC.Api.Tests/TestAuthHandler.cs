using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 测试专用认证处理器。
/// 默认以 admin（UserId=1）身份通过认证；
/// X-Test-UserId 头可切换为指定用户；
/// X-Test-Anonymous 头触发 NoResult（→ 401）。
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApplicationDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Anonymous"))
            return Task.FromResult(AuthenticateResult.NoResult());

        long userId = 1;
        if (Request.Headers.TryGetValue("X-Test-UserId", out var idHeader) &&
            long.TryParse(idHeader, out var parsed))
            userId = parsed;

        var user = db.Users.FirstOrDefault(u => u.UserId == userId);
        if (user == null)
            return Task.FromResult(AuthenticateResult.Fail($"Test user {userId} not found"));

        var userRoles = db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.Roles, ur => ur.RoleId, r => r.RoleId, (_, r) => r)
            .ToList();

        var permissions = new List<string>();
        if (userRoles.Any(r => r.RoleKey == "admin"))
        {
            permissions.Add("*:*:*");
        }
        else
        {
            var roleIds = userRoles.Select(r => r.RoleId).ToList();
            var menuIds = db.RoleMenus
                .Where(rm => roleIds.Contains(rm.RoleId))
                .Select(rm => rm.MenuId)
                .Distinct()
                .ToList();
            permissions = db.Menus
                .Where(m => menuIds.Contains(m.MenuId) && m.MenuType == "F"
                         && m.Permission != null && m.Permission != "")
                .Select(m => m.Permission!)
                .Distinct()
                .ToList();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
        };
        foreach (var role in userRoles)
            claims.Add(new Claim(ClaimTypes.Role, role.RoleKey));
        foreach (var perm in permissions)
            claims.Add(new Claim("permission", perm));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
