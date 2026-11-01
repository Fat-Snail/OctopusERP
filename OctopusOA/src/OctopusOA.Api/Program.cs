using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OctopusOA.Api.Persistence;
using Serilog;
using System.Reflection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg.WriteTo.Console());

// 分布式缓存：Redis（生产）或内存（开发/测试）— 由 Redis:Connection 配置决定
var oaRedis = builder.Configuration["Redis:Connection"];
if (!string.IsNullOrWhiteSpace(oaRedis))
{
    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = oaRedis;
        o.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "oa:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddControllers(o => o.Filters.Add<OctopusOA.Api.Filters.OperationLogFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OctopusOA API", Version = "v1", Description = "办公自动化系统接口" });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// CORS（含 SignalR 跨域，允许所有子系统前端）
builder.Services.AddCors(options =>
    options.AddPolicy("OctopusPolicy", policy =>
        policy.WithOrigins(
                "http://localhost:5173", // UMC.Web
                "http://localhost:5174", // OA.Web
                "http://localhost:5175", // PLM.Web
                "http://localhost:5176", // CRM.Web
                "http://localhost:5177", // WMS.Web
                "http://localhost:5178", // MES.Web
                "http://localhost:5179", // UMC.H5
                "http://localhost:5180"  // OA.H5
              )
              .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

// OpenIddict JWT 验证（指向 UMC OIDC 发现文档）
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"] ?? "http://localhost:5001";
        options.Audience = "octopus-oa-web";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Oidc:Authority"] ?? "http://localhost:5001/",
            ValidateLifetime = true,
        };
        // SignalR WebSocket 从 query string 获取 access_token
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// EF Core：优先使用 MySQL（ConnectionStrings:DefaultConnection），无则回退 SQLite
var oaConnStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<OaDbContext>(opt =>
{
    if (oaConnStr is not null)
        opt.UseMySQL(oaConnStr);
    else
        opt.UseSqlite($"Data Source={GetDbPath("octopus_oa.db")}");
});

builder.Services.AddHttpClient();
builder.Services.AddScoped<OctopusOA.Api.Services.WebhookSender>();
builder.Services.AddSignalR();
builder.Services.AddScoped<OctopusOA.Api.Services.NotificationService>();

// 限流：固定窗口 100次/分钟/IP
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// Services（Scoped，跟随 DbContext）
builder.Services.AddScoped<OctopusOA.Api.Services.ApprovalService>();
builder.Services.AddScoped<OctopusOA.Api.Services.EmployeeService>();
builder.Services.AddScoped<OctopusOA.Api.Services.ContactService>();
builder.Services.AddScoped<OctopusOA.Api.Services.NoticeService>();
builder.Services.AddScoped<OctopusOA.Api.Services.AttendanceService>();
builder.Services.AddScoped<OctopusOA.Api.Services.MeetingService>();
builder.Services.AddScoped<OctopusOA.Api.Services.DashboardService>();
builder.Services.AddHostedService<OctopusOA.Api.Services.ApprovalSlaWorker>();

var app = builder.Build();

// 初始化数据库 + 种子数据
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
    db.Database.EnsureCreated();
    OaDbSeeder.Seed(db);
}

app.UseMiddleware<OctopusOA.Api.Middleware.GlobalExceptionMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Use(async (ctx, next) => { ctx.Request.EnableBuffering(); await next(); });
app.UseCors("OctopusPolicy");
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OctopusOA v1"));
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("api");
app.MapHub<OctopusOA.Api.Hubs.NotificationHub>("/hubs/notification");

app.Run();

static string GetDbPath(string fileName)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            var dataDir = Path.Combine(dir.FullName, "data");
            Directory.CreateDirectory(dataDir);
            return Path.Combine(dataDir, fileName);
        }
        dir = dir.Parent;
    }
    var fallback = Path.Combine(AppContext.BaseDirectory, "data");
    Directory.CreateDirectory(fallback);
    return Path.Combine(fallback, fileName);
}

// 供测试项目使用
public partial class Program { }
