using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;
using OctopusCRM.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
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
                  ?? "Data Source=octopus_crm.db"));

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

var app = builder.Build();

// DB init
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    db.Database.EnsureCreated();
    CrmDbSeeder.Seed(db);
}

app.UseMiddleware<OctopusCRM.Api.Middleware.GlobalExceptionMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseSwagger();
app.UseSwaggerUI();
app.Use(async (ctx, next) => { ctx.Request.EnableBuffering(); await next(); });
app.UseCors("CrmPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
