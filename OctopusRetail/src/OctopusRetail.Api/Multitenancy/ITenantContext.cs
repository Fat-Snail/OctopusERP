using System.Security.Claims;

namespace OctopusRetail.Api.Multitenancy;

/// <summary>
/// 请求级租户上下文。
///
/// 关键区分（design/retail-tenant-workspace.md §2.1）：
///   Tenant       —— 数据归属边界，持久化在 retail_tenant
///   Membership   —— user × tenant，持久化在 retail_membership
///   ActiveTenant —— **会话/请求级运行时状态**，就是本接口承载的东西
///
/// ActiveTenant 绝不是数据库字段，也不是前端本地 state：它由 X-Tenant-Id 声明，
/// 由中间件对照 token 中不可伪造的 groups claim 校验后才写入。
/// </summary>
public interface ITenantContext
{
    /// <summary>当前请求已解析的租户；未解析（或跨租户端点）为 null</summary>
    string? TenantId { get; }

    /// <summary>是否已成功解析租户。全局过滤器据此 fail-closed</summary>
    bool IsResolved { get; }

    /// <summary>当前用户可进入的全部租户（来自 token.groups 的 /tenants/* 前缀）</summary>
    IReadOnlyList<string> AvailableTenants { get; }

    /// <summary>由中间件写入解析结果</summary>
    void SetAvailableTenants(IReadOnlyList<string> tenants);

    /// <summary>由中间件在校验通过后写入当前租户</summary>
    void Resolve(string tenantId);
}

/// <summary>可变的请求级实现（Scoped）</summary>
public sealed class TenantContext : ITenantContext
{
    private IReadOnlyList<string> _available = Array.Empty<string>();

    public string? TenantId { get; private set; }
    public bool IsResolved => TenantId is not null;
    public IReadOnlyList<string> AvailableTenants => _available;

    public void SetAvailableTenants(IReadOnlyList<string> tenants) => _available = tenants;
    public void Resolve(string tenantId) => TenantId = tenantId;
}

/// <summary>
/// 标记端点不要求租户上下文（如「列出我的 workspace」「当前用户信息」、健康检查）。
///
/// 未标记的端点一律要求合法 X-Tenant-Id —— 默认拒绝，而不是默认放行。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowCrossTenantAttribute : Attribute;

/// <summary>从 Keycloak token 的 groups claim 中提取租户标识</summary>
public static class TenantClaims
{
    /// <summary>Keycloak Group 中承载租户的根路径（与 octopus-realm.json 的 groups 一致）</summary>
    public const string TenantGroupRoot = "/tenants";

    /// <summary>请求头：声明本次请求的身份（当前 workspace）</summary>
    public const string TenantHeader = "X-Tenant-Id";

    /// <summary>
    /// 从 groups claim 解析可进入的租户集合。
    /// claim 值形如 /tenants/t_shanghai_001（Keycloak Group Membership mapper，full.path=true）。
    /// </summary>
    public static IReadOnlyList<string> ExtractAvailableTenants(ClaimsPrincipal user)
    {
        var prefix = TenantGroupRoot + "/";
        return user.FindAll("groups")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v)
                        && v.StartsWith(prefix, StringComparison.Ordinal))
            .Select(v => v[prefix.Length..].Trim('/'))
            .Where(v => v.Length > 0 && !v.Contains('/'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();
    }
}
