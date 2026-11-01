using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Api.Services;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using System.Security.Claims;

namespace OctopusUMC.Api.Controllers;

/// <summary>微信 OAuth 2.0 登录（PC 扫码）</summary>
[ApiController]
[Route("api/account/wechat")]
public class WechatController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWechatOAuthService _wechat;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;

    public WechatController(
        ApplicationDbContext db,
        IWechatOAuthService wechat,
        IMemoryCache cache,
        IConfiguration config)
    {
        _db = db;
        _wechat = wechat;
        _cache = cache;
        _config = config;
    }

    /// <summary>获取微信扫码授权 URL</summary>
    [HttpGet("authorize")]
    [AllowAnonymous]
    public IActionResult Authorize([FromQuery] string? returnUrl)
    {
        var state = Guid.NewGuid().ToString("N");
        _cache.Set($"wx_state:{state}", returnUrl ?? "/", TimeSpan.FromMinutes(5));
        var url = _wechat.BuildAuthUrl(state);
        return Ok(ApiResponse<WechatAuthorizeResponse>.Success(new WechatAuthorizeResponse { Url = url }));
    }

    /// <summary>微信回调（WeChat 授权后重定向到此接口）</summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state)
    {
        var frontendBase = _config["WechatSettings:FrontendBaseUrl"] ?? "http://localhost:5173";

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect($"{frontendBase}/login?error=invalid_request");

        if (!_cache.TryGetValue($"wx_state:{state}", out string? savedReturnUrl))
            return Redirect($"{frontendBase}/login?error=state_mismatch");

        _cache.Remove($"wx_state:{state}");

        var token = await _wechat.ExchangeCodeAsync(code);
        if (token is null)
            return Redirect($"{frontendBase}/login?error=wechat_token_error");

        var info = await _wechat.GetUserInfoAsync(token.AccessToken, token.OpenId);
        if (info is null)
            return Redirect($"{frontendBase}/login?error=wechat_userinfo_error");

        // 查找已绑定记录
        var binding = _db.WechatLogins.FirstOrDefault(w => w.OpenId == token.OpenId);
        User user;
        if (binding is null)
        {
            // 首次微信登录：自动创建账户
            user = new User
            {
                UserName = $"wx_{token.OpenId[..8]}",
                NickName = info.NickName,
                Avatar = info.HeadImgUrl,
                Email = $"wx_{token.OpenId}@wechat.placeholder",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                Status = 1,
                CreateTime = DateTime.UtcNow,
            };
            _db.Users.Add(user);
            _db.SaveChanges();

            _db.WechatLogins.Add(new WechatExternalLogin
            {
                UserId = user.UserId,
                OpenId = token.OpenId,
                UnionId = token.UnionId,
                WechatNickName = info.NickName,
                WechatAvatar = info.HeadImgUrl,
            });
            _db.SaveChanges();
        }
        else
        {
            user = _db.Users.FirstOrDefault(u => u.UserId == binding.UserId)!;
            if (user is null || user.Status != 1)
                return Redirect($"{frontendBase}/login?error=user_disabled");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
        };
        await HttpContext.SignInAsync("Cookies",
            new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies")));

        return Redirect(savedReturnUrl ?? frontendBase);
    }

    /// <summary>绑定微信到当前已登录账户</summary>
    [HttpPost("bind")]
    [Authorize]
    public async Task<ApiResponse<object?>> Bind([FromBody] BindWechatRequest request)
    {
        var token = await _wechat.ExchangeCodeAsync(request.Code);
        if (token is null)
            return ApiResponse<object?>.Fail("微信授权失败");

        if (_db.WechatLogins.Any(w => w.OpenId == token.OpenId))
            return ApiResponse<object?>.Fail("该微信已绑定其他账户");

        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (_db.WechatLogins.Any(w => w.UserId == userId))
            return ApiResponse<object?>.Fail("当前账户已绑定微信");

        var info = await _wechat.GetUserInfoAsync(token.AccessToken, token.OpenId);
        _db.WechatLogins.Add(new WechatExternalLogin
        {
            UserId = userId,
            OpenId = token.OpenId,
            UnionId = token.UnionId,
            WechatNickName = info?.NickName,
            WechatAvatar = info?.HeadImgUrl,
        });
        _db.SaveChanges();
        return ApiResponse<object?>.Success(null, "绑定成功");
    }

    /// <summary>解绑微信</summary>
    [HttpDelete("unbind")]
    [Authorize]
    public ApiResponse<object?> Unbind()
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var binding = _db.WechatLogins.FirstOrDefault(w => w.UserId == userId);
        if (binding is null) return ApiResponse<object?>.Fail("未绑定微信");
        _db.WechatLogins.Remove(binding);
        _db.SaveChanges();
        return ApiResponse<object?>.Success(null, "已解绑");
    }
}

/// <summary>绑定微信请求</summary>
public class BindWechatRequest
{
    /// <summary>微信 OAuth 回调中的 code</summary>
    public string Code { get; set; } = string.Empty;
}
