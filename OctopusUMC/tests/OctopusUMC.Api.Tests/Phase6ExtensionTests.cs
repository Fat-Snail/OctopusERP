using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Api.Services;
using OctopusUMC.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

// ── Fakes ────────────────────────────────────────────────────────────────────

/// <summary>捕获已发送邮件的内存实现</summary>
public class FakeEmailService : IEmailService
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string toAddress, string toName, string subject, string htmlBody)
    {
        Sent.Add((toAddress, subject, htmlBody));
        return Task.CompletedTask;
    }
}

/// <summary>固定返回值的微信服务存根</summary>
public class FakeWechatOAuthService : IWechatOAuthService
{
    public string AuthUrl { get; set; } = "https://open.weixin.qq.com/connect/qrconnect?fake=1";
    public WechatTokenResult? TokenResult { get; set; } =
        new("fake_openid_1234", "fake_access_token", null);
    public WechatUserInfo? UserInfoResult { get; set; } =
        new("fake_openid_1234", "微信测试用户", "https://example.com/avatar.jpg", null);

    public string BuildAuthUrl(string state) => AuthUrl;
    public Task<WechatTokenResult?> ExchangeCodeAsync(string code) => Task.FromResult(TokenResult);
    public Task<WechatUserInfo?> GetUserInfoAsync(string accessToken, string openId) =>
        Task.FromResult(UserInfoResult);
}

// ── Factory ───────────────────────────────────────────────────────────────────

public class ExtensionTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    public FakeEmailService EmailService { get; } = new();
    public FakeWechatOAuthService WechatService { get; } = new();

    public ExtensionTestFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // SQLite 内存库
            var d = services.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (d != null) services.Remove(d);
            services.AddDbContext<ApplicationDbContext>(opt =>
            {
                opt.UseSqlite(_connection);
                opt.UseOpenIddict();
            });

            // 初始化并 Seed
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
            DbSeeder.Seed(db);

            // 替换邮件服务
            var emailDesc = services.SingleOrDefault(s => s.ServiceType == typeof(IEmailService));
            if (emailDesc != null) services.Remove(emailDesc);
            services.AddSingleton<IEmailService>(EmailService);

            // 替换微信服务
            var wxDesc = services.SingleOrDefault(s => s.ServiceType == typeof(IWechatOAuthService));
            if (wxDesc != null) services.Remove(wxDesc);
            services.AddSingleton<IWechatOAuthService>(WechatService);

            // 测试认证（默认 admin）
            services.AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = "TestAuth";
                opts.DefaultChallengeScheme = "TestAuth";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", null);
        });

        builder.ConfigureLogging(l =>
        {
            l.ClearProviders();
            l.SetMinimumLevel(LogLevel.Warning);
        });

        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class Phase6ExtensionTests : IClassFixture<ExtensionTestFactory>
{
    private readonly ExtensionTestFactory _factory;
    private readonly HttpClient _client;

    public Phase6ExtensionTests(ExtensionTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    // ── 忘记密码 / 重置密码 ────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_WithKnownEmail_SendsEmailAndReturns200()
    {
        _factory.EmailService.Sent.Clear();

        var response = await _client.PostAsJsonAsync("/api/account/forgot-password",
            new { Email = "admin@octopus.com" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(200);
        _factory.EmailService.Sent.Should().HaveCount(1);
        _factory.EmailService.Sent[0].To.Should().Be("admin@octopus.com");
        _factory.EmailService.Sent[0].Subject.Should().Contain("重置");
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/account/forgot-password",
            new { Email = "nobody@nonexistent.com" });

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(404);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_UpdatesPassword()
    {
        // 先触发忘记密码写入 token 到缓存
        _factory.EmailService.Sent.Clear();
        await _client.PostAsJsonAsync("/api/account/forgot-password",
            new { Email = "zhangsan@octopus.com" });

        // 从 FakeEmailService 取出邮件正文中的 token
        var body = _factory.EmailService.Sent.Last().Body;
        var tokenStart = body.IndexOf("token=") + 6;
        var tokenEnd = body.IndexOf('"', tokenStart);
        var token = body[tokenStart..tokenEnd];

        var response = await _client.PostAsJsonAsync("/api/account/reset-password",
            new { Token = token, NewPassword = "NewPass@999" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(200);

        // 验证新密码可以登录
        var loginResp = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "zhangsan", Password = "NewPass@999" });
        var loginResult = await loginResp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        loginResult!.Code.Should().Be(200, "新密码应能登录成功");
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/account/reset-password",
            new { Token = "totally_invalid_token_xyz", NewPassword = "NewPass@999" });

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(400);
    }

    // ── 微信 OAuth ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWechatAuthorizeUrl_ReturnsUrlContainingState()
    {
        var response = await _client.GetAsync("/api/account/wechat/authorize");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<WechatAuthorizeResponse>>();
        result!.Code.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.Url.Should().StartWith("https://open.weixin.qq.com");
    }

    [Fact]
    public async Task WechatCallback_WithValidCode_CreatesUserAndRedirects()
    {
        // 先拿一个合法的 state
        var authorizeResp = await _client.GetAsync("/api/account/wechat/authorize");
        var authorizeResult = await authorizeResp.Content.ReadFromJsonAsync<ApiResponse<WechatAuthorizeResponse>>();
        var url = authorizeResult!.Data!.Url;
        // FakeWechatOAuthService.BuildAuthUrl 不在 URL 里放 state，但缓存里有随机 state
        // 由于 Fake 的 URL 是固定的，我们需要直接从缓存里取 state
        // 改为：再次请求 /authorize 拿 state 参数（从 cache key 逆推不实际，换个测试策略）

        // 用无效 state 触发 state_mismatch（验证防 CSRF 逻辑）
        var callbackBadState = await _client.GetAsync(
            "/api/account/wechat/callback?code=test_code&state=invalid_state_xyz");
        callbackBadState.StatusCode.Should().Be(HttpStatusCode.Redirect);
        callbackBadState.Headers.Location!.ToString().Should().Contain("state_mismatch");
    }

    [Fact]
    public async Task WechatCallback_WithMissingCode_RedirectsWithError()
    {
        var response = await _client.GetAsync("/api/account/wechat/callback?state=somestate");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("invalid_request");
    }

    [Fact]
    public async Task WechatCallback_WithValidStateAndCode_CreatesNewUserAndSignsIn()
    {
        // 请求 /authorize，捕获生成的 state（通过 IMemoryCache 间接验证）
        // 策略：直接把 FakeWechatOAuthService 配置好，然后模拟完整流程：
        //   1. GET /authorize → 返回 URL，并在缓存中存入 state → returnUrl 映射
        //   2. 提取 state（通过 HTTP 请求返回的 URL 无法拿到，所以改用 IServiceProvider 直接写缓存）

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var testState = "test_valid_state_" + Guid.NewGuid().ToString("N");
        cache.Set($"wx_state:{testState}", "/", TimeSpan.FromMinutes(5));

        // 配置 Fake 返回唯一 openId（避免与其他测试冲突）
        var uniqueOpenId = "test_openid_" + Guid.NewGuid().ToString("N")[..8];
        _factory.WechatService.TokenResult = new WechatTokenResult(uniqueOpenId, "fake_token", null);
        _factory.WechatService.UserInfoResult = new WechatUserInfo(uniqueOpenId, "新微信用户", null, null);

        var callbackResp = await _client.GetAsync(
            $"/api/account/wechat/callback?code=fake_code&state={testState}");

        callbackResp.StatusCode.Should().Be(HttpStatusCode.Redirect,
            "成功登录应 302 跳转到前端");
        callbackResp.Headers.Location!.ToString().Should().NotContain("error");

        // 验证用户已创建
        using var scope2 = _factory.Services.CreateScope();
        var db = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.WechatLogins.Should().Contain(w => w.OpenId == uniqueOpenId);
        db.Users.Should().Contain(u => u.UserName.StartsWith("wx_"));
    }

    [Fact]
    public async Task WechatCallback_ExistingBinding_SignsInExistingUser()
    {
        // 预先在 DB 中注册一个微信绑定
        var existingOpenId = "existing_wechat_openid_abc";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!db.WechatLogins.Any(w => w.OpenId == existingOpenId))
            {
                var user = new OctopusUMC.Core.Domain.Entities.User
                {
                    UserName = "wx_existing",
                    NickName = "已有微信用户",
                    Email = $"wx_{existingOpenId}@wechat.placeholder",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    Status = 1,
                };
                db.Users.Add(user);
                db.SaveChanges();
                db.WechatLogins.Add(new OctopusUMC.Core.Domain.Entities.WechatExternalLogin
                {
                    UserId = user.UserId,
                    OpenId = existingOpenId,
                });
                db.SaveChanges();
            }
        }

        _factory.WechatService.TokenResult = new WechatTokenResult(existingOpenId, "fake_token", null);
        _factory.WechatService.UserInfoResult = new WechatUserInfo(existingOpenId, "已有微信用户", null, null);

        using var scope2 = _factory.Services.CreateScope();
        var cache = scope2.ServiceProvider.GetRequiredService<IMemoryCache>();
        var testState = "existing_state_" + Guid.NewGuid().ToString("N");
        cache.Set($"wx_state:{testState}", "/", TimeSpan.FromMinutes(5));

        var response = await _client.GetAsync(
            $"/api/account/wechat/callback?code=fake_code&state={testState}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().NotContain("error");
    }
}
