using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;
using OctopusMES.Api.Services.PlmSync;
using OctopusMES.Api.Services.Scheduling;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 分布式缓存：Redis（生产）或内存（开发/测试）
var mesRedis = builder.Configuration["Redis:Connection"];
if (!string.IsNullOrWhiteSpace(mesRedis))
{
    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = mesRedis;
        o.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "mes:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddControllers(o => o.Filters.Add<OctopusMES.Api.Filters.OperationLogFilter>())
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(o => o.AddPolicy("MesPolicy", p =>
    p.WithOrigins("http://localhost:5178")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// EF Core SQLite
builder.Services.AddDbContext<MesDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                  ?? $"Data Source={GetDbPath("octopus_mes.db")}"));

// JWT Bearer（向 UMC 验证）
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Jwt:Authority"] ?? "http://localhost:5001";
        o.Audience = builder.Configuration["Jwt:Audience"] ?? "octopus-mes-web";
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ClockSkew = TimeSpan.FromMinutes(5),
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlmSyncView", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("PlmSyncRetry", policy => policy.RequireRole("admin"));
    options.AddPolicy("PlmSyncForce", policy => policy.RequireRole("admin"));
    options.AddPolicy("ProductionOrderVersionSwitch", policy => policy.RequireRole("admin"));
});

builder.Services.AddHttpClient();
builder.Services.AddScoped<WebhookSender>();
builder.Services.AddScoped<IPlmSyncAlertSink, PlmSyncAlertSink>();

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

// Services（Scoped）
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<WorkOrderService>();
builder.Services.AddScoped<MesStatsService>();
builder.Services.AddScoped<GarmentScheduleService>();
builder.Services.AddScoped<GarmentTeamService>();
builder.Services.AddScoped<ProductionOrderService>();
builder.Services.AddScoped<ResourceCalendarService>();
builder.Services.AddSingleton<CapacityCalculator>();
builder.Services.AddSingleton<ConflictDetector>();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddScoped<PlmProductSyncService>();
builder.Services.AddScoped<OctopusMES.Api.Services.PlmSync.IPlmManufacturingClient, OctopusMES.Api.Services.PlmSync.PlmManufacturingClient>();
builder.Services.AddScoped<OctopusMES.Api.Services.PlmSync.PlmManufacturingSyncService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<OctopusMES.Api.Services.PlmSync.PlmSyncRetryCoordinator>();
builder.Services.AddHostedService<OctopusMES.Api.Services.PlmSync.PlmSyncRetryWorker>();

var app = builder.Build();

// DB init
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
    db.Database.EnsureCreated();
    MesDbSeeder.Seed(db);

    // 存量数据订正：Seeder 只在空库插入，老库里的历史内容（如真实姓名）必须靠迁移清理
    var migLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("MesDataMigration");
    MesDataMigrationRunner.Apply(db, migLogger);
}

// 启用请求体 buffering 以便 Webhook 控制器可以读取原始 body 验证 HMAC
app.Use(async (ctx, next) => { ctx.Request.EnableBuffering(); await next(); });

app.UseMiddleware<OctopusMES.Api.Middleware.GlobalExceptionMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("MesPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("api");
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

public partial class Program { }
