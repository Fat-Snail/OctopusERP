// MiniappTodo.Api — OctopusERP 小程序平台 demo
//
// 设计原则（来自 MINIAPP-PLATFORM.md）：
// - 独立域名 + 独立容器 + 独立 DB
// - 仅信任 Keycloak JWT claims，绝不读 UMC 数据库或调 UMC API
// - 业务表用 KcSub (Keycloak sub UUID) 作为用户外键
// - 用户首次访问时按 JWT claims 创建本地 UserProfile (Lazy Provisioning)

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MiniappTodo.Api.Persistence;
using PushedAuthorizationBehavior = Microsoft.AspNetCore.Authentication.OpenIdConnect.PushedAuthorizationBehavior;

var builder = WebApplication.CreateBuilder(args);

// ── 配置加载 ─────────────────────────────────────────
// Aspire 注入：services:keycloak:http:0 = http://localhost:8088
// 生产 fallback：Keycloak:BaseUrl
var keycloakBase =
    builder.Configuration["services:keycloak:http:0"]
    ?? builder.Configuration["Keycloak:BaseUrl"]
    ?? "http://localhost:8088";

var realm = builder.Configuration["Keycloak:Realm"] ?? "octopus";
var audience = builder.Configuration["Keycloak:Audience"] ?? "miniapp-todo";
var issuer = $"{keycloakBase}/realms/{realm}";

// ── 服务注册 ─────────────────────────────────────────
builder.Services.AddDbContext<TodoDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                   ?? "Data Source=miniapp_todo.db"));

builder.Services.AddCors(o => o.AddPolicy("MiniappPolicy", p =>
    p.WithOrigins(
        "http://localhost:5201", "http://localhost:5301",
        "https://todo.miniapp.octopus.com")
     .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// 双通道认证（PolicyScheme 自动路由）：
// - 有 Authorization: Bearer header → JwtBearer 验证（API 调用）
// - 无 → Cookies（Razor Pages 浏览器会话，未登录会 challenge OIDC 跳 Keycloak）
//
// 这样所有 [Authorize] 都不需要指定 scheme，测试侧可以一键替换为 TestAuth
const string MultiAuthScheme = "MultiAuth";
builder.Services.AddAuthentication(MultiAuthScheme)
.AddPolicyScheme(MultiAuthScheme, MultiAuthScheme, o =>
{
    o.ForwardDefaultSelector = ctx =>
    {
        var auth = ctx.Request.Headers.Authorization.ToString();
        return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? JwtBearerDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
{
    o.Cookie.Name = "miniapp_todo_session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, o =>
{
    o.Authority = issuer;
    o.ClientId = audience;                     // miniapp-todo
    o.ResponseType = OpenIdConnectResponseType.Code;
    o.UsePkce = true;
    o.SaveTokens = true;
    o.GetClaimsFromUserInfoEndpoint = true;
    o.RequireHttpsMetadata = false;            // dev only
    o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.Scope.Clear();
    o.Scope.Add("openid");
    o.Scope.Add("profile");
    o.Scope.Add("email");
    // 自定义 claims（octopus_user_id / dept / company / miniapp_scope）通过
    // 直接挂在 client 的 protocolMappers 注入，无需额外 scope
    o.TokenValidationParameters.NameClaimType = "preferred_username";
    o.MapInboundClaims = false;                // 保留原始 claim 名（不映射成 .NET 内置 ClaimTypes）
    o.CallbackPath = "/signin-oidc";
    o.SignedOutCallbackPath = "/signout-callback-oidc";
    // .NET 10 默认行为：若 KC discovery 支持 PAR 则使用 → 但 redirect_uri 可能匹配失败
    // 强制禁用 PAR，回退到标准 Authorization Code Flow
    o.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
    // 失败快速重试：默认 30s 间隔太长，开发期改 5s
    o.RefreshOnIssuerKeyNotFound = true;
    o.AutomaticRefreshInterval = TimeSpan.FromMinutes(5);
    o.RefreshInterval = TimeSpan.FromSeconds(5);
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
{
    o.Authority = issuer;
    o.Audience = audience;
    o.RequireHttpsMetadata = false;
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = false,
        ValidateLifetime = true,
        NameClaimType = "preferred_username",
        RoleClaimType = "realm_access.roles",
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<MiniappTodo.Api.Services.UserContextService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 启动时建表（dev 友好）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("MiniappPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();

// 登录：触发 OIDC challenge（跳转 Keycloak）
app.MapGet("/login", (string? returnUrl, HttpContext ctx) =>
{
    var props = new AuthenticationProperties { RedirectUri = returnUrl ?? "/" };
    return Results.Challenge(props, new[] { OpenIdConnectDefaults.AuthenticationScheme });
});

// 退出：清 Cookie + 通知 Keycloak 注销
app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        new[] { OpenIdConnectDefaults.AuthenticationScheme });
});

app.Run();

public partial class Program { }
