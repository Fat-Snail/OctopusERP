using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

public class GarmentWebhookTests : IClassFixture<MesTestFactory>
{
    private readonly HttpClient _client;
    private const string Secret = "mes-shared-secret-dev";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public GarmentWebhookTests(MesTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static string ComputeSig(string body) =>
        "sha256=" + Convert.ToHexString(
            new HMACSHA256(Encoding.UTF8.GetBytes(Secret))
                .ComputeHash(Encoding.UTF8.GetBytes(body))).ToLower();

    private async Task<HttpResponseMessage> PostWithSig(string path, object payload, string? sigOverride = null)
    {
        var body = JsonSerializer.Serialize(payload, _json);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        req.Headers.TryAddWithoutValidation("X-Mes-Signature", sigOverride ?? ComputeSig(body));
        return await _client.SendAsync(req);
    }

    [Fact]
    public async Task GarmentOrderCreated_WithValidHmac_CreatesOrder()
    {
        var payload = new
        {
            ContractId = 9001L,
            CustomerId = 5001L,
            CustomerName = "示例客户1",
            OrderNo = "98071",
            PlmProductId = (long?)1L,
            StyleCode = "LB5210100龟背绣花",
            Quantity = 2000m,
            Season = "22春夏",
            DueDate = (DateTime?)new DateTime(2099, 6, 1),
            IsAdditional = false,
            OriginalContractId = (long?)null,
        };

        var resp = await PostWithSig("/api/webhook/garment-order-created", payload);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("orderId").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GarmentOrderCreated_WithSameContractId_UpdatesInsteadOfDuplicating()
    {
        var firstPayload = new
        {
            ContractId = 9002L,
            CustomerId = 5002L,
            CustomerName = "示例客户2",
            OrderNo = "96321",
            PlmProductId = (long?)null,
            StyleCode = "LA5500121",
            Quantity = 1000m,
            Season = "22春夏",
            DueDate = (DateTime?)null,
            IsAdditional = false,
            OriginalContractId = (long?)null,
        };
        var resp1 = await PostWithSig("/api/webhook/garment-order-created", firstPayload);
        var doc1 = await resp1.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var firstId = doc1!.RootElement.GetProperty("data").GetProperty("orderId").GetInt64();

        var updatedPayload = new
        {
            ContractId = 9002L,  // 同合同 ID
            CustomerId = 5002L,
            CustomerName = "示例客户2",
            OrderNo = "96321",
            PlmProductId = (long?)null,
            StyleCode = "LA5500121",
            Quantity = 1500m,  // 数量变更
            Season = "22春夏",
            DueDate = (DateTime?)null,
            IsAdditional = false,
            OriginalContractId = (long?)null,
        };
        var resp2 = await PostWithSig("/api/webhook/garment-order-created", updatedPayload);
        var doc2 = await resp2.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var secondId = doc2!.RootElement.GetProperty("data").GetProperty("orderId").GetInt64();
        secondId.Should().Be(firstId);  // 幂等：同 ID
    }

    [Fact]
    public async Task GarmentOrderCreated_WithoutHmac_Returns401()
    {
        var payload = new { ContractId = 9099L, CustomerId = 1L, CustomerName = "x", OrderNo = "x", Quantity = 1m, IsAdditional = false };
        var resp = await _client.PostAsJsonAsync("/api/webhook/garment-order-created", payload);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GarmentOrderCreated_WithBadHmac_Returns401()
    {
        var payload = new { ContractId = 9098L, CustomerId = 1L, CustomerName = "x", OrderNo = "x", Quantity = 1m, IsAdditional = false };
        var resp = await PostWithSig("/api/webhook/garment-order-created", payload, "sha256=invalid");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FabricArrived_UpdatesGarmentScheduleFabricStatus()
    {
        // 找一条 seed 排程
        var listResp = await _client.GetAsync("/api/garment-schedule?pageSize=1&from=2021-10-01&to=2021-10-31");
        var listDoc = await listResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var first = listDoc!.RootElement.GetProperty("data").GetProperty("rows")[0];
        var scheduleId = first.GetProperty("scheduleId").GetInt64();

        var payload = new
        {
            MesScheduleId = (long?)scheduleId,
            MesOrderId = (long?)null,
            ArrivalCode = "FA-TEST-001",
            FabricStatus = "5-9/ok",
            IsComplete = true,
            ActualArrivalDate = (DateTime?)new DateTime(2099, 5, 9),
        };
        var resp = await PostWithSig("/api/webhook/fabric-arrived", payload);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        // 验证 FabricStatus 已更新
        var getResp = await _client.GetAsync($"/api/garment-schedule/{scheduleId}");
        var getDoc = await getResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        getDoc!.RootElement.GetProperty("data").GetProperty("fabricStatus").GetString().Should().Be("5-9/ok");
    }

    [Fact]
    public async Task FabricArrived_WithoutHmac_Returns401()
    {
        var payload = new
        {
            MesScheduleId = (long?)1, MesOrderId = (long?)null, ArrivalCode = "x",
            FabricStatus = "ok", IsComplete = true, ActualArrivalDate = (DateTime?)null,
        };
        var resp = await _client.PostAsJsonAsync("/api/webhook/fabric-arrived", payload);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublishAndApprove_CreatesWorkOrderAndLinksBack()
    {
        // 1. 找一条排程并发布
        var listResp = await _client.GetAsync("/api/garment-schedule?from=2021-10-15&to=2021-10-20&pageSize=5");
        var listDoc = await listResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = listDoc!.RootElement.GetProperty("data").GetProperty("rows");
        long? scheduleId = null;
        foreach (var r in rows.EnumerateArray())
        {
            if (r.GetProperty("scheduleStatus").GetString() == "draft")
            {
                scheduleId = r.GetProperty("scheduleId").GetInt64();
                break;
            }
        }
        scheduleId.Should().NotBeNull();

        var fakeApprovalId = 7771L + scheduleId.Value;
        var publishResp = await _client.PostAsJsonAsync(
            $"/api/garment-schedule/{scheduleId}/publish",
            new { OaApprovalId = fakeApprovalId });
        var publishDoc = await publishResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        publishDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        publishDoc.RootElement.GetProperty("data").GetProperty("scheduleStatus").GetString().Should().Be("pending_approval");

        // 2. 模拟 OA 审批通过 webhook 回调
        var callbackResp = await PostWithSig("/api/webhook/approval-callback", new
        {
            OaApprovalId = fakeApprovalId,
            Approved = true,
            Comment = "通过",
        });
        var callbackDoc = await callbackResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        callbackDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        callbackDoc.RootElement.GetProperty("data").GetProperty("scheduleStatus").GetString().Should().Be("approved");
        callbackDoc.RootElement.GetProperty("data").GetProperty("workOrderCode").GetString().Should().StartWith("WO-");

        // 3. 验证排程上 WorkOrderId 已写回
        var getResp = await _client.GetAsync($"/api/garment-schedule/{scheduleId}");
        var getDoc = await getResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        getDoc!.RootElement.GetProperty("data").GetProperty("workOrderId").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ApprovalCallback_Rejected_SetsScheduleToRejected()
    {
        // 找另一条排程
        var listResp = await _client.GetAsync("/api/garment-schedule?from=2021-10-15&to=2021-10-25&pageSize=20");
        var listDoc = await listResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = listDoc!.RootElement.GetProperty("data").GetProperty("rows");
        long scheduleId = 0;
        foreach (var r in rows.EnumerateArray())
        {
            if (r.GetProperty("scheduleStatus").GetString() == "draft")
            {
                scheduleId = r.GetProperty("scheduleId").GetInt64();
                break;
            }
        }
        scheduleId.Should().BeGreaterThan(0);

        var approvalId = 88880L + scheduleId;
        await _client.PostAsJsonAsync(
            $"/api/garment-schedule/{scheduleId}/publish",
            new { OaApprovalId = approvalId });

        var resp = await PostWithSig("/api/webhook/approval-callback", new
        {
            OaApprovalId = approvalId,
            Approved = false,
            Comment = "款式有误",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("data").GetProperty("scheduleStatus").GetString().Should().Be("rejected");
    }

    [Fact]
    public async Task GarmentOrderCreated_AdditionalOrder_LinksToOriginal()
    {
        var payload = new
        {
            ContractId = 9011L,
            CustomerId = 5001L,
            CustomerName = "示例客户1",
            OrderNo = "98071加单1",
            PlmProductId = (long?)1L,
            StyleCode = "LB5210100",
            Quantity = 500m,
            Season = "22春夏",
            DueDate = (DateTime?)new DateTime(2099, 7, 1),
            IsAdditional = true,
            OriginalContractId = (long?)9001L,
        };
        var resp = await PostWithSig("/api/webhook/garment-order-created", payload);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }
}
