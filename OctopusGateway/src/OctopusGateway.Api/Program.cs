using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// ── JWT 验签（UMC 签发）──────────────────────────────────────────
// 网关层只验签（保证 token 有效且来自 UMC）；audience 由各下游系统按
// 自己的 octopus-*-web 值二次校验，网关不做 audience 收敛。
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Jwt:Authority"] ?? "http://localhost:5001";
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ClockSkew = TimeSpan.FromMinutes(5),
        };
    });
builder.Services.AddAuthorization(o =>
{
    // FallbackPolicy：所有未显式匿名/授权的端点默认要求认证（含 YARP 转发的全部路由），
    // 实现"外网请求强制走网关鉴权"；/health 与 /_self/echo 以 AllowAnonymous 豁免。
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── 统一限流（P16：按用户固定窗口，未认证按 IP）────────────────────
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy<string>("gateway-per-user", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.FindFirst("sub")?.Value
                ?? ctx.Connection.RemoteIpAddress?.ToString()
                ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimit:PermitLimit", 120),
                Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimit:WindowSeconds", 10)),
                QueueLimit = 0,
            }));
});

// ── CORS：门户前端（5184 Customer / 5185 Supplier）────────────────
builder.Services.AddCors(o => o.AddPolicy("GatewayPolicy", p =>
    p.WithOrigins("http://localhost:5184", "http://localhost:5185")
     .AllowAnyHeader()
     .AllowAnyMethod()));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// ── 请求访问日志（P16：网关统一日志）─────────────────────────────
app.Use(async (ctx, next) =>
{
    var sw = Stopwatch.StartNew();
    var user = ctx.User.FindFirst("sub")?.Value
        ?? ctx.Connection.RemoteIpAddress?.ToString()
        ?? "anon";
    try
    {
        await next();
    }
    finally
    {
        sw.Stop();
        app.Logger.LogInformation(
            "[gateway] {Method} {Path} -> {Status} {Elapsed}ms user={User}",
            ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode,
            sw.ElapsedMilliseconds, user);
    }
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// 限流放在认证之后：per-user key 依赖已填充的 sub claim，未认证请求回落到 IP 维度
app.UseRateLimiter();

// 网关自身健康（匿名，供编排健康检查）
app.MapGet("/health", () => Results.Ok(new
{
    code = 200,
    msg = "ok",
    data = new { service = "octopus-gateway", status = "healthy", time = DateTime.UtcNow },
})).AllowAnonymous();

// 内部自检回声端点：验证"网关 → 下游"转发链路（self-route 用）
app.MapGet("/_self/echo", (HttpRequest req) => Results.Ok(new
{
    code = 200,
    msg = "echo from downstream",
    data = new
    {
        path = req.Path.Value,
        server = "octopus-gateway-self",
        time = DateTime.UtcNow,
    },
})).AllowAnonymous();

// 门户 API 转发：所有 /gw/{system}/** 路由强制认证 + 统一限流
app.MapReverseProxy()
    .RequireAuthorization()
    .RequireRateLimiting("gateway-per-user");

app.Run();
