using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusCRM.Api.Tests;

/// <summary>
/// P16 门户"仅本人"隔离测试：验证 sub→客户身份绑定后行级过滤生效，
/// 且内部员工（未绑定）行为不变。
/// 身份通过 X-Test-UserId header 注入（见 CrmTestFactory）。
/// 种子绑定：CUS-20260101-001→UMC 2，CUS-20260101-002→UMC 3。
/// </summary>
public class PortalIsolationTests : IClassFixture<CrmTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PortalIsolationTests(CrmTestFactory factory)
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
    public async Task PortalMe_BoundCustomer_ReturnsBinding()
    {
        var resp = await _client.SendAsync(As(2, HttpMethod.Get, "/api/portal/me"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var data = doc!.RootElement.GetProperty("data");
        data.GetProperty("bound").GetBoolean().Should().BeTrue();
        data.GetProperty("customerId").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PortalMe_UnboundInternalUser_ReturnsNotBound()
    {
        var resp = await _client.SendAsync(As(1, HttpMethod.Get, "/api/portal/me"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("data").GetProperty("bound").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task InquiryList_PortalCustomer_OnlySeesOwnRows()
    {
        // 客户1（sub=2）只能看到自己客户 id 的询盘
        var resp = await _client.SendAsync(As(2, HttpMethod.Get, "/api/inquiry?size=100"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        rows.Should().NotBeEmpty();
        var boundCustomerId = await GetBoundCustomerId(2);
        rows.Should().OnlyContain(r => r.GetProperty("customerId").GetInt64() == boundCustomerId);
    }

    [Fact]
    public async Task InquiryList_InternalUser_SeesAllRows()
    {
        // 内部员工（sub=1，未绑定）保持全量可见
        var resp = await _client.SendAsync(As(1, HttpMethod.Get, "/api/inquiry?size=100"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("customerId").GetInt64()).Distinct().Count().Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task InquiryList_PortalCustomer_CannotOverrideCustomerId()
    {
        // 客户1 试图通过 customerId 参数窥探其他客户：应被强制过滤回本人
        var resp = await _client.SendAsync(As(2, HttpMethod.Get, "/api/inquiry?customerId=999&size=100"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        var boundCustomerId = await GetBoundCustomerId(2);
        rows.Should().OnlyContain(r => r.GetProperty("customerId").GetInt64() == boundCustomerId);
    }

    [Fact]
    public async Task InquiryGetById_CrossCustomer_Returns404()
    {
        // 取一条属于其他客户的询盘 id
        var all = await _client.SendAsync(As(1, HttpMethod.Get, "/api/inquiry?size=100"));
        var doc = await all.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var boundCustomerId = await GetBoundCustomerId(2);
        var foreign = doc!.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("customerId").GetInt64() != boundCustomerId);
        var foreignId = foreign.GetProperty("inquiryId").GetInt64();

        var resp = await _client.SendAsync(As(2, HttpMethod.Get, $"/api/inquiry/{foreignId}"));
        var detail = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        detail!.RootElement.GetProperty("code").GetInt32().Should().Be(404);
    }

    private async Task<long> GetBoundCustomerId(long sub)
    {
        var resp = await _client.SendAsync(As(sub, HttpMethod.Get, "/api/portal/me"));
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        return doc!.RootElement.GetProperty("data").GetProperty("customerId").GetInt64();
    }
}
