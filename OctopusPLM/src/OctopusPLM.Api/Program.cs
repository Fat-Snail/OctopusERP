using Microsoft.EntityFrameworkCore;
using OctopusPLM.Infrastructure.Persistence;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg.WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OctopusPLM API", Version = "v1", Description = "商品生命周期管理系统" });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// CORS
builder.Services.AddCors(options =>
    options.AddPolicy("OctopusPolicy", policy =>
        policy.WithOrigins("http://localhost:5175")
              .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

// OpenIddict JWT 验证（指向 UMC OIDC 发现文档）
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"] ?? "http://localhost:5001";
        options.Audience = "octopus-plm-web";
        options.RequireHttpsMetadata = false;
        var authority = (builder.Configuration["Oidc:Authority"] ?? "http://localhost:5001").TrimEnd('/');
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidIssuers = new[] { authority, authority + "/" },
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization();

// EF Core：优先使用 MySQL（ConnectionStrings:DefaultConnection），无则回退 SQLite
var plmConnStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PlmDbContext>(opt =>
{
    if (plmConnStr is not null)
        opt.UseMySQL(plmConnStr);
    else
        opt.UseSqlite("Data Source=octopus_plm.db");
});

// 业务服务
builder.Services.AddScoped<OctopusPLM.Api.Services.CategoryService>();
builder.Services.AddScoped<OctopusPLM.Api.Services.ProductService>();

// 向量搜索服务（Singleton：CLIP ONNX Session + Qdrant 客户端复用，IDisposable 由 DI 托管）
builder.Services.AddHttpClient();
builder.Services.AddSingleton<OctopusPLM.Api.Services.VectorService>();

var app = builder.Build();

// 初始化数据库 + 种子数据
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlmDbContext>();
    db.Database.EnsureCreated();
    PlmDbSeeder.Seed(db);
}

// 确保 Qdrant collection 存在（异步，非阻塞启动）
_ = Task.Run(async () =>
{
    try { await app.Services.GetRequiredService<OctopusPLM.Api.Services.VectorService>().EnsureCollectionAsync(); }
    catch { /* Qdrant 不可用时静默忽略 */ }
});

app.UseCors("OctopusPolicy");
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OctopusPLM v1"));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// 供测试项目使用
public partial class Program { }
