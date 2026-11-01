using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OctopusUMC.Api.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// P8 — UMC 菜单树分布式缓存测试
/// 验证：
/// - GET /api/system/menu/tree 第一次请求落库并写入缓存
/// - 第二次请求命中缓存（cache key 存在）
/// - 新增/修改/删除菜单后缓存失效
/// </summary>
public class MenuTreeCacheTests : IClassFixture<TestWebApplicationFactory>
{
    private const string CacheKey = "umc:menu:tree";
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MenuTreeCacheTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private IDistributedCache GetCache() =>
        _factory.Services.GetRequiredService<IDistributedCache>();

    [Fact]
    public async Task GetTree_FirstCallPopulatesCache_SecondCallHits()
    {
        var cache = GetCache();
        // 清空缓存以排除其他测试污染
        await cache.RemoveAsync(CacheKey);

        var r1 = await _client.GetAsync("/api/system/menu/tree");
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        var r1Json = await r1.Content.ReadAsStringAsync();

        // 缓存应已写入
        var cached = await cache.GetStringAsync(CacheKey);
        cached.Should().NotBeNullOrEmpty();

        // 第二次请求结果一致
        var r2 = await _client.GetAsync("/api/system/menu/tree");
        r2.StatusCode.Should().Be(HttpStatusCode.OK);
        var r2Json = await r2.Content.ReadAsStringAsync();
        r2Json.Should().Be(r1Json);
    }

    [Fact]
    public async Task CreateMenu_InvalidatesCache()
    {
        var cache = GetCache();
        // 先 warmup 写入缓存
        await _client.GetAsync("/api/system/menu/tree");
        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();

        // 新增一个菜单
        var resp = await _client.PostAsJsonAsync("/api/system/menu", new
        {
            ParentId = 0,
            MenuName = "缓存失效测试菜单",
            MenuType = "M",
            Path = "/cache-test",
            Component = null as string,
            Permission = null as string,
            Icon = null as string,
            OrderNum = 999,
            Status = 1,
            IsCache = false,
            IsFrame = true,
            Visible = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("code").GetInt32().Should().Be(200);

        // 缓存应被清空
        (await cache.GetStringAsync(CacheKey)).Should().BeNull();

        // 再请求 tree → 应包含新菜单（且重新写入缓存）
        var treeResp = await _client.GetAsync("/api/system/menu/tree");
        var treeJson = await treeResp.Content.ReadAsStringAsync();
        treeJson.Should().Contain("缓存失效测试菜单");
        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeleteMenu_InvalidatesCache()
    {
        var cache = GetCache();

        // 先创建一个可删除的菜单
        var createResp = await _client.PostAsJsonAsync("/api/system/menu", new
        {
            ParentId = 0,
            MenuName = "待删除缓存测试菜单",
            MenuType = "M",
            Path = "/cache-delete-test",
            OrderNum = 998,
            Status = 1,
            IsCache = false,
            IsFrame = true,
            Visible = true,
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var menuId = createDoc.GetProperty("data").GetProperty("menuId").GetInt64();

        // warmup 缓存
        await _client.GetAsync("/api/system/menu/tree");
        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();

        // 删除菜单
        var delResp = await _client.DeleteAsync($"/api/system/menu/{menuId}");
        delResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 缓存应被清空
        (await cache.GetStringAsync(CacheKey)).Should().BeNull();
    }
}
