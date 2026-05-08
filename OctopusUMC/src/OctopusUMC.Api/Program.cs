using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OctopusUMC.Api.Services;
using OctopusUMC.Infrastructure.Persistence;
using Serilog;
using System.Reflection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console());

builder.Services.AddMemoryCache();

builder.Services.AddControllers(options =>
    options.Filters.AddService<OctopusUMC.Api.Filters.OperLogFilter>());
builder.Services.AddScoped<OctopusUMC.Api.Filters.OperLogFilter>();
builder.Services.AddSingleton<OctopusUMC.Api.Services.OnlineUserService>();
// JobSchedulerService 需要注入 Controller（非 Singleton 不能直接 DI），改为单例并通过 IServiceProvider 访问 Scoped 依赖
builder.Services.AddSingleton<JobSchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<JobSchedulerService>());
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();

// 限流策略
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // 登录接口：固定窗口 — 每个 IP 每分钟最多 10 次（防暴力破解）
    options.AddFixedWindowLimiter("login", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 10;
        o.QueueLimit = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // 全局默认：滑动窗口 — 每个 IP 每分钟 200 次
    options.AddSlidingWindowLimiter("api", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow = 6;
        o.PermitLimit = 200;
        o.QueueLimit = 0;
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OctopusUMC API", Version = "v1", Description = "统一用户中心管理接口" });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// Cookie 认证（UMC 自身会话）
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "umc_session";
        options.LoginPath = "/api/account/login";
        options.Events.OnRedirectToLogin = ctx =>
        {
            // OIDC 授权请求：重定向到 UMC 前端登录页（带 returnUrl 参数）
            if (ctx.Request.Path.StartsWithSegments("/connect"))
            {
                // 原始 OIDC 授权请求完整路径
                var returnUrl = ctx.Request.Path + ctx.Request.QueryString;
                var loginUrl = $"http://localhost:5173/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
                ctx.Response.Redirect(loginUrl);
                return Task.CompletedTask;
            }
            // API 请求返回 401
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
    options.AddPolicy("OctopusPolicy", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:5175", "http://localhost:5176", "http://localhost:5177", "http://localhost:5178")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

// EF Core：优先使用 MySQL（ConnectionStrings:DefaultConnection），无则回退 SQLite
var umcConnStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
{
    if (umcConnStr is not null)
        opt.UseMySQL(umcConnStr);
    else
        opt.UseSqlite("Data Source=octopus_umc.db");
    opt.UseOpenIddict();
});

// OpenIddict OIDC 服务端
builder.Services.AddOpenIddict()
    .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>())
    .AddServer(o =>
    {
        o.SetAuthorizationEndpointUris("/connect/authorize")
         .SetTokenEndpointUris("/connect/token")
         .SetUserInfoEndpointUris("/connect/userinfo")
         .SetEndSessionEndpointUris("/connect/logout");

        o.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        o.AllowRefreshTokenFlow();

        o.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.OfflineAccess,
            "roles");

        o.AddDevelopmentEncryptionCertificate()
         .AddDevelopmentSigningCertificate();

        // 开发环境允许 HTTP（生产环境移除此行）
        o.AddEphemeralEncryptionKey()
         .AddEphemeralSigningKey();
        o.DisableAccessTokenEncryption();

        o.UseAspNetCore()
         .EnableAuthorizationEndpointPassthrough()
         .EnableTokenEndpointPassthrough()
         .EnableUserInfoEndpointPassthrough()
         .EnableEndSessionEndpointPassthrough()
         .DisableTransportSecurityRequirement(); // 开发环境允许 HTTP
    })
    .AddValidation(o =>
    {
        o.UseLocalServer();
        o.UseAspNetCore();
    });

builder.Services.AddHttpClient();
builder.Services.AddScoped<OctopusUMC.Api.Services.UserSyncService>();
builder.Services.AddScoped<OctopusUMC.Api.Services.DeptSyncService>();
builder.Services.AddScoped<OctopusUMC.Api.Services.NoticeSyncService>();

var app = builder.Build();

// 初始化数据库并执行种子数据
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);

    // 种入 OpenIddict OA 客户端
    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    if (await manager.FindByClientIdAsync("octopus-oa-web") is null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "octopus-oa-web",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "OctopusOA Web",
            RedirectUris          = { new Uri("http://localhost:5174/callback") },
            PostLogoutRedirectUris = { new Uri("http://localhost:5174") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",
                OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            },
        });
    }

    // 种入 OpenIddict PLM 客户端
    if (await manager.FindByClientIdAsync("octopus-plm-web") is null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "octopus-plm-web",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "OctopusPLM Web",
            RedirectUris          = { new Uri("http://localhost:5175/callback") },
            PostLogoutRedirectUris = { new Uri("http://localhost:5175") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",
                OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            },
        });
    }

    // 种入 OpenIddict CRM 客户端
    if (await manager.FindByClientIdAsync("octopus-crm-web") is null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "octopus-crm-web",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "OctopusCRM Web",
            RedirectUris          = { new Uri("http://localhost:5176/callback") },
            PostLogoutRedirectUris = { new Uri("http://localhost:5176") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",
                OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            },
        });
    }

    // 种入 OpenIddict WMS 客户端
    if (await manager.FindByClientIdAsync("octopus-wms-web") is null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "octopus-wms-web",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "OctopusWMS Web",
            RedirectUris          = { new Uri("http://localhost:5177/callback") },
            PostLogoutRedirectUris = { new Uri("http://localhost:5177") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",
                OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            },
        });
    }

    // 种入 OpenIddict MES 客户端
    if (await manager.FindByClientIdAsync("octopus-mes-web") is null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "octopus-mes-web",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "OctopusMES Web",
            RedirectUris          = { new Uri("http://localhost:5178/callback") },
            PostLogoutRedirectUris = { new Uri("http://localhost:5178") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",
                OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            },
        });
    }
}

app.UseMiddleware<OctopusUMC.Api.Middleware.GlobalExceptionMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseRateLimiter();
app.UseCors("OctopusPolicy");
app.MapHub<OctopusUMC.Api.Hubs.OnlineUserHub>("/hubs/online");
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OctopusUMC v1"));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// 供测试项目使用
public partial class Program { }
