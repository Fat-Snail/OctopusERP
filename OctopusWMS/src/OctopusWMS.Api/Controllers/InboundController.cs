using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusWMS.Api.Persistence;
using OctopusWMS.Api.Services;
using System.Security.Claims;

namespace OctopusWMS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/inbound")]
public class InboundController(InboundService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var data = await svc.GetListAsync(status, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data is null) return Ok(new { code = 404, msg = "入库单不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WmsInboundOrder req)
    {
        var data = await svc.CreateAsync(req, UserId);
        return Ok(new { code = 200, msg = "创建成功", data });
    }

    [HttpPut("{id}/receive")]
    public async Task<IActionResult> Receive(long id, [FromBody] List<ReceiveItemRequest> receipts)
    {
        var data = await svc.ReceiveAsync(id, receipts.Select(r => (r.ItemId, r.ReceivedQty, r.LocationId)).ToList());
        if (data is null) return Ok(new { code = 400, msg = "操作失败", data = (object?)null });
        return Ok(new { code = 200, msg = "收货成功", data });
    }
}

public record ReceiveItemRequest(long ItemId, decimal ReceivedQty, long? LocationId);
