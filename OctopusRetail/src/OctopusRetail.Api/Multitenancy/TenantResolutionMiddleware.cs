using System.Text.Json;

namespace OctopusRetail.Api.Multitenancy;

/// <summary>
/// 租户解析中间件 —— 越权防护的**唯一**入口（design §2.3 / §7 第一道防线）。
///
/// 规则（默认拒绝）：
///   1. 未认证请求            → 放行，交给认证/授权管线决定（通常 401）
///   2. 端点标记 [AllowCrossTenant] → 放行（仅登记可进入的租户集合）
///   3. 端点无租户要求（匿名/404） → 放行
///   4. 缺少 X-Tenant-Id      → 400
///   5. X-Tenant-Id ∉ token.groups 的租户集合 → 403
///   6. 校验通过              → 写入 ITenantContext，后续由全局过滤器保证隔离
///
/// 第 5 条是本方案安全性的核心：X-Tenant-Id 是客户端可任意伪造的请求头，
/// 只有对照 token 中不可伪造的 groups claim 校验后才有意义。
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        var endpoint = context.GetEndpoint();
        var available = TenantClaims.ExtractAvailableTenants(context.User);
        tenant.SetAvailableTenants(available);

        // 1) 未认证：交给认证管线
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        // 2) 跨租户端点：放行，不解析当前租户
        if (endpoint?.Metadata.GetMetadata<AllowCrossTenantAttribute>() is not null)
        {
            await next(context);
            return;
        }

        // 3) 不需要租户的端点（匿名、健康检查、未匹配路由）
        if (endpoint is null || !RequiresTenant(endpoint))
        {
            await next(context);
            return;
        }

        // 4) 缺少租户声明
        var declared = context.Request.Headers[TenantClaims.TenantHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(declared))
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest,
                "缺少租户上下文，请在 X-Tenant-Id 头中声明当前租户");
            return;
        }

        // 5) 伪造/越权检查 —— 必须对照 token 校验
        if (!available.Contains(declared, StringComparer.Ordinal))
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden,
                "无权访问该租户");
            return;
        }

        // 6) 通过
        tenant.Resolve(declared);
        await next(context);
    }

    /// <summary>
    /// 端点是否需要租户：需认证且未标记 [AllowCrossTenant]。
    /// 匿名端点（登录、健康检查）不需要租户。
    /// </summary>
    private static bool RequiresTenant(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>() is not null
        && endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is null;

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            code = statusCode,
            msg = message,
            data = (object?)null,
        }));
    }
}
