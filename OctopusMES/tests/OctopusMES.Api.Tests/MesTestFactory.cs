using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctopusMES.Api.Persistence;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace OctopusMES.Api.Tests;

public class MesTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public MesTestFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                s => s.ServiceType == typeof(DbContextOptions<MesDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<MesDbContext>(opt => opt.UseSqlite(_connection));

            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, MesTestAuthHandler>("Test", _ => { });

            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
            db.Database.EnsureCreated();
            MesDbSeeder.Seed(db);
        });

        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public class MesTestAuthHandler(
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
