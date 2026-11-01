using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OctopusOMS.Api.Persistence;

namespace OctopusOMS.Api.Services;

public class OrderService(OmsDbContext db, IHttpClientFactory httpFactory, IConfiguration config, ILogger<OrderService> logger)
{
    public async Task<(List<OmsOrder> rows, int total)> GetListAsync(long? shopId, string? status, int pageNum, int pageSize)
    {
        var q = db.Orders.Include(x => x.Items).AsQueryable();
        if (shopId.HasValue) q = q.Where(x => x.ShopId == shopId.Value);
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(x => x.OrderDate).Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows, total);
    }

    public async Task<OmsOrder?> GetByIdAsync(long id) =>
        await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.OrderId == id);

    /// <summary>从渠道平台拉单（或人工录入）</summary>
    public async Task<(OmsOrder? order, string? error)> CreateAsync(CreateOrderInput input, long createdBy)
    {
        var shop = await db.Shops.FirstOrDefaultAsync(x => x.ShopId == input.ShopId);
        if (shop == null) return (null, "店铺不存在");
        if (input.Items == null || input.Items.Count == 0) return (null, "明细不能为空");

        // 幂等：(ShopId, ExternalOrderNo) 唯一
        if (!string.IsNullOrEmpty(input.ExternalOrderNo))
        {
            var existing = await db.Orders.FirstOrDefaultAsync(x => x.ShopId == input.ShopId && x.ExternalOrderNo == input.ExternalOrderNo);
            if (existing != null) return (existing, null);
        }

        // 自动 SKU 映射
        var channelId = shop.ChannelId;
        var skuCodes = input.Items.Select(i => i.ChannelSkuCode).Distinct().ToList();
        var mappings = await db.SkuMappings
            .Where(m => m.ChannelId == channelId && skuCodes.Contains(m.ChannelSkuCode))
            .ToDictionaryAsync(m => m.ChannelSkuCode, m => m.PlmProductId);

        var order = new OmsOrder
        {
            OrderNo = $"OMS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            ShopId = input.ShopId,
            ChannelId = channelId,
            ExternalOrderNo = input.ExternalOrderNo ?? "",
            CustomerName = input.CustomerName ?? "未知",
            CustomerPhone = input.CustomerPhone,
            ShipAddress = input.ShipAddress,
            TotalAmount = input.Items.Sum(i => i.Quantity * i.UnitPrice),
            PaidAmount = 0,
            Currency = shop.Currency ?? "CNY",
            Status = "created",
            OrderDate = input.OrderDate ?? DateTime.UtcNow,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        foreach (var it in input.Items)
        {
            mappings.TryGetValue(it.ChannelSkuCode, out var plmProductId);
            order.Items.Add(new OmsOrderItem
            {
                ChannelSkuCode = it.ChannelSkuCode,
                PlmProductId = plmProductId == 0 ? null : plmProductId,
                ProductName = it.ProductName,
                Spec = it.Spec,
                Quantity = it.Quantity,
                UnitPrice = it.UnitPrice,
                Amount = it.Quantity * it.UnitPrice,
            });
        }
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (order, null);
    }

    /// <summary>支付完成（通常由渠道 webhook 触发）</summary>
    public async Task<(OmsOrder? order, string? error)> MarkPaidAsync(long orderId, decimal amount)
    {
        var order = await db.Orders.FindAsync(orderId);
        if (order == null) return (null, "订单不存在");
        if (order.Status == "cancelled" || order.Status == "refunded") return (null, "已取消/已退款的订单不能支付");

        order.PaidAmount = amount;
        order.Status = "paid";
        order.PaidAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (order, null);
    }

    /// <summary>下推 WMS 出库 — 改为 processing 状态</summary>
    public async Task<(OmsOrder? order, string? error)> PushToWmsAsync(long orderId)
    {
        var order = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.OrderId == orderId);
        if (order == null) return (null, "订单不存在");
        if (order.Status != "paid") return (null, $"订单状态 {order.Status} 不能下推 WMS");

        order.Status = "processing";
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Fire-and-forget WMS webhook
        _ = Task.Run(async () =>
        {
            try
            {
                var baseUrl = config["Webhooks:WmsBaseUrl"];
                if (string.IsNullOrEmpty(baseUrl)) return;
                var secret = config["Webhooks:WmsSecret"] ?? "";

                var payload = new
                {
                    OmsOrderId = order.OrderId,
                    OrderNo = order.OrderNo,
                    Recipient = order.CustomerName,
                    ShipAddress = order.ShipAddress,
                    Items = order.Items.Select(it => new
                    {
                        it.PlmProductId,
                        it.ProductName,
                        it.Spec,
                        Quantity = it.Quantity,
                    }),
                };
                var json = JsonSerializer.Serialize(payload);
                using var client = httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/webhook/oms-create-outbound")
                { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                if (!string.IsNullOrEmpty(secret))
                {
                    var sig = ComputeHmac(json, secret);
                    req.Headers.Add("X-Wms-Signature", $"sha256={sig}");
                }
                await client.SendAsync(req);
            }
            catch (Exception ex) { logger.LogWarning(ex, "OMS → WMS webhook 失败"); }
        });

        return (order, null);
    }

    /// <summary>WMS 发货回调 → 标记 shipped</summary>
    public async Task<(OmsOrder? order, string? error)> MarkShippedAsync(long orderId, long? wmsOutboundOrderId)
    {
        var order = await db.Orders.FindAsync(orderId);
        if (order == null) return (null, "订单不存在");
        order.Status = "shipped";
        order.ShippedAt = DateTime.UtcNow;
        order.WmsOutboundOrderId = wmsOutboundOrderId;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (order, null);
    }

    public async Task<(OmsOrder? order, string? error)> CancelAsync(long orderId, string reason)
    {
        var order = await db.Orders.FindAsync(orderId);
        if (order == null) return (null, "订单不存在");
        if (order.Status is "shipped" or "completed") return (null, "已发货/已完成的订单不能直接取消");
        order.Status = "cancelled";
        order.Remark = reason;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (order, null);
    }

    private static string ComputeHmac(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        return Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(key, data)).ToLowerInvariant();
    }
}

public record CreateOrderInput(
    long ShopId,
    string? ExternalOrderNo,
    string? CustomerName,
    string? CustomerPhone,
    string? ShipAddress,
    DateTime? OrderDate,
    List<CreateOrderItemInput> Items);

public record CreateOrderItemInput(
    string ChannelSkuCode,
    string ProductName,
    string? Spec,
    decimal Quantity,
    decimal UnitPrice);
