using Microsoft.AspNetCore.Mvc;
using OctopusOMS.Api.Services;

namespace OctopusOMS.Api.Controllers;

/// <summary>WMS 发货回调 → 标记订单 shipped</summary>
[ApiController]
[Route("api/webhook")]
public class WebhookController(OrderService svc) : ControllerBase
{
    [HttpPost("wms/shipped")]
    public async Task<IActionResult> OnShipped([FromBody] WmsShippedRequest req)
    {
        var (data, error) = await svc.MarkShippedAsync(req.OmsOrderId, req.WmsOutboundOrderId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已标记发货", data });
    }
}

public record WmsShippedRequest(long OmsOrderId, long? WmsOutboundOrderId);
