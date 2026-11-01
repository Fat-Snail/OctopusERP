using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctopusHRM.Api.Persistence;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace OctopusHRM.Api.Tests;

public class HrmTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public HrmTestFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var d = services.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<HrmDbContext>));
            if (d != null) services.Remove(d);
            services.AddDbContext<HrmDbContext>(opt => opt.UseSqlite(_connection));

            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, HrmTestAuthHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            db.Database.EnsureCreated();
            HrmDbSeeder.Seed(db);
        });
        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public class HrmTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userIdStr = Request.Headers["X-Test-UserId"].FirstOrDefault() ?? "1";
        var claims = new[]
        {
            new Claim("sub", userIdStr),
            new Claim("name", userIdStr == "1" ? "admin" : $"user{userIdStr}"),
            new Claim("role", userIdStr == "1" ? "admin" : "common"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
