using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctopusRetail.Api.Persistence;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// 测试用 WebApplicationFactory：SQLite 内存库 + TestAuthHandler。
/// </summary>
public class RetailTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    /// <summary>可控时钟：让邀请过期等时间相关逻辑可确定性测试</summary>
    public MutableTimeProvider Clock { get; } = new();

    public RetailTestFactory()
    {
        // 唯一库名：避免多个测试类共用 :memory:;Cache=Shared 造成数据互相污染
        _connection = new SqliteConnection(
            $"Data Source=retail_test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var d = services.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<RetailDbContext>));
            if (d != null) services.Remove(d);
            services.AddDbContext<RetailDbContext>(opt => opt.UseSqlite(_connection));

            // 覆盖 Program 的 TimeProvider.System（后注册者优先）
            services.AddSingleton<TimeProvider>(Clock);

            // ADR-010：零售建号走 UMC 内部接口。测试里不连真实 UMC，
            // 用假 IHttpClientFactory 固定返回建号成功。
            var fakeUmc = new RecordingHttpHandler
            {
                Responder = req => req.RequestUri!.AbsolutePath.StartsWith("/api/internal/users")
                    ? RecordingHttpHandler.Raw(
                        """{"code":200,"msg":"ok","data":{"userId":999,"kcSub":"kc-umbuilt","userName":"newstaff"}}""")
                    : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound),
            };
            services.AddSingleton<IHttpClientFactory>(new RecordingHttpClientFactory(fakeUmc));

            // Keycloak 在测试环境不可达 → 用 TestAuthHandler 取代 JwtBearer/OIDC
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, RetailTestAuthHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            });
        });

        builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));
        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

/// <summary>可手动推进的时钟（避免测试里 Thread.Sleep）</summary>
public sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);

    public void SetUtcNow(DateTimeOffset value) => _now = value;
}

/// <summary>
/// 模拟 Keycloak JWT，把 claims 注入 ClaimsPrincipal。测试用 HTTP Header 切换身份：
///   X-Test-Sub:      Keycloak sub（默认 kc-zhangsan）
///   X-Test-Tenants:  可进入的租户，逗号分隔（默认 t_shanghai_001,t_chain_hq）
///                    → 映射为 groups claim /tenants/{t}
///   X-Test-Groups:   额外原始 groups claim（逗号分隔，用于验证前缀过滤）
///
/// 默认用户 kc-zhangsan 是「一人两租户」，对应 RetailDbSeeder 的种子。
/// </summary>
public class RetailTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sub = Request.Headers["X-Test-Sub"].FirstOrDefault() ?? "kc-zhangsan";
        // 头部「存在」才决定租户集合：显式传空串 = 该用户没有任何租户
        var tenantsHeader = Request.Headers["X-Test-Tenants"].FirstOrDefault();
        var tenants = tenantsHeader is null
            ? new[] { "t_shanghai_001", "t_chain_hq" }
            : tenantsHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var extraGroups = (Request.Headers["X-Test-Groups"].FirstOrDefault() ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim>
        {
            new("sub", sub),
            new("preferred_username", sub.Replace("kc-", "")),
            new("name", sub),
            new("email", $"{sub}@octopus.com"),
        };
        claims.AddRange(tenants.Select(t => new Claim("groups", $"/tenants/{t}")));
        claims.AddRange(extraGroups.Select(g => new Claim("groups", g)));

        var identity = new ClaimsIdentity(claims, "Test");
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
    }
}
