using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>
/// P1 — OA 部门树缓存测试（oa:dept:tree，TTL 5 分钟）
/// 验证：GET /api/contact/dept/tree 缓存读写 + UMC 部门同步主动失效
/// </summary>
public class DeptTreeCacheTests : IClassFixture<OATestFactory>
{
    private const string CacheKey = "oa:dept:tree";
    private const string SharedSecret = "octopus-sync-secret-key-2026";
    private readonly OATestFactory _factory;
    private readonly HttpClient _client;

    public DeptTreeCacheTests(OATestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private IDistributedCache GetCache() =>
        _factory.Services.GetRequiredService<IDistributedCache>();

    private static string ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private async Task<HttpResponseMessage> SyncDeptAsync(string action, long deptId, string name)
    {
        var json = JsonSerializer.Serialize(new
        {
            Action = action,
            DeptId = deptId,
            ParentId = 0L,
            DeptName = name,
            OrderNum = 999,
            Status = 1,
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/contact/dept/sync")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(json)}");
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task GetDeptTree_FirstCallPopulatesCache_SecondCallHits()
    {
        var cache = GetCache();
        await cache.RemoveAsync(CacheKey);

        var r1 = await _client.GetAsync("/api/contact/dept/tree");
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        var r1Json = await r1.Content.ReadAsStringAsync();

        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();

        var r2 = await _client.GetAsync("/api/contact/dept/tree");
        (await r2.Content.ReadAsStringAsync()).Should().Be(r1Json);
    }

    [Fact]
    public async Task DeptSync_Upsert_InvalidatesCache()
    {
        var cache = GetCache();
        await _client.GetAsync("/api/contact/dept/tree"); // warmup
        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();

        // UMC 侧同步一个新部门（upsert）→ 缓存应被失效
        var resp = await SyncDeptAsync("upsert", 99001, "同步失效测试部门");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        (await cache.GetStringAsync(CacheKey)).Should().BeNull();

        // 再请求 tree → 包含新部门且缓存重建
        var treeJson = await (await _client.GetAsync("/api/contact/dept/tree")).Content.ReadAsStringAsync();
        treeJson.Should().Contain("同步失效测试部门");
        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeptSync_Delete_InvalidatesCache()
    {
        var cache = GetCache();
        // 先同步创建，再 warmup
        (await SyncDeptAsync("upsert", 99002, "待删除同步部门")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        await _client.GetAsync("/api/contact/dept/tree");
        (await cache.GetStringAsync(CacheKey)).Should().NotBeNullOrEmpty();

        // 删除部门 → 缓存失效
        (await SyncDeptAsync("delete", 99002, "")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await cache.GetStringAsync(CacheKey)).Should().BeNull();
    }
}
