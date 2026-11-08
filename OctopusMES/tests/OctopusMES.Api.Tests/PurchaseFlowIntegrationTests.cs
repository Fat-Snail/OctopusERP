using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusMES.Api.Tests;

/// <summary>采购单 → 审批中心（OA Flow）联动</summary>
public class PurchaseFlowIntegrationTests : IClassFixture<MesTestFactory>
{
    private readonly MesTestFactory _factory;
    private readonly HttpClient _client;
    private const string Secret = "mes-shared-secret-dev";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PurchaseFlowIntegrationTests(MesTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private sealed class FakeOaFlowClient(long? instanceId) : IOaFlowClient
    {
        public Task<long?> SubmitPurchaseAsync(MesPurchaseOrder po) => Task.FromResult(instanceId);
    }

    private static string ComputeSig(string body) =>
        "sha256=" + Convert.ToHexString(
            new HMACSHA256(Encoding.UTF8.GetBytes(Secret))
                .ComputeHash(Encoding.UTF8.GetBytes(body))).ToLower();

    private async Task<HttpResponseMessage> PostWithSig(string path, object payload)
    {
        var body = JsonSerializer.Serialize(payload, _json);
        var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        req.Headers.TryAddWithoutValidation("X-Mes-Signature", ComputeSig(body));
        return await _client.SendAsync(req);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_CallsOaFlow_AndStoresInstanceId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        var svc = new PurchaseOrderService(db, new FakeOaFlowClient(999));

        var po = await svc.CreateAsync(new MesPurchaseOrder
        {
            SupplierId = 1,
            Items = { new MesPurchaseItem { ProductName = "测试零件", Quantity = 10, UnitPrice = 10, Amount = 100 } },
        }, 1);

        var submitted = await svc.SubmitAsync(po.PurchaseId);

        submitted.Should().NotBeNull();
        submitted!.Status.Should().Be("submitted");
        submitted.OaApprovalId.Should().Be(999);
        (await db.PurchaseOrders.FindAsync(po.PurchaseId))!.OaApprovalId.Should().Be(999);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_WithoutOa_StillSubmitted()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
        var svc = new PurchaseOrderService(db, new FakeOaFlowClient(null));

        var po = await svc.CreateAsync(new MesPurchaseOrder
        {
            SupplierId = 1,
            Items = { new MesPurchaseItem { ProductName = "无OA", Quantity = 1, UnitPrice = 1, Amount = 1 } },
        }, 1);

        var submitted = await svc.SubmitAsync(po.PurchaseId);

        submitted!.Status.Should().Be("submitted");
        submitted.OaApprovalId.Should().BeNull();
    }

    [Fact]
    public async Task ApprovalCallback_UpdatesPurchaseOrderStatus()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MesDbContext>();
            db.PurchaseOrders.Add(new MesPurchaseOrder
            {
                PurchaseCode = "PO-TEST-777", SupplierId = 1, Status = "submitted",
                OaApprovalId = 777, TotalAmount = 100,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var resp = await PostWithSig("/api/webhook/approval-callback",
            new { OaApprovalId = 777L, Approved = true, Comment = "同意" });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        using var s2 = _factory.Services.CreateScope();
        var db2 = s2.ServiceProvider.GetRequiredService<MesDbContext>();
        var po = await db2.PurchaseOrders.FirstAsync(x => x.OaApprovalId == 777);
        po.Status.Should().Be("approved");
    }
}
