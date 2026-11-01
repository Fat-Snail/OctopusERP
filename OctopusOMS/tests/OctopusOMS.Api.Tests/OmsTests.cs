using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusOMS.Api.Tests;

public class OmsTests : IClassFixture<OmsTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public OmsTests(OmsTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // ── Catalog ───────────────────────────────────────────────────────

    [Fact]
    public async Task Channels_SeedHas6()
    {
        var doc = await GetJson("/api/catalog/channels");
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task Shops_FilterByChannel()
    {
        var doc = await GetJson("/api/catalog/shops?channelId=1");
        var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        rows.Should().NotBeEmpty();
        rows.All(r => r.GetProperty("channelId").GetInt64() == 1).Should().BeTrue();
    }

    [Fact]
    public async Task SkuMappings_SeedAndCreate()
    {
        var listDoc = await GetJson("/api/catalog/sku-mappings");
        listDoc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(4);

        var create = await _client.PostAsJsonAsync("/api/catalog/sku-mappings", new
        {
            channelId = 5L, channelSkuCode = "SHOPIFY-IPH15PRO", plmProductId = 1L, note = "Shopify 映射",
        });
        var doc = await create.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task SkuMapping_DuplicateRejected()
    {
        await _client.PostAsJsonAsync("/api/catalog/sku-mappings", new
        { channelId = 1L, channelSkuCode = "TB-IPH15PRO-256", plmProductId = 1L, note = "" });
        // 种子已经有 ChannelId=1, ChannelSkuCode=TB-IPH15PRO-256
        var doc = await GetJson("/api/catalog/sku-mappings?channelId=1");
        var count = doc.RootElement.GetProperty("data").EnumerateArray()
            .Count(r => r.GetProperty("channelSkuCode").GetString() == "TB-IPH15PRO-256");
        count.Should().Be(1);
    }

    // ── Orders ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_AutoMapsSku()
    {
        var resp = await _client.PostAsJsonAsync("/api/order", new
        {
            shopId = 1L,
            externalOrderNo = $"TB-TEST-{Guid.NewGuid():N}",
            customerName = "测试买家",
            customerPhone = "13800138000",
            shipAddress = "北京市朝阳区",
            items = new[]
            {
                new { channelSkuCode = "TB-IPH15PRO-256", productName = "iPhone 15 Pro", spec = "256GB", quantity = 1m, unitPrice = 9999m }
            }
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var order = doc.RootElement.GetProperty("data");
        order.GetProperty("totalAmount").GetDecimal().Should().Be(9999m);
        var firstItem = order.GetProperty("items").EnumerateArray().First();
        firstItem.GetProperty("plmProductId").GetInt64().Should().Be(1); // 映射到 PLM
    }

    [Fact]
    public async Task CreateOrder_DuplicateExternalNo_IsIdempotent()
    {
        var ext = $"DUP-{Guid.NewGuid():N}";
        var first = await _client.PostAsJsonAsync("/api/order", new
        {
            shopId = 1L, externalOrderNo = ext, customerName = "幂等",
            items = new[] { new { channelSkuCode = "TB-IPH15PRO-256", productName = "iPhone", spec = "", quantity = 1m, unitPrice = 100m } }
        });
        var firstId = (await first.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("orderId").GetInt64();

        var second = await _client.PostAsJsonAsync("/api/order", new
        {
            shopId = 1L, externalOrderNo = ext, customerName = "幂等",
            items = new[] { new { channelSkuCode = "TB-IPH15PRO-256", productName = "iPhone", spec = "", quantity = 1m, unitPrice = 100m } }
        });
        var secondId = (await second.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("orderId").GetInt64();

        secondId.Should().Be(firstId);
    }

    [Fact]
    public async Task PayAndPushAndShipFlow()
    {
        // 1. 创建订单
        var c = await _client.PostAsJsonAsync("/api/order", new
        {
            shopId = 1L, externalOrderNo = $"FLOW-{Guid.NewGuid():N}", customerName = "流程测试",
            items = new[] { new { channelSkuCode = "TB-IPH15PRO-256", productName = "iPhone", spec = "256GB", quantity = 1m, unitPrice = 9999m } }
        });
        var orderId = (await c.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("orderId").GetInt64();

        // 2. 支付
        var pay = await _client.PutAsJsonAsync($"/api/order/{orderId}/pay", new { amount = 9999m });
        var payDoc = await pay.Content.ReadFromJsonAsync<JsonDocument>(_json);
        payDoc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("paid");

        // 3. 下推 WMS
        var push = await _client.PutAsync($"/api/order/{orderId}/push-wms", null);
        var pushDoc = await push.Content.ReadFromJsonAsync<JsonDocument>(_json);
        pushDoc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("processing");

        // 4. WMS 回调发货
        var ship = await _client.PostAsJsonAsync("/api/webhook/wms/shipped",
            new { omsOrderId = orderId, wmsOutboundOrderId = 999L });
        var shipDoc = await ship.Content.ReadFromJsonAsync<JsonDocument>(_json);
        shipDoc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("shipped");
    }

    [Fact]
    public async Task Cancel_ShippedOrder_Returns400()
    {
        var c = await _client.PostAsJsonAsync("/api/order", new
        {
            shopId = 1L, externalOrderNo = $"CANCEL-{Guid.NewGuid():N}", customerName = "取消测试",
            items = new[] { new { channelSkuCode = "TB-IPH15PRO-256", productName = "iPhone", spec = "", quantity = 1m, unitPrice = 1000m } }
        });
        var orderId = (await c.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("orderId").GetInt64();
        await _client.PutAsJsonAsync($"/api/order/{orderId}/pay", new { amount = 1000m });
        await _client.PutAsync($"/api/order/{orderId}/push-wms", null);
        await _client.PostAsJsonAsync("/api/webhook/wms/shipped", new { omsOrderId = orderId, wmsOutboundOrderId = 1L });

        var resp = await _client.PutAsJsonAsync($"/api/order/{orderId}/cancel", new { reason = "客户改主意" });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    // ── After-sale ─────────────────────────────────────────────────────

    [Fact]
    public async Task AfterSale_CreateApproveCompleteFlow()
    {
        // 准备一个已支付订单
        var c = await _client.PostAsJsonAsync("/api/order", new
        {
            shopId = 1L, externalOrderNo = $"AS-{Guid.NewGuid():N}", customerName = "售后买家",
            items = new[] { new { channelSkuCode = "TB-IPH15PRO-256", productName = "iPhone", spec = "", quantity = 1m, unitPrice = 1000m } }
        });
        var orderId = (await c.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("orderId").GetInt64();
        await _client.PutAsJsonAsync($"/api/order/{orderId}/pay", new { amount = 1000m });

        // 发起退款售后
        var create = await _client.PostAsJsonAsync("/api/after-sale", new
        {
            orderId, type = "refund", amount = 1000m, reason = "买错型号", note = "全额退款",
        });
        var asId = (await create.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("afterSaleId").GetInt64();

        // 批准
        var approve = await _client.PutAsJsonAsync($"/api/after-sale/{asId}/approve", new { note = "运营审核通过" });
        var apDoc = await approve.Content.ReadFromJsonAsync<JsonDocument>(_json);
        apDoc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("approved");

        // 完成 → 订单标 refunded
        var done = await _client.PutAsync($"/api/after-sale/{asId}/complete", null);
        var doneDoc = await done.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doneDoc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("completed");

        var orderDoc = await GetJson($"/api/order/{orderId}");
        orderDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("refunded");
    }

    [Fact]
    public async Task AfterSale_OverAmount_Returns400()
    {
        var c = await _client.PostAsJsonAsync("/api/order", new
        {
            shopId = 1L, externalOrderNo = $"OVER-{Guid.NewGuid():N}", customerName = "x",
            items = new[] { new { channelSkuCode = "TB-IPH15PRO-256", productName = "x", spec = "", quantity = 1m, unitPrice = 100m } }
        });
        var orderId = (await c.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("orderId").GetInt64();
        await _client.PutAsJsonAsync($"/api/order/{orderId}/pay", new { amount = 100m });

        var resp = await _client.PostAsJsonAsync("/api/after-sale", new
        {
            orderId, type = "refund", amount = 500m, reason = "测试超额", note = "x",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    // ── helpers ─────────────────────────────────────────────────────

    private async Task<JsonDocument> GetJson(string url)
    {
        var resp = await _client.GetAsync(url);
        return (await resp.Content.ReadFromJsonAsync<JsonDocument>(_json))!;
    }
}
