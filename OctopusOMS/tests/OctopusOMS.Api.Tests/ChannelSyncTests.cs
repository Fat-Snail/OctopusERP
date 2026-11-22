using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusOMS.Api.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusOMS.Api.Tests;

/// <summary>
/// P2 渠道适配层沙箱全链路：
/// Mock 渠道拉单幂等入库 → 发货回传（状态机 + Mock 记录）→ 未支付拒绝 → 未知渠道明确报错
/// </summary>
public class ChannelSyncTests : IClassFixture<OmsTestFactory>
{
    private readonly HttpClient _client;
    private readonly OmsTestFactory _factory;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>seed 提供的 Mock 沙箱店铺（ChannelId=7，MockChannelAdapter）</summary>
    private const long MockShopId = 6;

    public ChannelSyncTests(OmsTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private OmsDbContext Db()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<OmsDbContext>();
    }

    [Fact]
    public async Task Sync_FirstRun_CreatesAllSandboxOrders()
    {
        // 清理 Mock 店铺历史订单（xUnit 类内共享 factory，测试间状态互染）
        using (var cleanup = _factory.Services.CreateScope())
        {
            var db0 = cleanup.ServiceProvider.GetRequiredService<OmsDbContext>();
            db0.Orders.RemoveRange(db0.Orders.Where(o => o.ShopId == MockShopId));
            db0.SaveChanges();
        }

        var resp = await _client.PostAsync("/api/channel-sync/sync/6", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("channelCode").GetString().Should().Be("MOCK");
        data.GetProperty("fetched").GetInt32().Should().Be(3);
        data.GetProperty("created").GetInt32().Should().Be(3);
        data.GetProperty("skipped").GetInt32().Should().Be(0);

        // 落库验证：已支付单 → paid，未支付单 → created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OmsDbContext>();
        var orders = await db.Orders.Include(o => o.Items)
            .Where(o => o.ShopId == MockShopId).ToListAsync();
        orders.Should().HaveCount(3);
        var o1 = orders.Single(o => o.ExternalOrderNo == "MOCK-2026-0001");
        o1.Status.Should().Be("paid");
        o1.OrderNo.Should().StartWith("OMS-MOCK-SANDBOX-");
        o1.Items.Should().ContainSingle(i => i.ChannelSkuCode == "MOCK-SKU-001" && i.Amount == 3560m);
        var o3 = orders.Single(o => o.ExternalOrderNo == "MOCK-2026-0003");
        o3.Status.Should().Be("created"); // 未支付
        o3.Items.First().PlmProductId.Should().BeNull(); // 无 SKU 映射 → 人工后补
    }

    [Fact]
    public async Task Sync_SecondRun_SkipsAll()
    {
        await _client.PostAsync("/api/channel-sync/sync/6", null); // 预热入库

        var resp = await _client.PostAsync("/api/channel-sync/sync/6", null);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        var data = doc!.RootElement.GetProperty("data");
        data.GetProperty("fetched").GetInt32().Should().Be(3);
        data.GetProperty("created").GetInt32().Should().Be(0);
        data.GetProperty("skipped").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task PushShipment_PaidOrder_ShippedAndRecordedInMock()
    {
        await _client.PostAsync("/api/channel-sync/sync/6", null); // 确保入库

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OmsDbContext>();
        var paid = await db.Orders.SingleAsync(o => o.ShopId == MockShopId && o.ExternalOrderNo == "MOCK-2026-0001");

        var resp = await _client.PostAsJsonAsync($"/api/channel-sync/shipment/{paid.OrderId}", new
        {
            trackingNo = "SF999888777",
            carrier = "顺丰速运",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("shipped");

        // 渠道平台（Mock）侧已记录回传
        var mockResp = await _client.GetAsync("/api/channel-sync/mock/pushed-shipments");
        var mockDoc = await mockResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        mockDoc!.RootElement.GetProperty("data").EnumerateArray()
            .Should().Contain(e => e.GetProperty("externalOrderNo").GetString() == "MOCK-2026-0001"
                && e.GetProperty("trackingNo").GetString() == "SF999888777");
    }

    [Fact]
    public async Task PushShipment_UnpaidOrder_Rejected()
    {
        await _client.PostAsync("/api/channel-sync/sync/6", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OmsDbContext>();
        var unpaid = await db.Orders.SingleAsync(o => o.ShopId == MockShopId && o.ExternalOrderNo == "MOCK-2026-0003");

        var resp = await _client.PostAsJsonAsync($"/api/channel-sync/shipment/{unpaid.OrderId}", new
        {
            trackingNo = "SF000111222",
            carrier = "顺丰速运",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("created");
    }

    [Fact]
    public async Task Sync_ShopOnUnsupportedChannel_ReturnsFriendlyError()
    {
        // 给 TAOBAO（无适配器实现）挂一个临时店铺，验证未知渠道的明确报错
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OmsDbContext>();
        var taobaoShopId = await db.Shops.Where(s => s.Channel!.ChannelCode == "TAOBAO")
            .Select(s => s.ShopId).FirstAsync();

        var resp = await _client.PostAsync($"/api/channel-sync/sync/{taobaoShopId}", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("TAOBAO");
        doc.RootElement.GetProperty("msg").GetString().Should().Contain("适配器");
    }
}
