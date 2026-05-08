using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusWMS.Api.Services;

namespace OctopusWMS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory")]
public class InventoryController(InventoryService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? warehouseId, [FromQuery] string? keyword, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var data = await svc.GetListAsync(warehouseId, keyword, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var data = await svc.GetSummaryAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock()
    {
        var data = await svc.GetLowStockAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPut("{id}/adjust")]
    public async Task<IActionResult> Adjust(long id, [FromBody] AdjustRequest req)
    {
        var data = await svc.AdjustAsync(id, req.Delta, req.Reason ?? "手动调整");
        if (data is null) return Ok(new { code = 404, msg = "库存记录不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "调整成功", data });
    }
}

public record AdjustRequest(decimal Delta, string? Reason);
