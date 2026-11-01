using System.Security.Claims;

namespace OctopusRetail.Api.Services;

/// <summary>
/// 当前用户上下文：只从 Keycloak JWT claims 提取，**绝不回查 UMC**
/// （MINIAPP-PLATFORM.md 原则 3；design §3.4）。
/// </summary>
public class CurrentUser(IHttpContextAccessor accessor)
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    /// <summary>Keycloak sub —— 零售域引用用户的唯一外键</summary>
    public string Sub =>
        Principal?.FindFirst("sub")?.Value
        ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? string.Empty;

    public string UserName => Principal?.FindFirst("preferred_username")?.Value ?? string.Empty;

    public string DisplayName =>
        Principal?.FindFirst("name")?.Value
        ?? Principal?.FindFirst("given_name")?.Value
        ?? UserName;

    public string? Email => Principal?.FindFirst("email")?.Value;

    /// <summary>是否可管理租户（平台级操作）。测试环境通过 claim 注入</summary>
    public bool IsPlatformAdmin =>
        Principal?.FindAll("realm_access.roles").Any(c => c.Value.Contains("platform_admin")) == true;
}
