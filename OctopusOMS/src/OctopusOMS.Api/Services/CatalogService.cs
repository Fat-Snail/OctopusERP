using Microsoft.EntityFrameworkCore;
using OctopusOMS.Api.Persistence;

namespace OctopusOMS.Api.Services;

public class CatalogService(OmsDbContext db)
{
    public async Task<List<OmsChannel>> GetChannelsAsync() =>
        await db.Channels.OrderBy(x => x.ChannelId).ToListAsync();

    public async Task<List<OmsShop>> GetShopsAsync(long? channelId = null)
    {
        var q = db.Shops.AsQueryable();
        if (channelId.HasValue) q = q.Where(x => x.ChannelId == channelId.Value);
        return await q.OrderBy(x => x.ShopId).ToListAsync();
    }

    public async Task<List<OmsSkuMapping>> GetMappingsAsync(long? channelId = null) =>
        await db.SkuMappings
            .Where(x => !channelId.HasValue || x.ChannelId == channelId.Value)
            .OrderBy(x => x.MappingId).ToListAsync();

    public async Task<(OmsSkuMapping? mapping, string? error)> CreateMappingAsync(long channelId, string channelSkuCode, long plmProductId, string? note)
    {
        if (string.IsNullOrEmpty(channelSkuCode)) return (null, "渠道 SKU 编码必填");
        if (await db.SkuMappings.AnyAsync(x => x.ChannelId == channelId && x.ChannelSkuCode == channelSkuCode))
            return (null, "该渠道下此 SKU 已映射");
        var m = new OmsSkuMapping
        {
            ChannelId = channelId, ChannelSkuCode = channelSkuCode,
            PlmProductId = plmProductId, Note = note, CreatedAt = DateTime.UtcNow,
        };
        db.SkuMappings.Add(m);
        await db.SaveChangesAsync();
        return (m, null);
    }
}

public class AfterSaleService(OmsDbContext db)
{
    public async Task<List<OmsAfterSale>> GetListAsync(string? status) =>
        await db.AfterSales
            .Where(x => string.IsNullOrEmpty(status) || x.Status == status)
            .OrderByDescending(x => x.CreatedAt).ToListAsync();

    public async Task<(OmsAfterSale? row, string? error)> CreateAsync(long orderId, string type, decimal amount, string reason, string? note, long createdBy)
    {
        var order = await db.Orders.FindAsync(orderId);
        if (order == null) return (null, "订单不存在");
        if (amount <= 0) return (null, "金额必须 > 0");
        if (amount > order.PaidAmount) return (null, $"金额不能超过已付款 {order.PaidAmount}");

        var row = new OmsAfterSale
        {
            AfterSaleNo = $"AS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            OrderId = orderId,
            AfterSaleType = type,
            Amount = amount,
            Reason = reason,
            CustomerNote = note,
            Status = "pending",
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };
        db.AfterSales.Add(row);
        await db.SaveChangesAsync();
        return (row, null);
    }

    public async Task<(OmsAfterSale? row, string? error)> ApproveAsync(long id, string? operatorNote)
    {
        var row = await db.AfterSales.FindAsync(id);
        if (row == null) return (null, "售后单不存在");
        if (row.Status != "pending") return (null, $"状态 {row.Status} 不能批准");
        row.Status = "approved";
        row.OperatorNote = operatorNote;
        await db.SaveChangesAsync();
        return (row, null);
    }

    public async Task<(OmsAfterSale? row, string? error)> CompleteAsync(long id)
    {
        var row = await db.AfterSales.FindAsync(id);
        if (row == null) return (null, "售后单不存在");
        if (row.Status != "approved") return (null, "仅已批准的售后可完成");
        row.Status = "completed";
        row.CompletedAt = DateTime.UtcNow;

        // 退款类售后 → 同步把订单标 refunded
        if (row.AfterSaleType == "refund")
        {
            var order = await db.Orders.FindAsync(row.OrderId);
            if (order != null && order.PaidAmount == row.Amount)
            {
                order.Status = "refunded";
                order.UpdatedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync();
        return (row, null);
    }
}
