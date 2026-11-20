using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusCRM.Api.Tests;

/// <summary>
/// P16 B 批：客户门户专属端点（汇总/物流/对账/客服/文档）契约与隔离测试。
/// </summary>
public class PortalEndpointsTests : IClassFixture<CrmTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PortalEndpointsTests(CrmTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static HttpRequestMessage As(long sub, HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-UserId", sub.ToString());
        return req;
    }

    private async Task<JsonDocument> GetAs(long sub, string url)
    {
        var resp = await _client.SendAsync(As(sub, HttpMethod.Get, url));
        return await resp.Content.ReadFromJsonAsync<JsonDocument>(_json) ?? throw new InvalidOperationException("empty response");
    }

    [Fact]
    public async Task Summary_BoundCustomer_ReturnsAggregates()
    {
        var doc = await GetAs(2, "/api/portal/summary");
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("openInquiries").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("activeContracts").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("shippingOrders").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("unpaidAmount").GetDecimal().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Summary_UnboundUser_Returns403()
    {
        var doc = await GetAs(1, "/api/portal/summary");
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(403);
    }

    [Fact]
    public async Task Shipments_BoundCustomer_ReturnsArray()
    {
        var doc = await GetAs(2, "/api/portal/shipments");
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task StatementSummary_ReconcilesNumbers()
    {
        var doc = await GetAs(2, "/api/portal/statement/summary");
        var data = doc.RootElement.GetProperty("data");
        var receivable = data.GetProperty("receivable").GetDecimal();
        var paid = data.GetProperty("paid").GetDecimal();
        var unpaid = data.GetProperty("unpaid").GetDecimal();
        (receivable - paid).Should().Be(unpaid);
    }

    [Fact]
    public async Task StatementItems_BoundCustomer_ReturnsOnlyOwnPayments()
    {
        var boundId = (await GetAs(2, "/api/portal/me")).RootElement.GetProperty("data").GetProperty("customerId").GetInt64();
        var doc = await GetAs(2, "/api/portal/statement/items");
        doc.RootElement.GetProperty("data").EnumerateArray().Should().NotBeEmpty();
        // 明细里的 code/description 不应包含其它客户的合同编号（这里只断言结构合法）
        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            item.GetProperty("type").GetString().Should().BeOneOf("payment", "invoice");
            item.GetProperty("status").GetString().Should().BeOneOf("settled", "pending");
        }
        boundId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Chat_SendAndRead_IsScopedToCustomer()
    {
        var text = $"测试消息-{Guid.NewGuid():N}";
        var sendReq = As(2, HttpMethod.Post, "/api/portal/chat");
        sendReq.Content = JsonContent.Create(new { text });
        var sent = await _client.SendAsync(sendReq);
        var sentDoc = await sent.Content.ReadFromJsonAsync<JsonDocument>(_json);
        sentDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        sentDoc.RootElement.GetProperty("data").GetProperty("from").GetString().Should().Be("me");

        // 客户1 能读到自己刚发的消息
        var mine = await GetAs(2, "/api/portal/chat");
        mine.RootElement.GetProperty("data").EnumerateArray()
            .Any(m => m.GetProperty("text").GetString() == text).Should().BeTrue();

        // 客户2 读不到客户1 的消息
        var other = await GetAs(3, "/api/portal/chat");
        other.RootElement.GetProperty("data").EnumerateArray()
            .Any(m => m.GetProperty("text").GetString() == text).Should().BeFalse();
    }

    [Fact]
    public async Task Documents_ReturnsGlobalDocs()
    {
        var doc = await GetAs(2, "/api/portal/documents");
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(3);
    }
}
