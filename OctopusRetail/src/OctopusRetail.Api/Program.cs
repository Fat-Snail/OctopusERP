// OctopusRetail.Api — 零售域独立服务（design/retail-tenant-workspace.md，路线 B + C）
//
// 定位：
// - 独立容器 + 独立 DB，不读 UMC 数据库、不调 UMC API（MINIAPP-PLATFORM.md 原则 3）
// - 身份来自 Keycloak JWT claims；业务表外键用 Keycloak sub（UUID），不存 UMC UserId
// - 多租户：tenant_id 只落在本库，**绝不回加到 UMC**（design §3）

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OctopusRetail.Api.Multitenancy;
using OctopusRetail.Api.Persistence;
using OctopusRetail.Api.Services;
using PushedAuthorizationBehavior = Microsoft.AspNetCore.Authentication.OpenIdConnect.PushedAuthorizationBehavior;

var builder = WebApplication.CreateBuilder(args);

// ── Keycloak 配置 ─────────────────────────────────────
// ⚠️ 实测：Aspire 的 AddKeycloak(port: 8088) 把 8088 映射到容器的 **8443（HTTPS）**，
// 不是 8080。所以可达端点是 https://localhost:8088，issuer 也是 https://…
// 这里优先取 https 端点，避免 http/https 方案不一致导致
// "The issuer 'https://…' is invalid"。
var keycloakBase =
    builder.Configuration["services:keycloak:https:0"]
    ?? builder.Configuration["services:keycloak:http:0"]
    ?? builder.Configuration["Keycloak:BaseUrl"]
    ?? "https://localhost:8088";
var realm = builder.Configuration["Keycloak:Realm"] ?? "octopus";
var audience = builder.Configuration["Keycloak:Audience"] ?? "octopus-retail";
var issuer = $"{keycloakBase}/realms/{realm}";

// ── 数据层（SQLite 开发库，统一放 repo_root/data） ──────
builder.Services.AddDbContext<RetailDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                  ?? $"Data Source={GetDbPath("octopus_retail.db")}"));

// ── 多租户基础设施 ─────────────────────────────────────
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddHttpContextAccessor();

// ── 零售域服务 ─────────────────────────────────────────
builder.Services.AddHttpClient();                 // KeycloakAdminClient 依赖 IHttpClientFactory
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TenantAuthorization>();
builder.Services.AddScoped<InviteService>();
// 自助建店：解决「引导第一个店长」的鸡生蛋问题
builder.Services.AddScoped<TenantService>();
// 成员管理（零售用户没有 UMC 后台，需要自己的入口）
builder.Services.AddScoped<MemberService>();
// ADR-010：零售建号改为调用 UMC 内部接口，UMC 是唯一用户主数据源
builder.Services.AddScoped<UmcUserClient>();
// M3：Keycloak 受控建号 + group 双写对账
builder.Services.AddScoped<KeycloakAdminClient>();
builder.Services.AddScoped<GroupSyncReconciler>();
builder.Services.AddHostedService<GroupSyncWorker>();

// ── 认证（双通道：Bearer 走 JwtBearer，浏览器走 Cookies + OIDC） ──
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
        o.Cookie.Name = "retail_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, o =>
    {
        o.Authority = issuer;
        o.ClientId = audience;
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.UsePkce = true;
        o.SaveTokens = true;
        o.GetClaimsFromUserInfoEndpoint = true;
        o.RequireHttpsMetadata = false;                 // dev only
        o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.Scope.Clear();
        o.Scope.Add("openid");
        o.Scope.Add("profile");
        o.Scope.Add("email");
        o.TokenValidationParameters.NameClaimType = "preferred_username";
        o.MapInboundClaims = false;
        o.CallbackPath = "/signin-oidc";
        o.SignedOutCallbackPath = "/signout-callback-oidc";
        o.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
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
            // 刻意不设 ValidIssuer：由 discovery 文档的 issuer 决定。
            // 硬编码会用自己拼出的 scheme/端口去比对，一旦与 Keycloak 实际 issuer
            // 有偏差（http vs https）就报 "The issuer … is invalid"。
            ValidateAudience = false,
            ValidateLifetime = true,
            NameClaimType = "preferred_username",
            RoleClaimType = "realm_access.roles",
        };
    });
builder.Services.AddAuthorization();

// ── CORS ──────────────────────────────────────────────
builder.Services.AddCors(o => o.AddPolicy("RetailPolicy", p =>
    p.WithOrigins(
            "http://localhost:5186", "http://localhost:5187",
            "https://retail.octopus.com")
     .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// 与 miniapps/todo 一致的最小 Swagger 配置（Swashbuckle 10 / Microsoft.OpenApi 2.x
// 移除了 Microsoft.OpenApi.Models 命名空间，此处不做自定义 OpenApi 类型配置）
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 启动建表（开发友好；生产走手动迁移）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RetailDbContext>();
    db.Database.EnsureCreated();
    RetailDbSeeder.Seed(db);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseCors("RetailPolicy");
app.UseAuthentication();
app.UseAuthorization();

// ★ 租户解析必须在认证之后（需要 token 的 groups claim）、
//   在业务端点之前（越权防护的第一道防线）
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

// 登录 / 退出（浏览器侧 SSO）
app.MapGet("/login", (string? returnUrl, HttpContext ctx) =>
{
    var props = new AuthenticationProperties { RedirectUri = returnUrl ?? "/" };
    return Results.Challenge(props, new[] { OpenIdConnectDefaults.AuthenticationScheme });
});

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        new[] { OpenIdConnectDefaults.AuthenticationScheme });
});

app.Run();

// 开发库统一放 repo_root/data（向上遍历找 CLAUDE.md 定位仓库根）
static string GetDbPath(string fileName)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        dir = dir.Parent;
    var root = dir?.FullName ?? AppContext.BaseDirectory;
    var dataDir = Path.Combine(root, "data");
    Directory.CreateDirectory(dataDir);
    return Path.Combine(dataDir, fileName);
}

public partial class Program { }
