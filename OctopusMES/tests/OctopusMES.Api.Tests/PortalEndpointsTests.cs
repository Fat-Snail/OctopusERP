using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

/// <summary>
/// P16 C 批：供应商门户专属端点（汇总/询价响应/发货登记/对账/资质）契约与隔离测试。
/// </summary>
public class PortalEndpointsTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PortalEndpointsTests(MesTestFactory factory)
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

    private async Task<JsonDocument> SendAs(long sub, HttpMethod method, string url, object body)
    {
        var req = As(sub, method, url);
        req.Content = JsonContent.Create(body);
        var resp = await _client.SendAsync(req);
        return await resp.Content.ReadFromJsonAsync<JsonDocument>(_json) ?? throw new InvalidOperationException("empty response");
    }

    [Fact]
    public async Task Summary_BoundSupplier_ReturnsAggregates()
    {
        var doc = await GetAs(2, "/api/portal/summary");
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("pendingQuotes").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("activeOrders").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("outstandingAmount").GetDecimal().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Summary_UnboundUser_Returns403()
    {
        var doc = await GetAs(1, "/api/portal/summary");
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(403);
    }

    [Fact]
    public async Task Quotes_BoundSupplier_OnlyOwn()
    {
        var boundId = (await GetAs(2, "/api/portal/me")).RootElement.GetProperty("data").GetProperty("supplierId").GetInt64();
        var doc = await GetAs(2, "/api/portal/quotes");
        var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        rows.Should().NotBeEmpty();
        // 每条报价的 code 都带本供应商标记 RFQ-P{id}-
        rows.Should().OnlyContain(q => q.GetProperty("code").GetString()!.StartsWith($"RFQ-P{boundId}-"));
    }

    [Fact]
    public async Task SubmitQuote_OwnQuote_Succeeds_ButCrossSupplier_404()
    {
        var mine = await GetAs(2, "/api/portal/quotes");
        var quoteId = long.Parse(mine.RootElement.GetProperty("data").EnumerateArray().First().GetProperty("id").GetString()!);

        // 供应商3 试图报价供应商2 的询价 → 404
        var cross = await SendAs(3, HttpMethod.Post, $"/api/portal/quotes/{quoteId}/submit",
            new { unitPrice = 1.0m, deliveryDays = 5, remark = "hack" });
        cross.RootElement.GetProperty("code").GetInt32().Should().Be(404);

        // 本人报价成功
        var ok = await SendAs(2, HttpMethod.Post, $"/api/portal/quotes/{quoteId}/submit",
            new { unitPrice = 2.5m, deliveryDays = 7, remark = "含税" });
        ok.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        ok.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("responded");
    }

    [Fact]
    public async Task Shipment_CrossSupplierOrder_404_AndOwnSucceeds()
    {
        // 供应商2（sub=3）的采购单 PO-2024-0002 (id=2)
        var cross = await SendAs(2, HttpMethod.Post, "/api/portal/shipments",
            new { orderId = 2L, trackingNo = "X", carrier = "顺丰" });
        cross.RootElement.GetProperty("code").GetInt32().Should().Be(404);

        var ok = await SendAs(3, HttpMethod.Post, "/api/portal/shipments",
            new { orderId = 2L, trackingNo = "SF123", carrier = "顺丰" });
        ok.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        ok.RootElement.GetProperty("data").GetProperty("trackingNo").GetString().Should().Be("SF123");

        // 供应商3 能读到，供应商2 读不到
        var mine = await GetAs(3, "/api/portal/shipments");
        mine.RootElement.GetProperty("data").EnumerateArray()
            .Any(s => s.GetProperty("trackingNo").GetString() == "SF123").Should().BeTrue();
        var other = await GetAs(2, "/api/portal/shipments");
        other.RootElement.GetProperty("data").EnumerateArray()
            .Any(s => s.GetProperty("trackingNo").GetString() == "SF123").Should().BeFalse();
    }

    [Fact]
    public async Task StatementSummary_ReconcilesNumbers()
    {
        var doc = await GetAs(2, "/api/portal/statement/summary");
        var data = doc.RootElement.GetProperty("data");
        var payable = data.GetProperty("payable").GetDecimal();
        var settled = data.GetProperty("settled").GetDecimal();
        var outstanding = data.GetProperty("outstanding").GetDecimal();
        (payable - settled).Should().Be(outstanding);
    }

    [Fact]
    public async Task Qualifications_ScopedAndUpload()
    {
        var mine = await GetAs(2, "/api/portal/qualifications");
        mine.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        var title = $"测试资质-{Guid.NewGuid():N}";
        var up = await SendAs(2, HttpMethod.Post, "/api/portal/qualifications",
            new { title, category = "测试", fileName = "t.pdf", fileSize = "1 MB" });
        up.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        up.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("pending");

        // 供应商3 看不到供应商2 新传的资质
        var other = await GetAs(3, "/api/portal/qualifications");
        other.RootElement.GetProperty("data").EnumerateArray()
            .Any(q => q.GetProperty("title").GetString() == title).Should().BeFalse();
    }
}
