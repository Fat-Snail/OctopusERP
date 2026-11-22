using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOMS.Api.Channels;

namespace OctopusOMS.Api.Controllers;

/// <summary>P2 渠道同步端点：拉单入库 / 发货回传 / 库存回写 / Mock 沙箱查询</summary>
[Authorize]
[ApiController]
[Route("api/channel-sync")]
public class ChannelSyncController(ChannelSyncService syncService, ChannelAdapterFactory factory) : ControllerBase
{
    /// <summary>从渠道拉单并幂等入库（重复拉取自动跳过）</summary>
    [HttpPost("sync/{shopId:long}")]
    public async Task<IActionResult> SyncOrders(long shopId, [FromQuery] DateTime? since)
    {
        try
        {
            var outcome = await syncService.SyncOrdersAsync(shopId, since);
            return Ok(new
            {
                code = 200,
                msg = $"拉取 {outcome.Fetched} 笔：新建 {outcome.Created}，跳过 {outcome.Skipped}",
                data = outcome,
            });
        }
        catch (ChannelNotSupportedException ex)
        {
            return Ok(new { code = 400, msg = ex.Message, data = (object?)null });
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new { code = 400, msg = ex.Message, data = (object?)null });
        }
    }

    /// <summary>发货回传（渠道平台确认后本地置 shipped）</summary>
    [HttpPost("shipment/{orderId:long}")]
    public async Task<IActionResult> PushShipment(long orderId, [FromBody] PushShipmentRequest req)
    {
        var (order, error) = await syncService.PushShipmentAsync(orderId, req.TrackingNo, req.Carrier);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new
        {
            code = 200,
            msg = "发货回传成功，订单已置 shipped",
            data = new { order!.OrderId, order.OrderNo, order.ExternalOrderNo, order.Status, order.ShippedAt },
        });
    }

    /// <summary>库存回写（渠道可售数量同步）</summary>
    [HttpPost("stock/{shopId:long}")]
    public async Task<IActionResult> PushStock(long shopId, [FromBody] PushStockRequest req)
    {
        try
        {
            var (success, message) = await syncService.PushStockAsync(shopId, req.Items);
            return Ok(new { code = success ? 200 : 400, msg = message, data = (object?)null });
        }
        catch (ChannelNotSupportedException ex)
        {
            return Ok(new { code = 400, msg = ex.Message, data = (object?)null });
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new { code = 400, msg = ex.Message, data = (object?)null });
        }
    }

    /// <summary>Mock 沙箱：已回传的发货记录（测试/演示用）</summary>
    [HttpGet("mock/pushed-shipments")]
    public IActionResult MockPushedShipments()
    {
        var mock = factory.GetAll().OfType<MockChannelAdapter>().FirstOrDefault();
        if (mock == null) return Ok(new { code = 400, msg = "Mock 适配器未注册", data = (object?)null });
        return Ok(new
        {
            code = 200,
            msg = "ok",
            data = mock.PushedShipments.Select(kv => new
            {
                externalOrderNo = kv.Key,
                trackingNo = kv.Value.TrackingNo,
                carrier = kv.Value.Carrier,
            }).ToList(),
        });
    }

    public record PushShipmentRequest(string TrackingNo, string Carrier);
    public record PushStockRequest(IReadOnlyList<ChannelStockSyncItem> Items);
}
