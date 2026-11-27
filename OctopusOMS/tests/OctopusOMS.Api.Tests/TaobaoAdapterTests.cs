using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OctopusOMS.Api.Channels;
using System.Net;
using System.Text;

namespace OctopusOMS.Api.Tests;

/// <summary>
/// P2 淘宝渠道适配器测试：TOP 签名参考向量、响应归一化、分页、错误重试、
/// 物流公司映射、库存回写，以及无凭证时的确定性仿真模式。
/// </summary>
public class TaobaoAdapterTests
{
    private const string Secret = "secret";

    /// <summary>TOP 官方签名算法的固定参考向量（由 secret + 字典序 key/value + secret 的 MD5 大写）</summary>
    [Fact]
    public void Sign_MatchesTopReferenceVector()
    {
        var p = new Dictionary<string, string>
        {
            ["app_key"] = "test",
            ["format"] = "json",
            ["method"] = "taobao.trades.sold.get",
            ["session"] = "token123",
            ["sign_method"] = "md5",
            ["timestamp"] = "2026-11-26 20:00:00",
            ["v"] = "2.0",
        };

        TaobaoSigner.Sign(p, Secret).Should().Be("B4D1D0A8889C0ADC701E01EE93AAB46F");
    }

    [Fact]
    public void Mode_WithoutCredentials_IsSimulated_AndWithCredentials_IsLive()
    {
        var simulated = Create(new TaobaoOptions());
        simulated.IsSimulated.Should().BeTrue();
        simulated.Mode.Should().Be("simulated");

        var live = Create(new TaobaoOptions { AppKey = "key", AppSecret = Secret, Session = "token" });
        live.IsSimulated.Should().BeFalse();
        live.Mode.Should().Be("live");
    }

    [Fact]
    public async Task FetchOrders_SimulatedMode_ReturnsDeterministicOrdersAndFiltersBySince()
    {
        var adapter = Create(new TaobaoOptions());
        var since = new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);

        var all = await adapter.FetchOrdersAsync(new ChannelFetchQuery(1, null));
        var filtered = await adapter.FetchOrdersAsync(new ChannelFetchQuery(1, since));
        var again = await adapter.FetchOrdersAsync(new ChannelFetchQuery(1, null));

        all.Should().HaveCount(3);
        all.Select(o => o.ExternalOrderNo).Should().BeEquivalentTo(
            ["SIMTB-2026-0001", "SIMTB-2026-0002", "SIMTB-2026-0003"]);
        filtered.Should().HaveCount(2);
        again.Select(o => o.ExternalOrderNo).Should().Equal(all.Select(o => o.ExternalOrderNo));
        all.Should().Contain(o => o.PaidAmount == 0, "包含未付款订单以覆盖状态机拒绝路径");
    }

    [Fact]
    public async Task FetchOrders_LiveMode_ParsesTopResponseAndNormalizes()
    {
        var handler = new StubHandler(_ => Ok(TradePage("TBREAL001", hasNext: false)));
        var adapter = Create(LiveOptions(), handler);

        var orders = await adapter.FetchOrdersAsync(new ChannelFetchQuery(1, DateTime.UtcNow.AddDays(-1)));

        orders.Should().ContainSingle();
        var o = orders[0];
        o.ExternalOrderNo.Should().Be("TBREAL001");
        o.Currency.Should().Be("CNY");
        o.TotalAmount.Should().Be(3560m);
        o.PaidAmount.Should().Be(3560m);
        o.BuyerName.Should().Be("张**", "买家真实姓名应脱敏");
        o.BuyerPhone.Should().Be("138****0001", "手机号应脱敏");
        o.ShipAddress.Should().Be("浙江省杭州市西湖区文三路 100 号");
        o.OrderDate.Should().Be(new DateTime(2026, 11, 20, 9, 30, 0));
        o.Items.Should().ContainSingle();
        o.Items[0].ChannelSkuCode.Should().Be("TB-SKU-001");
        o.Items[0].ProductName.Should().Be("全棉卫衣面料");
        o.Items[0].Spec.Should().Be("280g/蓝色");
        o.Items[0].Quantity.Should().Be(80);
        o.Items[0].UnitPrice.Should().Be(44.5m);

        handler.RequestBodies.Should().ContainSingle();
        handler.RequestBodies[0].Should().Contain("method=taobao.trades.sold.get");
        handler.RequestBodies[0].Should().Contain("sign=");
        handler.RequestBodies[0].Should().Contain("format=json");
    }

    [Fact]
    public async Task FetchOrders_LiveMode_PaginatesUntilHasNextFalse()
    {
        var handler = new StubHandler(req => Ok(
            req.Contains("page_no=2") ? TwoTradePage("TBREAL002", hasNext: false) : TradePage("TBREAL001", hasNext: true)));
        var adapter = Create(LiveOptions(), handler);

        var orders = await adapter.FetchOrdersAsync(new ChannelFetchQuery(1, DateTime.UtcNow.AddDays(-1)));

        orders.Select(o => o.ExternalOrderNo).Should().Equal("TBREAL001", "TBREAL002");
        handler.RequestBodies.Should().HaveCount(2);
        handler.RequestBodies[0].Should().Contain("page_no=1");
        handler.RequestBodies[1].Should().Contain("page_no=2");
    }

    [Fact]
    public async Task FetchOrders_LiveMode_ErrorResponse_ThrowsWithPlatformMessage()
    {
        var handler = new StubHandler(_ => Ok("""
            {"error_response":{"code":15,"msg":"Remote service error","sub_code":"ISP.ACCESS-DENIED","sub_msg":"没有权限访问该API"}}
            """));
        var adapter = Create(LiveOptions(), handler);

        // 固定用过去时间作 since，避免 since≈now 时窗口为空导致偶发不调用 API
        var act = async () => await adapter.FetchOrdersAsync(new ChannelFetchQuery(1, DateTime.UtcNow.AddDays(-1)));

        (await act.Should().ThrowAsync<TaobaoApiException>())
            .Which.Message.Should().Contain("没有权限访问该API");
    }

    [Fact]
    public async Task FetchOrders_LiveMode_ThrottledOnce_RetriesAndSucceeds()
    {
        var calls = 0;
        var handler = new StubHandler(_ =>
        {
            calls++;
            return calls == 1
                ? Ok("""{"error_response":{"code":15,"sub_code":"TIPS.INVALID_TIMESTAMP","sub_msg":"请求过于频繁"}}""")
                : Ok(TradePage("TBREAL003", hasNext: false));
        });
        var opts = LiveOptions();
        opts.RetryDelayMs = 1;
        var adapter = Create(opts, handler);

        // 固定用过去时间作 since，避免 since≈now 时窗口为空导致偶发返回空列表
        var orders = await adapter.FetchOrdersAsync(new ChannelFetchQuery(1, DateTime.UtcNow.AddDays(-1)));

        orders.Should().ContainSingle().Which.ExternalOrderNo.Should().Be("TBREAL003");
        calls.Should().Be(2, "限流错误应重试一次");
    }

    [Fact]
    public async Task PushShipment_MapsChineseCarrierToTopCompanyCode()
    {
        var handler = new StubHandler(_ => Ok("""{"logistics_offline_send_response":{"success":true}}"""));
        var adapter = Create(LiveOptions(), handler);

        var result = await adapter.PushShipmentAsync(new ChannelShipmentInfo("TBREAL001", "SF123456", "顺丰速运"));

        result.Success.Should().BeTrue();
        var body = handler.RequestBodies.Should().ContainSingle().Subject;
        body.Should().Contain("method=taobao.logistics.offline.send");
        body.Should().Contain("company_code=SF");
        body.Should().Contain("out_sid=SF123456");
        body.Should().Contain("tid=TBREAL001");
    }

    [Theory]
    [InlineData("中通快递", "ZTO")]
    [InlineData("圆通速递", "YTO")]
    [InlineData("韵达快递", "YUNDA")]
    [InlineData("EMS", "EMS")]
    public async Task PushShipment_MapsKnownCarriers(string carrier, string expectedCode)
    {
        var handler = new StubHandler(_ => Ok("""{"logistics_offline_send_response":{"success":true}}"""));
        var adapter = Create(LiveOptions(), handler);

        await adapter.PushShipmentAsync(new ChannelShipmentInfo("TBREAL001", "NO1", carrier));

        handler.RequestBodies.Single().Should().Contain($"company_code={expectedCode}");
    }

    [Fact]
    public async Task PushShipment_UnknownCarrier_ReturnsFailureWithoutCallingPlatform()
    {
        var handler = new StubHandler(_ => Ok("{}"));
        var adapter = Create(LiveOptions(), handler);

        var result = await adapter.PushShipmentAsync(new ChannelShipmentInfo("TBREAL001", "NO1", "某未知快递"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("物流公司");
        handler.RequestBodies.Should().BeEmpty();
    }

    [Fact]
    public async Task PushStock_PostsQuantityUpdateForEachSku()
    {
        var handler = new StubHandler(_ => Ok("""{"item_quantity_update_response":{"item":{"num_iid":"123","quantity":50}}}"""));
        var adapter = Create(LiveOptions(), handler);

        var result = await adapter.PushStockAsync(
            [new ChannelStockSyncItem("123", 50), new ChannelStockSyncItem("456", 20)]);

        result.Success.Should().BeTrue();
        handler.RequestBodies.Should().HaveCount(2);
        handler.RequestBodies[0].Should().Contain("method=taobao.item.quantity.update");
        handler.RequestBodies[0].Should().Contain("num_iid=123");
        handler.RequestBodies[0].Should().Contain("quantity=50");
    }

    [Fact]
    public async Task SimulatedMode_PushShipmentAndStock_AreRecordedForDiagnostics()
    {
        var adapter = Create(new TaobaoOptions());

        var shipment = await adapter.PushShipmentAsync(new ChannelShipmentInfo("SIMTB-2026-0001", "SF0001", "顺丰速运"));
        var stock = await adapter.PushStockAsync([new ChannelStockSyncItem("TB-SKU-001", 66)]);

        shipment.Success.Should().BeTrue();
        stock.Success.Should().BeTrue();
        adapter.PushedShipments.Should().ContainKey("SIMTB-2026-0001");
        adapter.PushedStocks.Values.Should().ContainSingle().Which.Should().ContainSingle()
            .Which.Should().Be(new ChannelStockSyncItem("TB-SKU-001", 66));
    }

    // ── helpers ──
    private static TaobaoOptions LiveOptions() => new()
    {
        AppKey = "key", AppSecret = Secret, Session = "token", RetryDelayMs = 1,
    };

    private static TaobaoAdapter Create(TaobaoOptions options, StubHandler? handler = null) =>
        new(new StubHttpClientFactory(handler ?? new StubHandler(_ => Ok("{}"))),
            Options.Create(options), NullLogger<TaobaoAdapter>.Instance);

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static string TradePage(string tid, bool hasNext) => """
        {"trades_sold_get_response":{"total_results":1,"has_next":__HAS_NEXT__,"trades":{"trade":[
          {"tid":"__TID__","status":"WAIT_SELLER_SEND_GOODS","buyer_nick":"买家甲","receiver_name":"张三丰",
           "receiver_mobile":"13800000001","receiver_state":"浙江省","receiver_city":"杭州市","receiver_district":"西湖区",
           "receiver_address":"文三路 100 号","total_fee":"3560.00","payment":"3560.00",
           "created":"2026-11-20 09:30:00","pay_time":"2026-11-20 09:35:00",
           "orders":{"order":[{"oid":"O1","title":"全棉卫衣面料","price":"44.50","num":80,
           "outer_sku_id":"TB-SKU-001","sku_properties_name":"280g/蓝色"}]}}
        ]}}}
        """
        .Replace("__HAS_NEXT__", hasNext ? "true" : "false").Replace("__TID__", tid);

    private static string TwoTradePage(string tid, bool hasNext) => """
        {"trades_sold_get_response":{"total_results":2,"has_next":__HAS_NEXT__,"trades":{"trade":[
          {"tid":"__TID__","status":"WAIT_SELLER_SEND_GOODS","receiver_name":"李四","receiver_mobile":"13900000002",
           "receiver_address":"上海市浦东新区","total_fee":"100.00","payment":"100.00",
           "created":"2026-11-21 10:00:00","orders":{"order":[{"title":"商品","price":"100.00","num":1}]}}
        ]}}}
        """
        .Replace("__HAS_NEXT__", hasNext ? "true" : "false").Replace("__TID__", tid);

    private sealed class StubHandler(Func<string, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            RequestBodies.Add(body);
            return responder(body);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("https://gw.api.taobao.com") };
    }
}
