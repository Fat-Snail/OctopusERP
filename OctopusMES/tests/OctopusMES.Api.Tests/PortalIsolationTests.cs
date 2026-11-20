using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

/// <summary>
/// P16 供应商门户"仅本人"隔离测试。
/// 种子绑定：SUP-001→UMC 2，SUP-002→UMC 3。
/// </summary>
public class PortalIsolationTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PortalIsolationTests(MesTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static HttpRequestMessage As(long sub, HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-UserId", sub.ToString());
        return req;
    }

    [Fact]
    public async Task PortalMe_BoundSupplier_ReturnsBinding()
    {
        var resp = await _client.SendAsync(As(2, HttpMethod.Get, "/api/portal/me"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var data = doc!.RootElement.GetProperty("data");
        data.GetProperty("bound").GetBoolean().Should().BeTrue();
        data.GetProperty("supplierId").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PortalMe_UnboundInternalUser_ReturnsNotBound()
    {
        var resp = await _client.SendAsync(As(1, HttpMethod.Get, "/api/portal/me"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("data").GetProperty("bound").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task PurchaseList_PortalSupplier_OnlySeesOwnRows()
    {
        var resp = await _client.SendAsync(As(2, HttpMethod.Get, "/api/purchase?pageSize=100"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        rows.Should().NotBeEmpty();
        var boundId = await GetBoundSupplierId(2);
        rows.Should().OnlyContain(r => r.GetProperty("supplierId").GetInt64() == boundId);
    }

    [Fact]
    public async Task PurchaseList_InternalUser_SeesAllRows()
    {
        var resp = await _client.SendAsync(As(1, HttpMethod.Get, "/api/purchase?pageSize=100"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("supplierId").GetInt64()).Distinct().Count().Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task PurchaseGetById_CrossSupplier_Returns404()
    {
        var all = await _client.SendAsync(As(1, HttpMethod.Get, "/api/purchase?pageSize=100"));
        var doc = await all.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var boundId = await GetBoundSupplierId(2);
        var foreign = doc!.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("supplierId").GetInt64() != boundId);
        var foreignId = foreign.GetProperty("purchaseId").GetInt64();

        var resp = await _client.SendAsync(As(2, HttpMethod.Get, $"/api/purchase/{foreignId}"));
        var detail = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        detail!.RootElement.GetProperty("code").GetInt32().Should().Be(404);
    }

    private async Task<long> GetBoundSupplierId(long sub)
    {
        var resp = await _client.SendAsync(As(sub, HttpMethod.Get, "/api/portal/me"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        return doc!.RootElement.GetProperty("data").GetProperty("supplierId").GetInt64();
    }
}
