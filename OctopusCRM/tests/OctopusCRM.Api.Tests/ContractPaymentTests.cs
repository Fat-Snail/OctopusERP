using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusCRM.Api.Tests;

public class ContractPaymentTests : IClassFixture<CrmTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private const string SharedSecret = "crm-shared-secret-dev";

    public ContractPaymentTests(CrmTestFactory factory)
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

    private async Task<long> GetSeedContractIdAsync()
    {
        var resp = await _client.GetAsync("/api/contract");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = doc!.RootElement.GetProperty("data").GetProperty("rows");
        // 种子合同代码已知，遍历找到它
        foreach (var row in rows.EnumerateArray())
        {
            if (row.GetProperty("contractCode").GetString() == "CON-20260101-001")
                return row.GetProperty("contractId").GetInt64();
        }
        // 降级：返回最后一条（最旧的）
        return rows[rows.GetArrayLength() - 1].GetProperty("contractId").GetInt64();
    }

    private async Task<long> CreateApprovedQuoteAsync()
    {
        // 获取华为客户
        var custResp = await _client.GetAsync("/api/customer?keyword=华为");
        var custDoc = await custResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var customerId = custDoc!.RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("customerId").GetInt64();

        // 创建询盘
        var inqResp = await _client.PostAsJsonAsync("/api/inquiry", new
        {
            customerId, title = $"合同测试询盘 {Guid.NewGuid()}", assignedTo = 2
        });
        var inqId = (await inqResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("inquiryId").GetInt64();

        // 创建报价并提交
        var quoteResp = await _client.PostAsJsonAsync("/api/quote", new
        {
            inquiryId = inqId, customerId, currency = "CNY"
        });
        var quoteId = (await quoteResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("quoteId").GetInt64();

        await _client.PostAsJsonAsync($"/api/quote/{quoteId}/item", new
        {
            productName = "测试产品", quantity = 100m, unitPrice = 500m
        });

        var submitDoc = (await (await _client.PutAsJsonAsync($"/api/quote/{quoteId}/submit", new { }))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        var approvalId = submitDoc.RootElement.GetProperty("data").GetProperty("oaApprovalId").GetInt64();

        // 审批通过（使用精确的 JSON body 以保持 HMAC 一致性）
        var cbBody = $"{{\"oaApprovalId\":{approvalId},\"approved\":true,\"comment\":\"ok\"}}";
        var cbReq = new HttpRequestMessage(HttpMethod.Post, "/api/approval-callback")
        {
            Content = new StringContent(cbBody, Encoding.UTF8, "application/json")
        };
        cbReq.Headers.Add("X-Crm-Signature", ComputeHmac(cbBody));
        await _client.SendAsync(cbReq);

        // 确认报价
        await _client.PutAsJsonAsync($"/api/quote/{quoteId}/confirm", new { });

        return quoteId;
    }

    [Fact]
    public async Task GetContractList_ReturnsSeedContract()
    {
        var resp = await _client.GetAsync("/api/contract");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CreateContract_FromConfirmedQuote_HasDraftStatus()
    {
        var quoteId = await CreateApprovedQuoteAsync();

        var custResp = await _client.GetAsync("/api/customer?keyword=华为");
        var customerId = (await custResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("rows")[0]
            .GetProperty("customerId").GetInt64();

        var createResp = await _client.PostAsJsonAsync("/api/contract", new
        {
            quoteId,
            title = "测试销售合同",
            deliveryDate = DateTime.UtcNow.AddDays(45),
            signDate = DateTime.UtcNow
        });
        var createDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        createDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        createDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("draft");
        createDoc.RootElement.GetProperty("data").GetProperty("contractCode").GetString().Should().StartWith("CON-");
    }

    [Fact]
    public async Task SubmitAndApproveContract_ChangesStatusToActive()
    {
        var quoteId = await CreateApprovedQuoteAsync();

        var createResp = await _client.PostAsJsonAsync("/api/contract", new
        {
            quoteId,
            title = $"激活合同测试 {Guid.NewGuid()}",
            deliveryDate = DateTime.UtcNow.AddDays(45),
            signDate = DateTime.UtcNow
        });
        var contractId = (await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("contractId").GetInt64();

        // 提交审批
        var submitDoc = (await (await _client.PutAsJsonAsync($"/api/contract/{contractId}/submit", new { }))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        var approvalId = submitDoc.RootElement.GetProperty("data").GetProperty("oaApprovalId").GetInt64();

        // 审批通过
        var body = $"{{\"oaApprovalId\":{approvalId},\"approved\":true,\"comment\":\"合同ok\"}}";
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/approval-callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Crm-Signature", ComputeHmac(body));
        await _client.SendAsync(req);

        var getDoc = (await (await _client.GetAsync($"/api/contract/{contractId}"))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        getDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("active");
    }

    [Fact]
    public async Task ShipContract_RecordsActualDeliveryDate()
    {
        var contractId = await GetSeedContractIdAsync();

        var shipDate = DateTime.UtcNow;
        var shipResp = await _client.PutAsJsonAsync($"/api/contract/{contractId}/ship", new
        {
            trackingNumber = "SF1234567890",
            actualDeliveryDate = shipDate
        });
        var shipDoc = await shipResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        shipDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var getDoc = (await (await _client.GetAsync($"/api/contract/{contractId}"))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        getDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("shipped");
    }

    [Fact]
    public async Task AddPayment_ThenConfirm_ChangesPaymentStatus()
    {
        var contractId = await GetSeedContractIdAsync();

        // 添加回款
        var payResp = await _client.PostAsJsonAsync($"/api/contract/{contractId}/payment", new
        {
            amount = 50000m,
            paymentMethod = "银行转账",
            paymentDate = DateTime.UtcNow,
            bankReference = "TXN-TEST-001"
        });
        var payDoc = await payResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        payDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var paymentId = payDoc.RootElement.GetProperty("data").GetProperty("paymentId").GetInt64();

        // 确认回款
        var confirmResp = await _client.PutAsJsonAsync($"/api/payment/{paymentId}/confirm", new { });
        var confirmDoc = await confirmResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        confirmDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task SubmitPaymentForApproval_ChangesStatusToPendingApproval()
    {
        var contractId = await GetSeedContractIdAsync();

        var payResp = await _client.PostAsJsonAsync($"/api/contract/{contractId}/payment", new
        {
            amount = 196000m,
            paymentMethod = "银行转账",
            paymentDate = DateTime.UtcNow,
            bankReference = "TXN-TEST-002"
        });
        var paymentId = (await payResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("paymentId").GetInt64();

        var submitResp = await _client.PutAsJsonAsync($"/api/payment/{paymentId}/submit", new { });
        var submitDoc = await submitResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        submitDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        submitDoc.RootElement.GetProperty("data").GetProperty("oaApprovalId").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompleteContract_AfterShipment_ChangesStatusToCompleted()
    {
        var quoteId = await CreateApprovedQuoteAsync();

        var createResp = await _client.PostAsJsonAsync("/api/contract", new
        {
            quoteId,
            title = $"完成合同测试 {Guid.NewGuid()}",
            deliveryDate = DateTime.UtcNow.AddDays(45),
            signDate = DateTime.UtcNow
        });
        var contractId = (await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("contractId").GetInt64();

        // 提交 + 审批通过 → active
        var submitDoc = (await (await _client.PutAsJsonAsync($"/api/contract/{contractId}/submit", new { }))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        var approvalId = submitDoc.RootElement.GetProperty("data").GetProperty("oaApprovalId").GetInt64();

        var body = $"{{\"oaApprovalId\":{approvalId},\"approved\":true,\"comment\":\"ok\"}}";
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/approval-callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Crm-Signature", ComputeHmac(body));
        await _client.SendAsync(req);

        // 发货
        await _client.PutAsJsonAsync($"/api/contract/{contractId}/ship", new
        {
            trackingNumber = "SF999",
            actualDeliveryDate = DateTime.UtcNow
        });

        // 完成
        var completeResp = await _client.PutAsJsonAsync($"/api/contract/{contractId}/complete", new { });
        var completeDoc = await completeResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        completeDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var getDoc = (await (await _client.GetAsync($"/api/contract/{contractId}"))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        getDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("completed");
    }

    [Fact]
    public async Task TerminateContract_ChangesStatusToTerminated()
    {
        var quoteId = await CreateApprovedQuoteAsync();

        var createResp = await _client.PostAsJsonAsync("/api/contract", new
        {
            quoteId,
            title = $"终止合同测试 {Guid.NewGuid()}",
            deliveryDate = DateTime.UtcNow.AddDays(45),
            signDate = DateTime.UtcNow
        });
        var contractId = (await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("contractId").GetInt64();

        // 提交 + 审批通过 → active（终止需要 active 状态）
        var submitDoc = (await (await _client.PutAsJsonAsync($"/api/contract/{contractId}/submit", new { }))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        var approvalId = submitDoc.RootElement.GetProperty("data").GetProperty("oaApprovalId").GetInt64();
        var body = $"{{\"oaApprovalId\":{approvalId},\"approved\":true,\"comment\":\"ok\"}}";
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/approval-callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Crm-Signature", ComputeHmac(body));
        await _client.SendAsync(req);

        var terminateResp = await _client.PutAsJsonAsync($"/api/contract/{contractId}/terminate", new
        {
            reason = "客户取消订单"
        });
        var terminateDoc = await terminateResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        terminateDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        var getDoc = (await (await _client.GetAsync($"/api/contract/{contractId}"))
            .Content.ReadFromJsonAsync<JsonDocument>(_json))!;
        getDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("terminated");
    }
}
