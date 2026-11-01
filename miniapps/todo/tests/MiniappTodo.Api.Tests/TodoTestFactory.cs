using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniappTodo.Api.Persistence;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace MiniappTodo.Api.Tests;

public class TodoTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public TodoTestFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var d = services.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<TodoDbContext>));
            if (d != null) services.Remove(d);
            services.AddDbContext<TodoDbContext>(opt => opt.UseSqlite(_connection));

            // 注意：Keycloak JWT 在测试环境不可用，用 TestAuthHandler 替代 JwtBearer
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TodoTestAuthHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
            db.Database.EnsureCreated();
        });
        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

/// <summary>
/// 模拟 Keycloak JWT 注入到 ClaimsPrincipal。
/// 通过 HTTP Header 切换测试用户：
///   X-Test-Sub:           Keycloak sub UUID（默认 test-user-zhangsan）
///   X-Test-Username:      preferred_username（默认 zhangsan）
///   X-Test-Name:          展示名（默认 张三）
///   X-Test-OctopusUserId: octopus_user_id claim（默认 2）
///   X-Test-DeptName:      primary_dept_name（默认 技术部）
///   X-Test-Company:       company_name（默认 章鱼科技）
///   X-Test-Scope:         miniapp_scope（默认 team）
/// </summary>
public class TodoTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sub      = Request.Headers["X-Test-Sub"].FirstOrDefault() ?? "test-user-zhangsan";
        var username = Request.Headers["X-Test-Username"].FirstOrDefault() ?? "zhangsan";
        var name     = Request.Headers["X-Test-Name"].FirstOrDefault() ?? "张三";
        var octId    = Request.Headers["X-Test-OctopusUserId"].FirstOrDefault() ?? "2";
        var deptId   = Request.Headers["X-Test-DeptId"].FirstOrDefault() ?? "3";
        var deptName = Request.Headers["X-Test-DeptName"].FirstOrDefault() ?? "技术部";
        var company  = Request.Headers["X-Test-Company"].FirstOrDefault() ?? "章鱼科技";
        var scope    = Request.Headers["X-Test-Scope"].FirstOrDefault() ?? "team";

        var claims = new[]
        {
            new Claim("sub", sub),
            new Claim("preferred_username", username),
            new Claim("name", name),
            new Claim("email", $"{username}@octopus.com"),
            new Claim("octopus_user_id", octId),
            new Claim("primary_dept_id", deptId),
            new Claim("primary_dept_name", deptName),
            new Claim("company_name", company),
            new Claim("miniapp_scope", scope),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
    }
}
