using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctopusCRM.Api.Persistence;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace OctopusCRM.Api.Tests;

public class CrmTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CrmTestFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 替换 DbContext 为共享内存 SQLite
            var descriptor = services.SingleOrDefault(
                s => s.ServiceType == typeof(DbContextOptions<CrmDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<CrmDbContext>(opt => opt.UseSqlite(_connection));

            // 测试环境：无验证的测试 Auth scheme
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, CrmTestAuthHandler>("Test", _ => { });

            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            });

            // 初始化 schema + 种子
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            db.Database.EnsureCreated();
            CrmDbSeeder.Seed(db);
        });

        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

/// <summary>测试认证处理器：通过 X-Test-UserId 切换身份（默认 admin=1）</summary>
public class CrmTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userIdStr = Request.Headers["X-Test-UserId"].FirstOrDefault() ?? "1";
        var userNameMap = new Dictionary<string, (string name, string email)>
        {
            ["1"] = ("admin", "admin@octopus.com"),
            ["2"] = ("zhangsan", "zhangsan@octopus.com"),
            ["3"] = ("lisi", "lisi@octopus.com"),
        };
        var (name, email) = userNameMap.TryGetValue(userIdStr, out var v)
            ? v : ($"user{userIdStr}", $"user{userIdStr}@test.com");

        var claims = new[]
        {
            new Claim("sub", userIdStr),
            new Claim("name", name),
            new Claim("email", email),
            new Claim("role", userIdStr == "1" ? "admin" : "common"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
