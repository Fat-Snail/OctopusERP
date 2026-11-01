using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OctopusWMS.Api.Persistence;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusWMS.Api.Tests;

public class WebhookTests : IClassFixture<WmsTestFactory>
{
    private readonly HttpClient _client;
    private readonly WmsTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private const string WmsSecret = "wms-shared-secret-dev";

    public WebhookTests(WmsTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static string ComputeHmac(string body, string secret = WmsSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLower();
    }

    private async Task<HttpResponseMessage> PostWithSignature(string path, object payload, string? secret = null)
    {
        var body = JsonSerializer.Serialize(payload);
        var sig = ComputeHmac(body, secret ?? WmsSecret);
        var req = new HttpRequestMessage(HttpMethod.Post, path)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        req.Headers.TryAddWithoutValidation("X-Wms-Signature", sig);
        return await _client.SendAsync(req);
    }

    [Fact]
    public async Task ReserveStock_WithValidSignature_LocksInventory()
    {
        // Get a seed inventory item first
        var invResp = await _client.GetAsync("/api/inventory?pageNum=1&pageSize=10");
        var invDoc = await invResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var rows = invDoc!.RootElement.GetProperty("data").GetProperty("rows");
        if (rows.GetArrayLength() == 0)
        {
            // No inventory, test just checks the endpoint is callable
            var emptyResp = await PostWithSignature("/api/webhook/reserve-stock", new
            {
                CrmContractId = 999L,
                Items = Array.Empty<object>()
            });
            emptyResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            return;
        }

        var firstItem = rows[0];
        var productName = firstItem.GetProperty("productName").GetString()!;
        var oldLocked = firstItem.TryGetProperty("lockedQuantity", out var lq) ? lq.GetDecimal() : 0m;

        var resp = await PostWithSignature("/api/webhook/reserve-stock", new
        {
            CrmContractId = 1L,
            Items = new[] { new { ProductName = productName, PlmProductId = (long?)null, Quantity = 5m } }
        });

        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        // Verify locked quantity increased
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var inv = db.Inventories.FirstOrDefault(x => x.ProductName == productName);
        inv.Should().NotBeNull();
        inv!.LockedQuantity.Should().BeGreaterThanOrEqualTo(oldLocked + 5m);
    }

    [Fact]
    public async Task ReserveStock_WithInvalidSignature_Returns401()
    {
        var body = JsonSerializer.Serialize(new { CrmContractId = 1L, Items = Array.Empty<object>() });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/reserve-stock")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Wms-Signature", "sha256=invalidsignature");
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReserveStock_WithoutSignatureHeader_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/webhook/reserve-stock", new
        {
            CrmContractId = 1L,
            Items = Array.Empty<object>()
        });
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReceiveGoods_WithValidSignature_CreatesInboundAndUpdatesInventory()
    {
        var woId = 9001L;
        var productName = $"WH-TEST-{Guid.NewGuid().ToString("N")[..6]}";

        var resp = await PostWithSignature("/api/webhook/receive-goods", new
        {
            MesWorkOrderId = woId,
            PlmProductId = (long?)null,
            ProductName = productName,
            Spec = "规格A",
            Unit = "件",
            Quantity = 100m
        });

        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);

        // Verify inbound created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var inbound = db.InboundOrders.FirstOrDefault(x => x.MesWorkOrderId == woId);
        inbound.Should().NotBeNull();
        inbound!.InboundType.Should().Be("production");
        inbound.Status.Should().Be("completed");

        // Verify inventory updated
        var inv = db.Inventories.FirstOrDefault(x => x.ProductName == productName);
        inv.Should().NotBeNull();
        inv!.Quantity.Should().Be(100m);
    }

    [Fact]
    public async Task ReceiveGoods_WithInvalidSignature_Returns401()
    {
        var resp = await PostWithSignature("/api/webhook/receive-goods",
            new { MesWorkOrderId = 1L, ProductName = "Test", Quantity = 1m },
            secret: "wrong-secret");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
