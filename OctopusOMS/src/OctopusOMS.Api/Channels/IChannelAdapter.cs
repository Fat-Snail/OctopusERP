namespace OctopusOMS.Api.Channels;

/// <summary>渠道拉单查询条件</summary>
public record ChannelFetchQuery(long ShopId, DateTime? Since);

/// <summary>渠道订单（适配器统一归一化形态）</summary>
public record ChannelOrderDto(
    string ExternalOrderNo,
    DateTime OrderDate,
    string BuyerName,
    string? BuyerPhone,
    string? ShipAddress,
    decimal TotalAmount,
    decimal PaidAmount,
    string Currency,
    IReadOnlyList<ChannelOrderItemDto> Items);

public record ChannelOrderItemDto(
    string ChannelSkuCode,
    string ProductName,
    string? Spec,
    decimal Quantity,
    decimal UnitPrice);

/// <summary>发货回传信息</summary>
public record ChannelShipmentInfo(string ExternalOrderNo, string TrackingNo, string Carrier);

public record ChannelShipmentResult(bool Success, string? Message);

public record ChannelStockSyncItem(string ChannelSkuCode, int Quantity);

/// <summary>
/// P2 渠道适配器契约：统一各电商平台的「拉单 / 发货回传 / 库存回写」能力面。
/// 真实平台实现（淘宝/京东/拼多多…）在开放平台凭证到位后按此契约填充；
/// MockChannelAdapter 提供确定性沙箱（演示与集成测试）。
/// </summary>
public interface IChannelAdapter
{
    /// <summary>对应的 OmsChannel.ChannelCode（如 MOCK / TAOBAO / JD）</summary>
    string ChannelCode { get; }

    /// <summary>拉取渠道订单（归一化为 ChannelOrderDto）</summary>
    Task<IReadOnlyList<ChannelOrderDto>> FetchOrdersAsync(ChannelFetchQuery query, CancellationToken ct = default);

    /// <summary>发货回传（物流单号同步到渠道平台）</summary>
    Task<ChannelShipmentResult> PushShipmentAsync(ChannelShipmentInfo shipment, CancellationToken ct = default);

    /// <summary>库存回写（渠道商品可售数量）。适配器不支持时默认返回成功空操作。</summary>
    Task<ChannelShipmentResult> PushStockAsync(IReadOnlyList<ChannelStockSyncItem> items, CancellationToken ct = default)
        => Task.FromResult(new ChannelShipmentResult(true, "stock sync not supported by this adapter"));
}
