using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OctopusGateway.Api.Tests;

/// <summary>
/// 网关测试工厂：
/// 1. 独立 stub 下游（真 Kestrel，随机端口）承载 /api/echo，验证转发链路；
/// 2. Test 认证 scheme 替代真实 UMC JWT（Gateway 路由只需"已认证"标记）；
/// 3. 限流 PermitLimit 覆盖为 3，便于触发 429。
/// </summary>
public class GatewayTestFactory : WebApplicationFactory<Program>
{
    private WebApplication? _stub;

    public GatewayTestFactory()
    {
        // 启动独立 stub 下游（随机端口），作为 self-cluster 目标
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var stubApp = builder.Build();
        stubApp.MapGet("/api/echo", (HttpRequest req) => Results.Json(new
        {
            code = 200,
            msg = "echo from stub",
            data = new { path = req.Path.Value, downstream = "stub", forwarded = req.Headers["X-Forwarded-For"].ToString() },
        }));
        stubApp.StartAsync().GetAwaiter().GetResult();
        StubUrl = stubApp.Urls.First(u => u.StartsWith("http"));
        _stub = stubApp;
    }

    public string StubUrl { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimit:PermitLimit"] = "3",
            ["RateLimit:WindowSeconds"] = "60",
            ["ReverseProxy:Clusters:self:Destinations:d1:Address"] = StubUrl + "/",
        }));

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, GatewayTestAuthHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            });
        });

        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _stub?.StopAsync().GetAwaiter().GetResult();
    }
}

public class GatewayTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 模拟真实 JWT 语义：无 Authorization 头 = 未认证（触发 Challenge → 401）
        if (string.IsNullOrEmpty(Request.Headers["Authorization"].FirstOrDefault()))
            return Task.FromResult(AuthenticateResult.NoResult());

        // X-Test-UserId 区分测试用户（默认 test-user-1），限流 per-user 测试依赖不同 sub
        var userId = Request.Headers["X-Test-UserId"].FirstOrDefault() ?? "test-user-1";
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("name", userId),
            new Claim("role", "admin"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public static class GatewayTestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
