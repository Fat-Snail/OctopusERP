using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctopusRetail.Api.Persistence;
using OctopusRetail.Api.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// M5 — 端到端联调工厂。
///
/// 与 RetailTestFactory 的差异：**启用 Keycloak 并注入假 Admin 端点**，
/// 从而能跑通「邀请 → 受控建号 → 成员关系 → group 对账 → 新用户凭租户身份访问」的完整闭环。
/// </summary>
public class RetailE2EFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public RecordingHttpHandler Keycloak { get; } = new();
    public MutableTimeProvider Clock { get; } = new();

    /// <summary>按用户名返回的 Keycloak user id（建号后回查用）</summary>
    public string CreatedUserId { get; set; } = "kc-e2e-newuser";

    public RetailE2EFactory()
    {
        _connection = new SqliteConnection($"Data Source=e2e_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        _connection.Open();

        Keycloak.Responder = req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            var method = req.Method;

            // ADR-010：零售建号改为调用 UMC 内部接口，这里让假服务同时扮演 UMC
            if (path.StartsWith("/api/internal/users"))
                return RecordingHttpHandler.Raw(
                    "{\"code\":200,\"msg\":\"ok\",\"data\":{\"userId\":999,\"kcSub\":\""
                    + CreatedUserId
                    + "\",\"userName\":\"newstaff\"}}");

            if (method == HttpMethod.Post && path.EndsWith("/users"))
                return new HttpResponseMessage(HttpStatusCode.Created);

            if (method == HttpMethod.Get && path.EndsWith("/users"))
                return RecordingHttpHandler.Raw(
                    $$"""[{"id":"{{CreatedUserId}}","username":"newstaff"}]""");

            if (method == HttpMethod.Get && path.EndsWith("/groups"))
                // Keycloak 26 真实形态：只给 subGroupCount，子组不内联
                return RecordingHttpHandler.Raw(
                    """[{ "id":"g-tenants","name":"tenants","path":"/tenants","subGroupCount":2,"subGroups":[] }]""");

            if (method == HttpMethod.Get && path.EndsWith("/children"))
                return RecordingHttpHandler.Raw("""
                [
                  { "id":"g-sh","name":"t_shanghai_001","path":"/tenants/t_shanghai_001","subGroupCount":0,"subGroups":[] },
                  { "id":"g-bj","name":"t_beijing_002","path":"/tenants/t_beijing_002","subGroupCount":0,"subGroups":[] }
                ]
                """);

            if (method == HttpMethod.Put && path.Contains("/groups/"))
                return new HttpResponseMessage(HttpStatusCode.NoContent);

            if (method == HttpMethod.Delete && path.Contains("/groups/"))
                return new HttpResponseMessage(HttpStatusCode.NoContent);

            return new HttpResponseMessage(HttpStatusCode.OK);
        };
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 打开 Keycloak 同步并指向假端点
        builder.UseSetting("Keycloak:SyncEnabled", "true");
        builder.UseSetting("Keycloak:BaseUrl", "http://fake-kc");
        builder.UseSetting("Keycloak:Realm", "octopus");
        builder.UseSetting("Keycloak:BotClientId", "retail-invite-bot");
        builder.UseSetting("Keycloak:BotClientSecret", "secret");

        builder.ConfigureServices(services =>
        {
            var dbOpt = services.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<RetailDbContext>));
            if (dbOpt != null) services.Remove(dbOpt);
            services.AddDbContext<RetailDbContext>(opt => opt.UseSqlite(_connection));

            services.AddSingleton<TimeProvider>(Clock);

            // 用假 IHttpClientFactory 接管 Keycloak Admin 调用
            var f = services.SingleOrDefault(s => s.ServiceType == typeof(IHttpClientFactory));
            if (f != null) services.Remove(f);
            services.AddSingleton<IHttpClientFactory>(new RecordingHttpClientFactory(Keycloak));

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
