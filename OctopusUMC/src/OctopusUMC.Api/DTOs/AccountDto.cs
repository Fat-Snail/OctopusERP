namespace OctopusUMC.Api.DTOs;

/// <summary>登录请求</summary>
public class LoginRequest
{
    /// <summary>用户名</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>密码（明文）</summary>
    public string Password { get; set; } = string.Empty;
    /// <summary>记住我</summary>
    public bool RememberMe { get; set; }
}

/// <summary>登录响应</summary>
public class LoginResponse
{
    /// <summary>用户ID</summary>
    public long UserId { get; set; }
    /// <summary>用户名</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>昵称</summary>
    public string NickName { get; set; } = string.Empty;
    /// <summary>头像URL</summary>
    public string? Avatar { get; set; }
    /// <summary>角色标识列表</summary>
    public List<string> Roles { get; set; } = new();
    /// <summary>权限标识列表</summary>
    public List<string> Permissions { get; set; } = new();
}

/// <summary>用户自助重置密码请求（凭邮件 token）</summary>
public class SelfResetPasswordRequest
{
    /// <summary>邮件链接中的重置 token</summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>新密码</summary>
    public string NewPassword { get; set; } = string.Empty;
}
