using Microsoft.Extensions.Options;
using System.Text.Json;

namespace OctopusUMC.Api.Services;

public class WechatSettings
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    // 必须与微信开放平台注册的回调域一致，开发时填 http://localhost:5001
    public string CallbackBaseUrl { get; set; } = "http://localhost:5001";
    // 登录成功后跳转的前端地址
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}

public class WechatOAuthService : IWechatOAuthService
{
    private readonly WechatSettings _cfg;
    private readonly HttpClient _http;

    public WechatOAuthService(IOptions<WechatSettings> options, HttpClient http)
    {
        _cfg = options.Value;
        _http = http;
    }

    public string BuildAuthUrl(string state)
    {
        var redirectUri = Uri.EscapeDataString($"{_cfg.CallbackBaseUrl}/api/account/wechat/callback");
        return $"https://open.weixin.qq.com/connect/qrconnect" +
               $"?appid={_cfg.AppId}" +
               $"&redirect_uri={redirectUri}" +
               $"&response_type=code" +
               $"&scope=snsapi_login" +
               $"&state={state}" +
               $"#wechat_redirect";
    }

    public async Task<WechatTokenResult?> ExchangeCodeAsync(string code)
    {
        var url = "https://api.weixin.qq.com/sns/oauth2/access_token" +
                  $"?appid={_cfg.AppId}&secret={_cfg.AppSecret}" +
                  $"&code={code}&grant_type=authorization_code";
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("errcode", out var err) && err.GetInt32() != 0)
            return null;
        return new WechatTokenResult(
            root.GetProperty("openid").GetString()!,
            root.GetProperty("access_token").GetString()!,
            root.TryGetProperty("unionid", out var uid) ? uid.GetString() : null);
    }

    public async Task<WechatUserInfo?> GetUserInfoAsync(string accessToken, string openId)
    {
        var url = "https://api.weixin.qq.com/sns/userinfo" +
                  $"?access_token={accessToken}&openid={openId}&lang=zh_CN";
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("errcode", out var err) && err.GetInt32() != 0)
            return null;
        return new WechatUserInfo(
            root.GetProperty("openid").GetString()!,
            root.TryGetProperty("nickname", out var nn) ? nn.GetString() ?? "微信用户" : "微信用户",
            root.TryGetProperty("headimgurl", out var img) ? img.GetString() : null,
            root.TryGetProperty("unionid", out var uid) ? uid.GetString() : null);
    }
}
