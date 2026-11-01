using OctopusRetail.Api.Multitenancy;

namespace OctopusRetail.Api.Tests;

/// <summary>测试用可变租户上下文（模拟中间件解析结果）</summary>
public sealed class TestTenantContext : ITenantContext
{
    public string? TenantId { get; private set; }
    public bool IsResolved => TenantId is not null;
    public IReadOnlyList<string> AvailableTenants { get; private set; } = Array.Empty<string>();

    public void SetAvailableTenants(IReadOnlyList<string> tenants) => AvailableTenants = tenants;
    public void Resolve(string tenantId) => TenantId = tenantId;

    public static TestTenantContext For(string? tenantId)
    {
        var ctx = new TestTenantContext();
        if (tenantId is not null) ctx.Resolve(tenantId);
        return ctx;
    }
}
