using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctopusPLM.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OctopusPLM.Api.Tests;

public class PlmTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public PlmTestFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                s => s.ServiceType == typeof(DbContextOptions<PlmDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<PlmDbContext>(opt => opt.UseSqlite(_connection));

            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "Test", options => { });

            services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlmDbContext>();
            db.Database.EnsureCreated();
            PlmDbSeeder.Seed(db);
        });

        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

/// <summary>测试用认证处理器：模拟已登录用户</summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userIdStr = Request.Headers["X-Test-UserId"].FirstOrDefault();
        if (userIdStr == null)
            return Task.FromResult(AuthenticateResult.NoResult());
        var userNameMap = new Dictionary<string, (string name, string email)>
        {
            ["1"] = ("admin", "admin@octopus.com"),
            ["2"] = ("zhangsan", "zhangsan@octopus.com"),
            ["3"] = ("lisi", "lisi@octopus.com"),
        };

        var (name, email) = userNameMap.GetValueOrDefault(userIdStr, ("test", "test@test.com"));

        var claims = new[]
        {
            new Claim("sub", userIdStr),
            new Claim(ClaimTypes.NameIdentifier, userIdStr),
            new Claim("name", name),
            new Claim(ClaimTypes.Name, name),
            new Claim("email", email),
            new Claim("role", "plm_admin"),
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }
}
