using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusOA.Api.Controllers;
using OctopusOA.Api.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

public class OATestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public OATestFactory()
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
                s => s.ServiceType == typeof(DbContextOptions<OaDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<OaDbContext>(opt => opt.UseSqlite(_connection));

            // 测试环境：用无验证的 JWT 策略
            services.AddAuthentication("Test")
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                    "Test", options => { });

            services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            });

            // 初始化 schema + 种子
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
            db.Database.EnsureCreated();
            OaDbSeeder.Seed(db);
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
public class TestAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        // 支持通过 X-Test-UserId header 切换测试用户（默认 admin=1）
        var userIdStr = Request.Headers["X-Test-UserId"].FirstOrDefault() ?? "1";
        var userNameMap = new Dictionary<string, (string name, string email)>
        {
            ["1"] = ("admin", "admin@octopus.com"),
            ["2"] = ("zhangsan", "zhangsan@octopus.com"),
            ["3"] = ("lisi", "lisi@octopus.com"),
        };
        var (name, email) = userNameMap.TryGetValue(userIdStr, out var v) ? v : ($"user{userIdStr}", $"user{userIdStr}@test.com");
        var claims = new[]
        {
            new System.Security.Claims.Claim("sub", userIdStr),
            new System.Security.Claims.Claim("name", name),
            new System.Security.Claims.Claim("email", email),
            new System.Security.Claims.Claim("role", userIdStr == "1" ? "admin" : "common"),
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Test");
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}

public class UserSyncControllerTests : IClassFixture<OATestFactory>
{
    private readonly HttpClient _client;
    private const string SharedSecret = "octopus-sync-secret-key-2026";

    public UserSyncControllerTests(OATestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private static string ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    [Fact]
    public async Task SyncUser_NewUser_CreatesInSyncUsers()
    {
        var json = JsonSerializer.Serialize(new
        {
            UserId = 999, UserName = "newuser_sync", NickName = "新同步用户",
            Email = "newuser@octopus.com", PhoneNumber = "13900000099", Status = 1
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/sync");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(json)}");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("新增");
    }

    [Fact]
    public async Task SyncUser_ExistingUser_UpdatesSyncUsers()
    {
        var json = JsonSerializer.Serialize(new
        {
            UserId = 1, UserName = "admin", NickName = "超级管理员（已同步更新）",
            Email = "admin_updated@octopus.com", Status = 1
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/sync");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(json)}");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("更新");
    }

    [Fact]
    public async Task GetSyncUsers_ReturnsSeededUsers()
    {
        var response = await _client.GetAsync("/api/users/sync");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BackchannelLogout_Returns200()
    {
        var response = await _client.PostAsync("/api/auth/backchannel-logout", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMe_WithJwtClaims_ReturnsUser()
    {
        var response = await _client.GetAsync("/api/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task SyncUser_WithInvalidSignature_Returns401()
    {
        var json = JsonSerializer.Serialize(new
        {
            UserId = 888, UserName = "hacker", NickName = "黑客",
            Email = "hacker@evil.com", Status = 1
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/sync");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Sync-Signature", "sha256=invalid_signature");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
