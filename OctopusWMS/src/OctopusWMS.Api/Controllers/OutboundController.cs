using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusWMS.Api.Persistence;
using OctopusWMS.Api.Services;

namespace OctopusWMS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/outbound")]
public class OutboundController(OutboundService svc) : ControllerBase
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
        if (data is null) return Ok(new { code = 404, msg = "出库单不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WmsOutboundOrder req)
    {
        var data = await svc.CreateAsync(req, UserId);
        return Ok(new { code = 200, msg = "创建成功", data });
    }

    [HttpPut("{id}/ship")]
    public async Task<IActionResult> Ship(long id)
    {
        var data = await svc.ShipAsync(id);
        if (data is null) return Ok(new { code = 400, msg = "操作失败", data = (object?)null });
        return Ok(new { code = 200, msg = "出库成功", data });
    }
}
