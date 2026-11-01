using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 密码相关安全回归。
///
/// 真实事故：`PUT /api/system/user/resetPwd` 对 NewPassword **没有任何校验**，
/// 传入空字符串时会把 BCrypt("") 写入库并返回「密码重置成功」；
/// 而登录接口也没有挡空密码，于是：
///
///     POST /api/account/login {"userName":"zhangsan","password":""}
///     → HTTP 200 + Set-Cookie: umc_session=...     ← 空密码直接登录成功
///
/// 这是一个认证绕过（任何知道用户名的人都能登录）。以下用例锁死这两处。
/// </summary>
public class PasswordSecurityTests : IClassFixture<TestWebApplicationFactory>
{
    private const long TargetUserId = 6;      // zhaoliu（启用状态）
    private const string TargetUserName = "zhaoliu";

    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public PasswordSecurityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> f)
    {
        using var scope = _factory.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private Task<string> HashOfAsync() => WithDbAsync(async db =>
        (await db.Users.SingleAsync(u => u.UserId == TargetUserId)).PasswordHash);

    private Task SetHashAsync(string hash) => WithDbAsync(async db =>
    {
        var u = await db.Users.SingleAsync(x => x.UserId == TargetUserId);
        u.PasswordHash = hash;
        await db.SaveChangesAsync();
        return 0;
    });

    private static async Task<ApiResponse<object?>> PostResetAsync(HttpClient c, long userId, string newPassword)
    {
        var resp = await c.PutAsJsonAsync("/api/system/user/resetPwd",
            new { userId, newPassword });
        return (await resp.Content.ReadFromJsonAsync<ApiResponse<object?>>())!;
    }

    private async Task<HttpResponseMessage> LoginAsync(string userName, string password) =>
        await _client.PostAsJsonAsync("/api/account/login", new { userName, password });

    // ── 重置密码的入参校验 ───────────────────────────────

    [Fact]
    public async Task ResetPassword_WithEmptyPassword_IsRejectedAndHashUnchanged()
    {
        var before = await HashOfAsync();

        var r = await PostResetAsync(_client, TargetUserId, "");

        r.Code.Should().Be(400, "空密码必须被拒绝——否则等于给账号开空密码登录后门");
        (await HashOfAsync()).Should().Be(before, "被拒绝的请求不能改动密码哈希");
    }

    [Fact]
    public async Task ResetPassword_WithShortPassword_IsRejected()
    {
        var before = await HashOfAsync();

        var r = await PostResetAsync(_client, TargetUserId, "1234567");

        r.Code.Should().Be(400);
        r.Msg.Should().Contain("8");
        (await HashOfAsync()).Should().Be(before);
    }

    // ── 登录的空密码防护（即使库里已被写坏也要挡住）──────

    [Fact]
    public async Task Login_WithEmptyPassword_IsRejected_EvenIfStoredHashMatchesEmpty()
    {
        // 复刻历史事故留下的坏数据
        await SetHashAsync(BCrypt.Net.BCrypt.HashPassword(""));

        var resp = await LoginAsync(TargetUserName, "");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);   // 统一响应体
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        body!.Code.Should().Be(401, "空密码永远不能登录，哪怕库里的哈希恰好匹配");

        resp.Headers.Contains("Set-Cookie").Should().BeFalse("认证失败不得下发会话 Cookie");
    }

    [Fact]
    public async Task Login_WithWhitespacePassword_IsRejected()
    {
        await SetHashAsync(BCrypt.Net.BCrypt.HashPassword("   "));

        var body = await (await LoginAsync(TargetUserName, "   "))
            .Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();

        body!.Code.Should().Be(401);
    }

    // ── 正常路径不能被误伤 ───────────────────────────────

    [Fact]
    public async Task ResetPassword_WithValidPassword_AllowsLoginWithNewPasswordOnly()
    {
        var original = await HashOfAsync();
        try
        {
            var r = await PostResetAsync(_client, TargetUserId, "NewPass@123");
            r.Code.Should().Be(200, r.Msg);

            var ok = await (await LoginAsync(TargetUserName, "NewPass@123"))
                .Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            ok!.Code.Should().Be(200, "重置后的新密码应可登录");

            var old = await (await LoginAsync(TargetUserName, "Admin@123"))
                .Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            old!.Code.Should().Be(401, "旧密码必须失效");
        }
        finally
        {
            await SetHashAsync(original);
        }
    }
}
