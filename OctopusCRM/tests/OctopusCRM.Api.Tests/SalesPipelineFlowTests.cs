using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusCRM.Api.Tests;

public class SalesPipelineFlowTests : IClassFixture<CrmTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private const string SharedSecret = "crm-shared-secret-dev";

    public SalesPipelineFlowTests(CrmTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private static string ComputeHmac(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLower();
    }

    private async Task<long> GetHuaweiCustomerIdAsync()
    {
        var resp = await _client.GetAsync("/api/customer?keyword=华为");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        return doc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("customerId").GetInt64();
    }

    [Fact]
    public async Task GetInquiryList_ReturnsSeedInquiries()
    {
        var resp = await _client.GetAsync("/api/inquiry");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CreateInquiry_WithItems_ReturnsInquiryCode()
    {
        var customerId = await GetHuaweiCustomerIdAsync();

        var resp = await _client.PostAsJsonAsync("/api/inquiry", new
        {
            customerId,
            title = "测试询盘",
            description = "集成测试询盘",
            expectedDelivery = DateTime.UtcNow.AddDays(30),
            assignedTo = 2,
            assignedToName = "张三"
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("inquiryCode").GetString().Should().StartWith("INQ-");
    }

    [Fact]
    public async Task AddInquiryItem_IncreaseItemCount()
    {
        var customerId = await GetHuaweiCustomerIdAsync();

        // 创建询盘
        var inqResp = await _client.PostAsJsonAsync("/api/inquiry", new
        {
            customerId,
            title = "含明细询盘",
            assignedTo = 2
        });
        var inqDoc = await inqResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var inqId = inqDoc!.RootElement.GetProperty("data").GetProperty("inquiryId").GetInt64();

        // 添加明细
        var itemResp = await _client.PostAsJsonAsync($"/api/inquiry/{inqId}/item", new
        {
            productName = "测试产品",
            quantity = 100m,
            unit = "个",
            spec = "标准规格"
        });
        var itemDoc = await itemResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        itemDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        // 查询验证
        var getResp = await _client.GetAsync($"/api/inquiry/{inqId}");
        var getDoc = await getResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        getDoc!.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task CreateQuote_FromInquiry_HasDraftStatus()
    {
        var customerId = await GetHuaweiCustomerIdAsync();

        // 先建询盘
        var inqResp = await _client.PostAsJsonAsync("/api/inquiry", new
        {
            customerId, title = "报价测试询盘", assignedTo = 2
        });
        var inqId = (await inqResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("inquiryId").GetInt64();

        // 创建报价单
        var quoteResp = await _client.PostAsJsonAsync("/api/quote", new
        {
            inquiryId = inqId,
            customerId,
            currency = "CNY",
            validUntil = DateTime.UtcNow.AddDays(30)
        });
        var quoteDoc = await quoteResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        quoteDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        quoteDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("draft");
        quoteDoc.RootElement.GetProperty("data").GetProperty("quoteCode").GetString().Should().StartWith("QUO-");
    }

    [Fact]
    public async Task AddQuoteItem_UpdatesTotalAmount()
    {
        var customerId = await GetHuaweiCustomerIdAsync();
        var inqResp = await _client.PostAsJsonAsync("/api/inquiry", new
        {
            customerId, title = "金额测试询盘", assignedTo = 2
        });
        var inqId = (await inqResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("inquiryId").GetInt64();

        var quoteResp = await _client.PostAsJsonAsync("/api/quote", new
        {
            inquiryId = inqId, customerId, currency = "CNY"
        });
        var quoteId = (await quoteResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("quoteId").GetInt64();

        // 添加明细
        await _client.PostAsJsonAsync($"/api/quote/{quoteId}/item", new
        {
            productName = "产品A", quantity = 100m, unitPrice = 50m
        });
        await _client.PostAsJsonAsync($"/api/quote/{quoteId}/item", new
        {
            productName = "产品B", quantity = 200m, unitPrice = 20m
        });

        // 验证总金额 = 100×50 + 200×20 = 9000
        var getResp = await _client.GetAsync($"/api/quote/{quoteId}");
        var getDoc = await getResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        getDoc!.RootElement.GetProperty("data").GetProperty("totalAmount").GetDecimal().Should().Be(9000m);
    }

    [Fact]
    public async Task SubmitQuote_ChangesStatusToPendingApproval()
    {
        var customerId = await GetHuaweiCustomerIdAsync();
        var inqResp = await _client.PostAsJsonAsync("/api/inquiry", new
        {
            customerId, title = "提交测试询盘", assignedTo = 2
        });
        var inqId = (await inqResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("inquiryId").GetInt64();

        var quoteResp = await _client.PostAsJsonAsync("/api/quote", new
        {
            inquiryId = inqId, customerId, currency = "CNY"
        });
        var quoteId = (await quoteResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("quoteId").GetInt64();

        var submitResp = await _client.PutAsJsonAsync($"/api/quote/{quoteId}/submit", new { });
        var submitDoc = await submitResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        submitDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var getDoc = (await (await _client.GetAsync($"/api/quote/{quoteId}"))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        getDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("pending_approval");
    }

    [Fact]
    public async Task ApprovalCallback_Approved_SetsQuoteToApproved()
    {
        var customerId = await GetHuaweiCustomerIdAsync();
        var inqResp = await _client.PostAsJsonAsync("/api/inquiry", new
        {
            customerId, title = "审批回调测试询盘", assignedTo = 2
        });
        var inqId = (await inqResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("inquiryId").GetInt64();

        var quoteResp = await _client.PostAsJsonAsync("/api/quote", new
        {
            inquiryId = inqId, customerId, currency = "CNY"
        });
        var quoteId = (await quoteResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("quoteId").GetInt64();

        var submitDoc = (await (await _client.PutAsJsonAsync($"/api/quote/{quoteId}/submit", new { }))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        var approvalId = submitDoc.RootElement.GetProperty("data").GetProperty("oaApprovalId").GetInt64();

        // 模拟 OA 审批通过回调（精确 JSON 字符串保证 HMAC 计算一致）
        var body = $"{{\"oaApprovalId\":{approvalId},\"approved\":true,\"comment\":\"通过\"}}";
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/approval-callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Crm-Signature", ComputeHmac(body));
        var cbResp = await _client.SendAsync(req);
        cbResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var getDoc = (await (await _client.GetAsync($"/api/quote/{quoteId}"))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        getDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("approved");
    }

    [Fact]
    public async Task ApprovalCallback_Rejected_SetsQuoteToRejected()
    {
        var customerId = await GetHuaweiCustomerIdAsync();
        var inqResp = await _client.PostAsJsonAsync("/api/inquiry", new
        {
            customerId, title = "驳回测试询盘", assignedTo = 2
        });
        var inqId = (await inqResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("inquiryId").GetInt64();

        var quoteResp = await _client.PostAsJsonAsync("/api/quote", new
        {
            inquiryId = inqId, customerId, currency = "CNY"
        });
        var quoteId = (await quoteResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("quoteId").GetInt64();

        var submitDoc = (await (await _client.PutAsJsonAsync($"/api/quote/{quoteId}/submit", new { }))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        var approvalId = submitDoc.RootElement.GetProperty("data").GetProperty("oaApprovalId").GetInt64();

        var body = $"{{\"oaApprovalId\":{approvalId},\"approved\":false,\"comment\":\"价格偏高\"}}";
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/approval-callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Crm-Signature", ComputeHmac(body));
        await _client.SendAsync(req);

        var getDoc = (await (await _client.GetAsync($"/api/quote/{quoteId}"))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        getDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("rejected");
    }

    [Fact]
    public async Task ConfirmQuote_AfterApproval_ChangesStatusToConfirmed()
    {
        var customerId = await GetHuaweiCustomerIdAsync();
        var inqResp = await _client.PostAsJsonAsync("/api/inquiry", new
        {
            customerId, title = "确认测试询盘", assignedTo = 2
        });
        var inqId = (await inqResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("inquiryId").GetInt64();

        var quoteResp = await _client.PostAsJsonAsync("/api/quote", new
        {
            inquiryId = inqId, customerId, currency = "CNY"
        });
        var quoteId = (await quoteResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("quoteId").GetInt64();

        var submitDoc = (await (await _client.PutAsJsonAsync($"/api/quote/{quoteId}/submit", new { }))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        var approvalId = submitDoc.RootElement.GetProperty("data").GetProperty("oaApprovalId").GetInt64();

        // 审批通过
        var body = $"{{\"oaApprovalId\":{approvalId},\"approved\":true,\"comment\":\"ok\"}}";
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/approval-callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Crm-Signature", ComputeHmac(body));
        await _client.SendAsync(req);

        // 客户确认
        var confirmResp = await _client.PutAsJsonAsync($"/api/quote/{quoteId}/confirm", new { });
        var confirmDoc = await confirmResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        confirmDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var getDoc = (await (await _client.GetAsync($"/api/quote/{quoteId}"))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        getDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("confirmed");
    }

    [Fact]
    public async Task ApprovalCallback_WithInvalidSignature_Returns401()
    {
        var body = JsonSerializer.Serialize(new { oaApprovalId = 1, approved = true, comment = "" });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/approval-callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Crm-Signature", "sha256=invalidsignature");
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
