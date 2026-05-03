using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>OpenIddict OIDC 端点（授权、令牌、用户信息、登出）</summary>
public class AuthorizationController : Controller
{
    private readonly ApplicationDbContext _context;

    public AuthorizationController(ApplicationDbContext context) => _context = context;

    /// <summary>授权端点 — OA 跳转到此处发起 OIDC 授权码流</summary>
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("无法获取 OpenIddict 请求。");

        // 检查是否已有 Cookie 会话
        var result = await HttpContext.AuthenticateAsync("Cookies");
        if (!result.Succeeded || result.Principal == null)
        {
            // 未登录 → 重定向到 UMC 登录页，登录后回到此授权端点
            var returnUrl = Request.PathBase + Request.Path + QueryString.Create(
                Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList());

            return Challenge(
                authenticationSchemes: new[] { "Cookies" },
                properties: new AuthenticationProperties { RedirectUri = returnUrl });
        }

        // 已登录 → 从 Cookie Claims 构建 OIDC ClaimsIdentity
        var userId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

        var user = userId != null
            ? _context.Users.FirstOrDefault(u => u.UserId == long.Parse(userId))
            : null;

        var claims = new List<Claim>
        {
            new(OpenIddictConstants.Claims.Subject, userId ?? "0"),
            new(OpenIddictConstants.Claims.Name, userName ?? ""),
        };

        if (user != null)
        {
            claims.Add(new(OpenIddictConstants.Claims.Email, user.Email));
            claims.Add(new(OpenIddictConstants.Claims.Nickname, user.NickName));

            // 添加角色 Claims
            var roleIds = _context.UserRoles.Where(ur => ur.UserId == user.UserId).Select(ur => ur.RoleId).ToList();
            var roles = _context.Roles.Where(r => roleIds.Contains(r.RoleId)).ToList();
            foreach (var role in roles)
                claims.Add(new(OpenIddictConstants.Claims.Role, role.RoleKey));
        }

        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // 设置每个 claim 的目标（access_token / id_token）
        foreach (var claim in identity.Claims)
        {
            claim.SetDestinations(claim.Type switch
            {
                OpenIddictConstants.Claims.Name or
                OpenIddictConstants.Claims.Nickname or
                OpenIddictConstants.Claims.Email =>
                    new[] { OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken },

                OpenIddictConstants.Claims.Role =>
                    new[] { OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken },

                _ => new[] { OpenIddictConstants.Destinations.AccessToken },
            });
        }

        identity.SetScopes(request.GetScopes());

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>令牌端点 — 用授权码换取 access_token</summary>
    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("无法获取 OpenIddict 请求。");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = result.Principal!;

            // 刷新令牌时重新加载用户数据
            var userId = principal.GetClaim(OpenIddictConstants.Claims.Subject);
            if (userId != null)
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == long.Parse(userId));
                if (user != null && user.Status != 1)
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.AccessDenied,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "账号已被禁用"
                        }));
                }

            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(claim.Type switch
                {
                    OpenIddictConstants.Claims.Name or
                    OpenIddictConstants.Claims.Nickname or
                    OpenIddictConstants.Claims.Email =>
                        new[] { OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken },

                    OpenIddictConstants.Claims.Role =>
                        new[] { OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken },

                    _ => new[] { OpenIddictConstants.Destinations.AccessToken },
                });
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("不支持的授权类型。");
    }

    /// <summary>用户信息端点 — 返回当前用户 Claims</summary>
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    public IActionResult Userinfo()
    {
        var subject = User.GetClaim(OpenIddictConstants.Claims.Subject);
        var user = subject != null
            ? _context.Users.FirstOrDefault(u => u.UserId == long.Parse(subject))
            : null;

        if (user == null)
            return Challenge(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "用户不存在"
                }));

        var roleIds = _context.UserRoles.Where(ur => ur.UserId == user.UserId).Select(ur => ur.RoleId).ToList();
        var roles = _context.Roles.Where(r => roleIds.Contains(r.RoleId)).Select(r => r.RoleKey).ToList();

        var claims = new Dictionary<string, object>
        {
            [OpenIddictConstants.Claims.Subject] = user.UserId.ToString(),
            [OpenIddictConstants.Claims.Name] = user.UserName,
            [OpenIddictConstants.Claims.Nickname] = user.NickName,
            [OpenIddictConstants.Claims.Email] = user.Email,
            [OpenIddictConstants.Claims.Role] = roles,
        };

        return Ok(claims);
    }

    /// <summary>登出端点 — 清除 UMC Cookie + 触发 Back-Channel Logout</summary>
    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }
}
