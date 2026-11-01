using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOMS.Api.Filters;
using OctopusOMS.Api.Services;

namespace OctopusOMS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/order")]
public class OrderController(OrderService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? shopId, [FromQuery] string? status,
        [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var (rows, total) = await svc.GetListAsync(shopId, status, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data == null) return Ok(new { code = 404, msg = "订单不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    [OperationLog("创建订单")]
    public async Task<IActionResult> Create([FromBody] CreateOrderInput req)
    {
        var (data, error) = await svc.CreateAsync(req, UserId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已创建", data });
    }

    [HttpPut("{id:long}/pay")]
    [OperationLog("订单支付")]
    public async Task<IActionResult> Pay(long id, [FromBody] PayRequest req)
    {
        var (data, error) = await svc.MarkPaidAsync(id, req.Amount);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已支付", data });
    }

    [HttpPut("{id:long}/push-wms")]
    [OperationLog("下推 WMS")]
    public async Task<IActionResult> PushWms(long id)
    {
        var (data, error) = await svc.PushToWmsAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已下推", data });
    }

    [HttpPut("{id:long}/cancel")]
    [OperationLog("取消订单")]
    public async Task<IActionResult> Cancel(long id, [FromBody] CancelRequest req)
    {
        var (data, error) = await svc.CancelAsync(id, req.Reason ?? "");
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已取消", data });
    }
}

public record PayRequest(decimal Amount);
public record CancelRequest(string? Reason);
