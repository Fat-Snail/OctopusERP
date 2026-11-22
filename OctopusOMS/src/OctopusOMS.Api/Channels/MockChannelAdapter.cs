using System.Collections.Concurrent;

namespace OctopusOMS.Api.Channels;

/// <summary>
/// P2 Mock 沙箱渠道：确定性数据集 + 可查询的回传记录。
/// 真实渠道凭证到位前的演示/集成测试载体；PushedShipments 供测试断言与演示查询。
/// </summary>
public class MockChannelAdapter(ILogger<MockChannelAdapter> logger) : IChannelAdapter
{
    public const string Code = "MOCK";

    public string ChannelCode => Code;

    // 回传记录（发货 / 库存），测试与演示查询用
    private readonly ConcurrentDictionary<string, ChannelShipmentInfo> _pushedShipments = new();
    private readonly ConcurrentDictionary<string, List<ChannelStockSyncItem>> _pushedStocks = new();

    /// <summary>已回传的发货记录（externalOrderNo → 信息）</summary>
    public IReadOnlyDictionary<string, ChannelShipmentInfo> PushedShipments =>
        new ConcurrentDictionary<string, ChannelShipmentInfo>(_pushedShipments);

    /// <summary>已回传的库存记录（externalOrderNo 不适用，key 为批次号）</summary>
    public IReadOnlyDictionary<string, List<ChannelStockSyncItem>> PushedStocks =>
        new ConcurrentDictionary<string, List<ChannelStockSyncItem>>(_pushedStocks);

    // 内置 3 笔确定性沙箱订单：2 笔已支付、1 笔未支付（用于状态机拒绝路径）
    private static readonly List<ChannelOrderDto> SandboxOrders = new()
    {
        new(
            "MOCK-2026-0001", new DateTime(2026, 11, 20, 9, 30, 0, DateTimeKind.Utc),
            "沙箱买家-甲", "13800000001", "杭州市西湖区沙箱路 1 号",
            3560m, 3560m, "CNY",
            new List<ChannelOrderItemDto>
            {
                new("MOCK-SKU-001", "全棉卫衣面料", "280g/蓝色", 80, 44.5m),
            }),
        new(
            "MOCK-2026-0002", new DateTime(2026, 11, 20, 14, 10, 0, DateTimeKind.Utc),
            "沙箱买家-乙", "13800000002", "广州市天河区沙箱大道 88 号",
            9240m, 9240m, "CNY",
            new List<ChannelOrderItemDto>
            {
                new("MOCK-SKU-002", "涤纶尼龙面料", "210T/黑色", 120, 77m),
                new("MOCK-SKU-003", "针织罗纹袖口", "2x2/白色", 300, 8m),
            }),
        new(
            "MOCK-2026-0003", new DateTime(2026, 11, 21, 10, 5, 0, DateTimeKind.Utc),
            "沙箱买家-丙", "13800000003", "成都市高新区沙箱街 5 号",
            1280m, 0m, "CNY",
            new List<ChannelOrderItemDto>
            {
                new("MOCK-SKU-004", "针织T恤坯布", "32支/白色", 40, 32m),
            }),
    };

    public Task<IReadOnlyList<ChannelOrderDto>> FetchOrdersAsync(ChannelFetchQuery query, CancellationToken ct = default)
    {
        IReadOnlyList<ChannelOrderDto> result = SandboxOrders
            .Where(o => query.Since == null || o.OrderDate >= query.Since.Value)
            .ToList();
        logger.LogInformation("[mock-adapter] fetched {Count} sandbox orders (shop {ShopId}, since {Since})",
            result.Count, query.ShopId, query.Since);
        return Task.FromResult(result);
    }

    public Task<ChannelShipmentResult> PushShipmentAsync(ChannelShipmentInfo shipment, CancellationToken ct = default)
    {
        _pushedShipments[shipment.ExternalOrderNo] = shipment;
        logger.LogInformation("[mock-adapter] shipment pushed: {OrderNo} tracking={Tracking} carrier={Carrier}",
            shipment.ExternalOrderNo, shipment.TrackingNo, shipment.Carrier);
        return Task.FromResult(new ChannelShipmentResult(true, "mock shipment recorded"));
    }

    public Task<ChannelShipmentResult> PushStockAsync(IReadOnlyList<ChannelStockSyncItem> items, CancellationToken ct = default)
    {
        var batchKey = $"stock-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        _pushedStocks[batchKey] = items.ToList();
        logger.LogInformation("[mock-adapter] stock pushed: batch={Batch} items={Count}", batchKey, items.Count);
        return Task.FromResult(new ChannelShipmentResult(true, $"mock stock recorded ({items.Count} items)"));
    }
}
