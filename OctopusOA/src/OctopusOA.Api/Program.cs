using Microsoft.EntityFrameworkCore;
using OctopusOA.Api.Persistence;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg.WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OctopusOA API", Version = "v1", Description = "办公自动化系统接口" });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// CORS
builder.Services.AddCors(options =>
    options.AddPolicy("OctopusPolicy", policy =>
        policy.WithOrigins("http://localhost:5174")
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
    });

builder.Services.AddAuthorization();

// EF Core：优先使用 MySQL（ConnectionStrings:DefaultConnection），无则回退 SQLite
var oaConnStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<OaDbContext>(opt =>
{
    if (oaConnStr is not null)
        opt.UseMySQL(oaConnStr);
    else
        opt.UseSqlite("Data Source=octopus_oa.db");
});

// Services（Scoped，跟随 DbContext）
builder.Services.AddScoped<OctopusOA.Api.Services.ApprovalService>();
builder.Services.AddScoped<OctopusOA.Api.Services.EmployeeService>();
builder.Services.AddScoped<OctopusOA.Api.Services.ContactService>();
builder.Services.AddScoped<OctopusOA.Api.Services.NoticeService>();
builder.Services.AddScoped<OctopusOA.Api.Services.AttendanceService>();
builder.Services.AddScoped<OctopusOA.Api.Services.MeetingService>();
builder.Services.AddScoped<OctopusOA.Api.Services.DashboardService>();

var app = builder.Build();

// 初始化数据库 + 种子数据
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
    db.Database.EnsureCreated();
    OaDbSeeder.Seed(db);
}

app.UseCors("OctopusPolicy");
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OctopusOA v1"));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// 供测试项目使用
public partial class Program { }
