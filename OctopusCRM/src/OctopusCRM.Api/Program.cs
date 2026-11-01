using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;
using OctopusCRM.Api.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 分布式缓存：Redis（生产）或内存（开发/测试）
var crmRedis = builder.Configuration["Redis:Connection"];
if (!string.IsNullOrWhiteSpace(crmRedis))
{
    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = crmRedis;
        o.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "crm:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddControllers(o => o.Filters.Add<OctopusCRM.Api.Filters.OperationLogFilter>())
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(o => o.AddPolicy("CrmPolicy", p =>
    p.WithOrigins("http://localhost:5176")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// EF Core SQLite
builder.Services.AddDbContext<CrmDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                  ?? $"Data Source={GetDbPath("octopus_crm.db")}"));

// JWT Bearer（向 UMC 验证）
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Jwt:Authority"] ?? "http://localhost:5001";
        o.Audience = builder.Configuration["Jwt:Audience"] ?? "octopus-crm-web";
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ClockSkew = TimeSpan.FromMinutes(5),
        };
    });
builder.Services.AddAuthorization();

// Services（Scoped）
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<InquiryService>();
builder.Services.AddScoped<QuoteService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<StatsService>();

builder.Services.AddHttpClient();
builder.Services.AddScoped<WebhookSender>();

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

var app = builder.Build();

// DB init
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    db.Database.EnsureCreated();
    CrmDbSeeder.Seed(db);

    // 存量数据订正（Seeder 只在空库插入，老库里的历史内容必须靠迁移清理）
    var migLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("CrmDataMigration");
    DataMigrationRunner.Apply(db, msg => migLogger.LogWarning("{Msg}", msg));
}

app.UseMiddleware<OctopusCRM.Api.Middleware.GlobalExceptionMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseSwagger();
app.UseSwaggerUI();
app.Use(async (ctx, next) => { ctx.Request.EnableBuffering(); await next(); });
app.UseCors("CrmPolicy");
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
