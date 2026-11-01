namespace OctopusUMC.Api.DTOs;

/// <summary>微信授权 URL 响应</summary>
public class WechatAuthorizeResponse
{
    /// <summary>微信扫码授权 URL（前端 redirect 到此地址）</summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>密码重置请求（忘记密码）</summary>
public class ForgotPasswordRequest
{
    /// <summary>注册邮箱</summary>
    public string Email { get; set; } = string.Empty;
}
