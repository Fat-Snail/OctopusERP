using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OctopusUMC.Api.Controllers;
using OctopusUMC.Api.DTOs;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// P1 — UMC 用户权限缓存测试（umc:user:perms:{userId}，TTL 5 分钟）
/// 验证 /api/account/me 的缓存读写路径 + 主动失效
/// </summary>
public class UserPermsCacheTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserPermsCacheTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private IDistributedCache GetCache() =>
        _factory.Services.GetRequiredService<IDistributedCache>();

    private async Task<HttpResponseMessage> LoginAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "admin", Password = "Admin@123" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return resp;
    }

    [Fact]
    public async Task Me_FirstCallPopulatesCache_SecondCallHits()
    {
        await LoginAsync();
        var cache = GetCache();

        var r1 = await _client.GetAsync("/api/account/me");
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await r1.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        login!.Data.Should().NotBeNull();
        var userId = login.Data!.UserId;

        var cacheKey = $"umc:user:perms:{userId}";
        // 缓存应已写入
        (await cache.GetStringAsync(cacheKey)).Should().NotBeNullOrEmpty();

        // 第二次请求命中缓存，结果一致
        var r2 = await _client.GetAsync("/api/account/me");
        r2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body2 = await r2.Content.ReadAsStringAsync();
        body2.Should().Contain(login.Data!.UserName);
        // 缓存仍在（未被清除）
        (await cache.GetStringAsync(cacheKey)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvalidateUserPermsCache_RemovesKey_AndMeRebuilds()
    {
        await LoginAsync();
        var cache = GetCache();

        var r1 = await _client.GetAsync("/api/account/me");
        var login = await r1.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var userId = login!.Data!.UserId;
        var cacheKey = $"umc:user:perms:{userId}";
        (await cache.GetStringAsync(cacheKey)).Should().NotBeNullOrEmpty();

        // 主动失效（UserController/RoleController 变更路径调用的同一函数）
        AccountController.InvalidateUserPermsCache(cache, userId);
        (await cache.GetStringAsync(cacheKey)).Should().BeNull();

        // 再次请求 → 重建缓存
        var r2 = await _client.GetAsync("/api/account/me");
        r2.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cache.GetStringAsync(cacheKey)).Should().NotBeNullOrEmpty();
    }
}
