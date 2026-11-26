using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OctopusOMS.Api.Channels;

/// <summary>
/// 淘宝/天猫开放平台（TOP）渠道适配器：以 IChannelAdapter 契约接入 OMS 拉单 / 发货回传 / 库存回写。
/// 凭证缺失时自动使用确定性仿真订单，便于无开放平台账号时跑通全链路；凭证到位后仅在配置中填入
/// AppKey / AppSecret / Session 即切换为真实 TOP API 调用（无需改代码）。
/// </summary>
public class TaobaoAdapter(
    IHttpClientFactory httpClientFactory,
    IOptions<TaobaoOptions> options,
    ILogger<TaobaoAdapter> logger) : IChannelAdapter
{
    public const string Code = "TAOBAO";

    private const string TradeFields =
        "tid,status,buyer_nick,receiver_name,receiver_mobile,receiver_phone," +
        "receiver_state,receiver_city,receiver_district,receiver_address," +
        "total_fee,payment,created,pay_time,orders";

    private static readonly TimeSpan Window = TimeSpan.FromDays(7);

    private readonly IHttpClientFactory _http = httpClientFactory;
    private readonly TaobaoOptions _opt = options.Value;
    private readonly ConcurrentDictionary<string, ChannelShipmentInfo> _pushedShipments = new();
    private readonly ConcurrentDictionary<string, List<ChannelStockSyncItem>> _pushedStocks = new();

    public string ChannelCode => Code;

    /// <summary>live = 调用真实 TOP API；simulated = 无凭证/强制仿真</summary>
    public bool IsSimulated => _opt.Simulated || !_opt.HasCredentials;

    public string Mode => IsSimulated ? "simulated" : "live";

    /// <summary>仿真模式已回传的发货记录（诊断/测试用）</summary>
    public IReadOnlyDictionary<string, ChannelShipmentInfo> PushedShipments =>
        new Dictionary<string, ChannelShipmentInfo>(_pushedShipments);

    /// <summary>仿真模式已回传的库存批次（诊断/测试用）</summary>
    public IReadOnlyDictionary<string, List<ChannelStockSyncItem>> PushedStocks =>
        new Dictionary<string, List<ChannelStockSyncItem>>(_pushedStocks);

    /// <summary>仿真模式的确定性订单（与 Mock 沙箱同样的可重复数据集，含一笔未付款用于拒绝路径）</summary>
    private static readonly List<ChannelOrderDto> SimulatedOrders =
    [
        new("SIMTB-2026-0001", new DateTime(2026, 11, 20, 9, 30, 0, DateTimeKind.Utc),
            "淘宝买家甲", "138****0001", "浙江省杭州市西湖区文三路 1 号",
            3560m, 3560m, "CNY",
            [new ChannelOrderItemDto("TB-SKU-001", "全棉卫衣面料", "280g/蓝色", 80, 44.5m)]),
        new("SIMTB-2026-0002", new DateTime(2026, 11, 21, 14, 10, 0, DateTimeKind.Utc),
            "淘宝买家乙", "139****0002", "广东省广州市天河区沙箱大道 88 号",
            9240m, 9240m, "CNY",
            [
                new ChannelOrderItemDto("TB-SKU-002", "涤纶尼龙面料", "210T/黑色", 120, 77m),
                new ChannelOrderItemDto("TB-SKU-003", "针织罗纹袖口", "2x2/白色", 300, 8m),
            ]),
        new("SIMTB-2026-0003", new DateTime(2026, 11, 22, 10, 5, 0, DateTimeKind.Utc),
            "淘宝买家丙", "137****0003", "四川省成都市高新区仿真街 5 号",
            1280m, 0m, "CNY",
            [new ChannelOrderItemDto("TB-SKU-004", "针织T恤坯布", "32支/白色", 40, 32m)]),
    ];

    public Task<IReadOnlyList<ChannelOrderDto>> FetchOrdersAsync(ChannelFetchQuery query, CancellationToken ct = default)
    {
        if (IsSimulated)
        {
            IReadOnlyList<ChannelOrderDto> result = SimulatedOrders
                .Where(o => query.Since == null || o.OrderDate >= query.Since.Value)
                .ToList();
            logger.LogInformation("[taobao-adapter] simulated fetch: {Count} orders (shop {ShopId}, since {Since})",
                result.Count, query.ShopId, query.Since);
            return Task.FromResult(result);
        }

        return FetchLiveOrdersAsync(query, ct);
    }

    private async Task<IReadOnlyList<ChannelOrderDto>> FetchLiveOrdersAsync(ChannelFetchQuery query, CancellationToken ct)
    {
        var from = query.Since ?? DateTime.UtcNow - TimeSpan.FromDays(7);
        var to = DateTime.UtcNow;
        var result = new List<ChannelOrderDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // TOP 查询有时间跨度限制，按 7 天窗口分片；跨窗口边界的 tid 去重
        for (var windowStart = from; windowStart < to; windowStart += Window)
        {
            var windowEnd = windowStart + Window < to ? windowStart + Window : to;

            for (var page = 1; page <= _opt.MaxPagesPerWindow; page++)
            {
                var p = CommonParams("taobao.trades.sold.get");
                p["fields"] = TradeFields;
                p["start_created"] = FormatTime(windowStart);
                p["end_created"] = FormatTime(windowEnd);
                p["page_no"] = page.ToString(CultureInfo.InvariantCulture);
                p["page_size"] = _opt.PageSize.ToString(CultureInfo.InvariantCulture);
                p["use_has_next"] = "true";

                using var doc = await CallTopAsync("taobao.trades.sold.get", p, ct);
                var response = doc.RootElement.GetProperty("trades_sold_get_response");

                if (response.TryGetProperty("trades", out var trades) &&
                    trades.TryGetProperty("trade", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var trade in arr.EnumerateArray())
                    {
                        var dto = ParseTrade(trade);
                        if (seen.Add(dto.ExternalOrderNo)) result.Add(dto);
                    }
                }

                var hasNext = response.TryGetProperty("has_next", out var hn) && hn.ValueKind == JsonValueKind.True;
                if (!hasNext) break;
            }
        }

        logger.LogInformation("[taobao-adapter] live fetch: {Count} orders (shop {ShopId}, from {From})",
            result.Count, query.ShopId, from);
        return result;
    }

    public async Task<ChannelShipmentResult> PushShipmentAsync(ChannelShipmentInfo shipment, CancellationToken ct = default)
    {
        if (IsSimulated)
        {
            _pushedShipments[shipment.ExternalOrderNo] = shipment;
            logger.LogInformation("[taobao-adapter] simulated shipment: {OrderNo} {Tracking}",
                shipment.ExternalOrderNo, shipment.TrackingNo);
            return new ChannelShipmentResult(true, "simulated shipment recorded");
        }

        var companyCode = MapCarrier(shipment.Carrier);
        if (companyCode == null)
            return new ChannelShipmentResult(false,
                $"物流公司 '{shipment.Carrier}' 无法映射到淘宝 company_code，请使用标准快递名称或补充映射");

        var p = CommonParams("taobao.logistics.offline.send");
        p["tid"] = shipment.ExternalOrderNo;
        p["company_code"] = companyCode;
        p["out_sid"] = shipment.TrackingNo;

        using var doc = await CallTopAsync("taobao.logistics.offline.send", p, ct);
        logger.LogInformation("[taobao-adapter] shipment pushed: {OrderNo} carrier={Carrier}({Code})",
            shipment.ExternalOrderNo, shipment.Carrier, companyCode);
        return new ChannelShipmentResult(true, $"shipment pushed to taobao ({companyCode})");
    }

    public async Task<ChannelShipmentResult> PushStockAsync(IReadOnlyList<ChannelStockSyncItem> items, CancellationToken ct = default)
    {
        if (IsSimulated)
        {
            var batch = $"stock-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            _pushedStocks[batch] = items.ToList();
            logger.LogInformation("[taobao-adapter] simulated stock: batch={Batch} items={Count}", batch, items.Count);
            return new ChannelShipmentResult(true, $"simulated stock recorded ({items.Count} items)");
        }

        var failures = new List<string>();
        foreach (var item in items)
        {
            var p = CommonParams("taobao.item.quantity.update");
            p["num_iid"] = item.ChannelSkuCode;
            p["quantity"] = item.Quantity.ToString(CultureInfo.InvariantCulture);
            try
            {
                using var doc = await CallTopAsync("taobao.item.quantity.update", p, ct);
            }
            catch (TaobaoApiException ex)
            {
                failures.Add($"{item.ChannelSkuCode}: {ex.Message}");
            }
        }

        return failures.Count == 0
            ? new ChannelShipmentResult(true, $"stock pushed to taobao ({items.Count} items)")
            : new ChannelShipmentResult(false, string.Join("; ", failures));
    }

    // ── TOP 调用 ──
    private Dictionary<string, string> CommonParams(string method) => new()
    {
        ["method"] = method,
        ["app_key"] = _opt.AppKey,
        ["session"] = _opt.Session,
        ["format"] = "json",
        ["v"] = "2.0",
        ["sign_method"] = "md5",
        ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
    };

    private async Task<JsonDocument> CallTopAsync(string method, Dictionary<string, string> parameters, CancellationToken ct)
    {
        parameters["sign"] = TaobaoSigner.Sign(parameters, _opt.AppSecret);

        for (var attempt = 0; ; attempt++)
        {
            using var content = new FormUrlEncodedContent(parameters);
            using var response = await _http.CreateClient(nameof(TaobaoAdapter)).PostAsync(_opt.ApiUrl, content, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new TaobaoApiException($"淘宝开放平台 HTTP {(int)response.StatusCode}: {Trim(json)}");

            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("error_response", out var err)) return doc;

            var code = GetStr(err, "code") ?? "";
            var subCode = GetStr(err, "sub_code") ?? "";
            var msg = GetStr(err, "sub_msg") ?? GetStr(err, "msg") ?? "淘宝开放平台返回错误";

            if (attempt == 0 && IsThrottling(subCode, msg))
            {
                doc.Dispose();
                logger.LogWarning("[taobao-adapter] throttled on {Method} ({SubCode}: {Msg}), retrying once", method, subCode, msg);
                await Task.Delay(_opt.RetryDelayMs, ct);
                continue;
            }

            doc.Dispose();
            throw new TaobaoApiException(msg, code, subCode);
        }
    }

    private static bool IsThrottling(string subCode, string msg) =>
        subCode.Contains("THROTTL", StringComparison.OrdinalIgnoreCase) ||
        subCode.Contains("RATE_LIMIT", StringComparison.OrdinalIgnoreCase) ||
        subCode.Contains("FLOW_LIMIT", StringComparison.OrdinalIgnoreCase) ||
        msg.Contains("频繁", StringComparison.Ordinal) ||
        msg.Contains("限流", StringComparison.Ordinal) ||
        msg.Contains("too many", StringComparison.OrdinalIgnoreCase);

    // ── 响应归一化 ──
    private static ChannelOrderDto ParseTrade(JsonElement trade)
    {
        var tid = GetStr(trade, "tid") ?? GetStr(trade, "oid") ?? Guid.NewGuid().ToString("N");
        var date = ParseDate(GetStr(trade, "created") ?? GetStr(trade, "pay_time")) ?? DateTime.UtcNow;
        var buyer = MaskName(GetStr(trade, "receiver_name") ?? GetStr(trade, "buyer_nick") ?? string.Empty);
        var phone = MaskPhone(GetStr(trade, "receiver_mobile") ?? GetStr(trade, "receiver_phone"));
        var address = string.Concat(new[]
        {
            GetStr(trade, "receiver_state"), GetStr(trade, "receiver_city"),
            GetStr(trade, "receiver_district"), GetStr(trade, "receiver_address"),
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var total = ParseDecimal(GetStr(trade, "total_fee") ?? GetStr(trade, "payment")) ?? 0m;
        var paid = ParseDecimal(GetStr(trade, "payment")) ?? 0m;

        var items = new List<ChannelOrderItemDto>();
        if (trade.TryGetProperty("orders", out var orders) &&
            orders.TryGetProperty("order", out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var i in arr.EnumerateArray())
            {
                items.Add(new ChannelOrderItemDto(
                    GetStr(i, "outer_sku_id") ?? GetStr(i, "sku_id") ?? GetStr(i, "iid") ?? GetStr(i, "oid") ?? string.Empty,
                    GetStr(i, "title") ?? string.Empty,
                    GetStr(i, "sku_properties_name") ?? GetStr(i, "properties") ?? string.Empty,
                    ParseDecimal(GetStr(i, "num")) ?? 0m,
                    ParseDecimal(GetStr(i, "price")) ?? 0m));
            }
        }

        return new ChannelOrderDto(tid, date, buyer, phone, address, total, paid, "CNY", items);
    }

    private static string MaskName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        return name.Length == 1 ? "*" : name[..1] + new string('*', name.Length - 1);
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7) return phone;
        return phone[..3] + "****" + phone[^4..];
    }

    private static string FormatTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static DateTime? ParseDate(string? value) =>
        value != null && DateTime.TryParseExact(value,
            ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static decimal? ParseDecimal(string? value) =>
        value != null && decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static string? GetStr(JsonElement element, string name) =>
        element.TryGetProperty(name, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null,
            }
            : null;

    private static string Trim(string json) => json.Length <= 200 ? json : json[..200] + "…";

    /// <summary>中文/英文快递名 → 淘宝物流公司编码</summary>
    private static readonly Dictionary<string, string> CarrierCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["顺丰"] = "SF", ["顺丰速运"] = "SF", ["SF"] = "SF",
        ["中通"] = "ZTO", ["中通快递"] = "ZTO", ["ZTO"] = "ZTO",
        ["圆通"] = "YTO", ["圆通速递"] = "YTO", ["YTO"] = "YTO",
        ["韵达"] = "YUNDA", ["韵达快递"] = "YUNDA", ["YUNDA"] = "YUNDA",
        ["申通"] = "STO", ["申通快递"] = "STO", ["STO"] = "STO",
        ["京东"] = "JD", ["京东物流"] = "JD", ["JD"] = "JD",
        ["EMS"] = "EMS", ["邮政"] = "EMS",
        ["德邦"] = "DBL", ["德邦快递"] = "DBL", ["DBL"] = "DBL",
        ["极兔"] = "JTEX", ["极兔速递"] = "JTEX", ["JTEX"] = "JTEX",
    };

    private static string? MapCarrier(string carrier) =>
        CarrierCodes.TryGetValue(carrier.Trim(), out var code) ? code : null;
}
