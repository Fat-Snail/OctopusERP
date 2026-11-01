using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusWMS.Api.Persistence;
using OctopusWMS.Api.Services;

namespace OctopusWMS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/fabric-arrival")]
public class FabricArrivalController(FabricArrivalService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] long? mesScheduleId,
        [FromQuery] long? mesOrderId,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 20)
    {
        var data = await svc.ListAsync(status, mesScheduleId, mesOrderId, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var d = await svc.GetByIdAsync(id);
        if (d == null) return Ok(new { code = 404, msg = "回布单不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data = d });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WmsFabricArrival req)
    {
        var userId = long.TryParse(User.FindFirstValue("sub"), out var u) ? u : 0;
        var d = await svc.CreateAsync(req, userId);
        return Ok(new { code = 200, msg = "创建成功", data = d });
    }

    public record MarkArrivedRequest(decimal ReceivedQty, long WarehouseId);

    [HttpPost("{id}/mark-arrived")]
    public async Task<IActionResult> MarkArrived(long id, [FromBody] MarkArrivedRequest req)
    {
        var (a, err) = await svc.MarkArrivedAsync(id, req.ReceivedQty, req.WarehouseId);
        if (err != null && a == null)
            return Ok(new { code = 404, msg = err, data = (object?)null });
        return Ok(new { code = 200, msg = err ?? "ok", data = a });
    }
}
