using Microsoft.EntityFrameworkCore;
using OctopusOMS.Api.Persistence;

namespace OctopusOMS.Api.Channels;

/// <summary>渠道拉单结果（幂等：已存在的 ExternalOrderNo 计入 Skipped）</summary>
public record ChannelSyncOutcome(
    string ChannelCode,
    int Fetched,
    int Created,
    int Skipped,
    List<string> CreatedExternalOrderNos);

public record ChannelShipmentPushOutcome(OmsOrder Order);

/// <summary>
/// P2 渠道同步编排：拉单幂等入库（DB 唯一索引 ShopId+ExternalOrderNo 兜底）+ 发货回传。
/// 发货回传先走适配器（渠道平台确认），成功后本地状态机置 shipped。
/// </summary>
public class ChannelSyncService(OmsDbContext db, ChannelAdapterFactory factory, ILogger<ChannelSyncService> logger)
{
    /// <summary>从渠道拉单并幂等入库。SKU 映射未配置的明细 PlmProductId 置空（人工映射后补）。</summary>
    public async Task<ChannelSyncOutcome> SyncOrdersAsync(long shopId, DateTime? since, CancellationToken ct = default)
    {
        var shop = await db.Shops.Include(s => s.Channel)
            .FirstOrDefaultAsync(s => s.ShopId == shopId, ct)
            ?? throw new InvalidOperationException($"店铺 {shopId} 不存在");

        var adapter = factory.GetAdapter(shop.Channel!.ChannelCode);
        var fetched = await adapter.FetchOrdersAsync(new ChannelFetchQuery(shopId, since), ct);

        int created = 0, skipped = 0;
        var createdNos = new List<string>();

        foreach (var o in fetched)
        {
            var exists = await db.Orders.AnyAsync(
                x => x.ShopId == shopId && x.ExternalOrderNo == o.ExternalOrderNo, ct);
            if (exists)
            {
                skipped++;
                continue;
            }

            var order = new OmsOrder
            {
                OrderNo = $"OMS-{shop.ShopCode}-{o.ExternalOrderNo}",
                ShopId = shopId,
                ChannelId = shop.ChannelId,
                ExternalOrderNo = o.ExternalOrderNo,
                CustomerName = o.BuyerName,
                CustomerPhone = o.BuyerPhone,
                ShipAddress = o.ShipAddress,
                TotalAmount = o.TotalAmount,
                PaidAmount = o.PaidAmount,
                Currency = o.Currency,
                Status = o.TotalAmount > 0 && o.PaidAmount >= o.TotalAmount ? "paid" : "created",
                OrderDate = o.OrderDate,
                PaidAt = o.TotalAmount > 0 && o.PaidAmount >= o.TotalAmount ? o.OrderDate : null,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            foreach (var i in o.Items)
            {
                var mapping = await db.SkuMappings.FirstOrDefaultAsync(
                    m => m.ChannelId == shop.ChannelId && m.ChannelSkuCode == i.ChannelSkuCode, ct);
                order.Items.Add(new OmsOrderItem
                {
                    ChannelSkuCode = i.ChannelSkuCode,
                    PlmProductId = mapping?.PlmProductId,
                    ProductName = i.ProductName,
                    Spec = i.Spec,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Amount = i.Quantity * i.UnitPrice,
                });
            }

            db.Orders.Add(order);
            await db.SaveChangesAsync(ct); // 逐单落库：并发场景由唯一索引兜底
            created++;
            createdNos.Add(o.ExternalOrderNo);
        }

        logger.LogInformation("[channel-sync] {ChannelCode} shop={ShopId}: fetched={Fetched} created={Created} skipped={Skipped}",
            shop.Channel!.ChannelCode, shopId, fetched.Count, created, skipped);
        return new ChannelSyncOutcome(shop.Channel.ChannelCode, fetched.Count, created, skipped, createdNos);
    }

    /// <summary>发货回传：先通知渠道平台（适配器），成功后本地置 shipped。</summary>
    public async Task<(OmsOrder? order, string? error)> PushShipmentAsync(
        long orderId, string trackingNo, string carrier, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.Shop).ThenInclude(s => s!.Channel)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order == null) return (null, "订单不存在");
        if (order.Status is not ("paid" or "processing"))
            return (null, $"订单状态 {order.Status} 不可发货（仅 paid/processing 可回传发货）");

        var adapter = factory.GetAdapter(order.Shop!.Channel!.ChannelCode);
        var push = await adapter.PushShipmentAsync(
            new ChannelShipmentInfo(order.ExternalOrderNo, trackingNo, carrier), ct);
        if (!push.Success) return (null, $"渠道回传失败：{push.Message}");

        order.Status = "shipped";
        order.ShippedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("[channel-sync] shipment pushed: order={OrderNo} tracking={Tracking}",
            order.ExternalOrderNo, trackingNo);
        return (order, null);
    }

    /// <summary>库存回写（渠道可售数量同步），适配器不支持时返回友好提示。</summary>
    public async Task<(bool Success, string Message)> PushStockAsync(
        long shopId, IReadOnlyList<ChannelStockSyncItem> items, CancellationToken ct = default)
    {
        var shop = await db.Shops.Include(s => s.Channel)
            .FirstOrDefaultAsync(s => s.ShopId == shopId, ct)
            ?? throw new InvalidOperationException($"店铺 {shopId} 不存在");
        var adapter = factory.GetAdapter(shop.Channel!.ChannelCode);
        var result = await adapter.PushStockAsync(items, ct);
        return (result.Success, result.Message ?? "");
    }
}
