using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusPLM.Api.Tests;

/// <summary>
/// P1 — PLM 类目树缓存测试（plm:category:tree，TTL 5 分钟）
/// 验证：GET /api/category/tree 缓存读写 + Create/Update 主动失效
/// </summary>
public class CategoryCacheTests : IClassFixture<PlmTestFactory>
{
    private const string CacheKey = "plm:category:tree";
    private readonly PlmTestFactory _factory;
    private readonly HttpClient _client;

    public CategoryCacheTests(PlmTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "1"); // PLM 测试认证：admin
    }

    private IDistributedCache GetCache() =>
        _factory.Services.GetRequiredService<IDistributedCache>();

    [Fact]
    public async Task GetTree_FirstCallPopulatesCache_SecondCallHits()
    {
        var cache = GetCache();
        await cache.RemoveAsync(CacheKey);

        var r1 = await _client.GetAsync("/api/category/tree");
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        var r1Json = await r1.Content.ReadAsStringAsync();

        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();

        var r2 = await _client.GetAsync("/api/category/tree");
        (await r2.Content.ReadAsStringAsync()).Should().Be(r1Json);
    }

    [Fact]
    public async Task CreateCategory_InvalidatesCache()
    {
        var cache = GetCache();
        await _client.GetAsync("/api/category/tree"); // warmup
        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();

        var createResp = await _client.PostAsJsonAsync("/api/category", new
        {
            name = "缓存失效测试类目",
            parentId = (long?)null,
            orderNum = 999,
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 缓存应被清空
        (await cache.GetStringAsync(CacheKey)).Should().BeNull();

        // 再请求 tree → 包含新类目且缓存重建
        var treeJson = await (await _client.GetAsync("/api/category/tree")).Content.ReadAsStringAsync();
        treeJson.Should().Contain("缓存失效测试类目");
        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateCategory_InvalidatesCache()
    {
        var cache = GetCache();
        // 创建一个可更新的类目
        var createResp = await _client.PostAsJsonAsync("/api/category", new
        {
            name = "待更新缓存测试类目",
            parentId = (long?)null,
            orderNum = 998,
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>();
        var catId = createDoc!.RootElement.GetProperty("data").GetProperty("categoryId").GetInt64();

        await _client.GetAsync("/api/category/tree"); // warmup
        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();

        var updateResp = await _client.PutAsJsonAsync($"/api/category/{catId}", new
        {
            name = "更新后缓存测试类目",
            orderNum = 997,
        });
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 缓存应被清空
        (await cache.GetStringAsync(CacheKey)).Should().BeNull();

        // 再请求 tree → 反映新名称
        var treeJson = await (await _client.GetAsync("/api/category/tree")).Content.ReadAsStringAsync();
        treeJson.Should().Contain("更新后缓存测试类目");
    }
}
