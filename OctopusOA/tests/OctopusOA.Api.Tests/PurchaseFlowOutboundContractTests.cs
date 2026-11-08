using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 采购审批跨系统链路 — 出站载荷契约测试。
/// 校验审批通过后 OA 发往 WMS / MES 的 webhook 载荷与接收方契约一致
/// （WMS ReceiveGoodsRequest / MES ApprovalCallbackRequest），与两系统的接收测试共同构成链路验证。
/// </summary>
public class PurchaseFlowOutboundContractTests : IClassFixture<OATestFactory>
{
    private readonly OATestFactory _factory;
    private readonly CapturingHttpClientFactory _capture = new();

    public PurchaseFlowOutboundContractTests(OATestFactory factory) => _factory = factory;

    private HttpClient CreateClient()
    {
        return _factory
            .WithWebHostBuilder(b =>
            {
                b.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Webhooks:WmsBaseUrl"] = "http://wms.test",
                        ["Webhooks:MesBaseUrl"] = "http://mes.test",
                        ["Webhooks:WmsSecret"] = "wms-shared-secret-dev",
                        ["Webhooks:MesSecret"] = "mes-shared-secret-dev",
                    });
                });
                b.ConfigureServices(s =>
                {
                    s.RemoveAll<IHttpClientFactory>();
                    s.AddSingleton<IHttpClientFactory>(_capture);
                });
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task PurchaseApproval_EmitsWmsAndMesContractPayloads()
    {
        var client = CreateClient();

        // 张三发起采购（金额 5 万，< 10 万，跳过总经理）
        var submitReq = new HttpRequestMessage(HttpMethod.Post, "/api/flow/submit")
        {
            Content = JsonContent.Create(new
            {
                definitionCode = "purchase_order",
                businessKey = "purchase_order:1",
                title = "采购面料 5 万",
                formData = """{"supplierName":"供应商A","productName":"FabricX","plmProductId":1,"quantity":100,"unit":"米","amount":50000}""",
            }),
        };
        submitReq.Headers.Add("X-Test-UserId", "2");
        var submit = await client.SendAsync(submitReq);
        var submitDoc = JsonDocument.Parse(await submit.Content.ReadAsStringAsync());
        submitDoc.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var instanceId = submitDoc.RootElement.GetProperty("data").GetProperty("instanceId").GetInt64();

        // admin（主管 + 财务）两次通过 → 服务任务 + approved 事件
        for (var i = 0; i < 2; i++)
        {
            var pending = await client.GetAsync("/api/flow/pending");
            var pendingDoc = JsonDocument.Parse(await pending.Content.ReadAsStringAsync());
            var taskId = pendingDoc.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray()
                .First(r => r.GetProperty("instanceId").GetInt64() == instanceId)
                .GetProperty("taskId").GetInt64();

            var approve = await client.PutAsJsonAsync($"/api/flow/task/{taskId}/approve", new { comment = "ok" });
            var approveDoc = JsonDocument.Parse(await approve.Content.ReadAsStringAsync());
            approveDoc.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        }

        await _capture.WaitForAsync(2, TimeSpan.FromSeconds(5));

        // WMS 契约：ReceiveGoodsRequest（PascalCase）
        var wms = _capture.Requests.Single(r => r.Url.Contains("/api/webhook/receive-goods"));
        wms.SignatureHeader.Should().Be("X-Wms-Signature");
        wms.Body.Should().Contain("\"InboundType\":\"purchase\"");
        wms.Body.Should().Contain("\"ProductName\":\"FabricX\"");
        wms.Body.Should().Contain("\"Quantity\":100");

        // MES 契约：ApprovalCallbackRequest（PascalCase）
        var mes = _capture.Requests.Single(r => r.Url.Contains("/api/webhook/approval-callback"));
        mes.SignatureHeader.Should().Be("X-Mes-Signature");
        mes.Body.Should().Contain($"\"OaApprovalId\":{instanceId}");
        mes.Body.Should().Contain("\"Approved\":true");
    }
}

/// <summary>捕获出站 webhook 的 Http 客户端工厂（fire-and-forget 亦可断言）</summary>
public sealed class CapturingHttpClientFactory : IHttpClientFactory
{
    private readonly List<CapturedRequest> _requests = new();
    private readonly object _lock = new();

    public IReadOnlyList<CapturedRequest> Requests
    {
        get { lock (_lock) return _requests.ToList(); }
    }

    public HttpClient CreateClient(string name) => new(new CapturingHandler(this), disposeHandler: false);

    public async Task WaitForAsync(int count, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            lock (_lock) if (_requests.Count >= count) return;
            await Task.Delay(50);
        }
    }

    private void Add(CapturedRequest request)
    {
        lock (_lock) _requests.Add(request);
    }

    private sealed class CapturingHandler(CapturingHttpClientFactory owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            string header = "", sig = "";
            if (request.Headers.TryGetValues("X-Wms-Signature", out var w)) { header = "X-Wms-Signature"; sig = w.First(); }
            else if (request.Headers.TryGetValues("X-Mes-Signature", out var m)) { header = "X-Mes-Signature"; sig = m.First(); }

            owner.Add(new CapturedRequest(request.RequestUri!.ToString(), body, header, sig));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

public record CapturedRequest(string Url, string Body, string SignatureHeader, string Signature);
