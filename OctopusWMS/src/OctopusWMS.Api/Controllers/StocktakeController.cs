using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusWMS.Api.Services;

namespace OctopusWMS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/stocktake")]
public class StocktakeController(StocktakeService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? warehouseId, [FromQuery] string? status, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var data = await svc.GetListAsync(warehouseId, status, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data is null) return Ok(new { code = 404, msg = "盘点任务不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStocktakeRequest req)
    {
        var data = await svc.CreateAsync(req.WarehouseId, req.Remark, UserId);
        return Ok(new { code = 200, msg = "创建成功", data });
    }

    [HttpPut("{id}/submit")]
    public async Task<IActionResult> Submit(long id, [FromBody] List<StocktakeResultItem> results)
    {
        var data = await svc.SubmitResultsAsync(id, results.Select(r => (r.ItemId, r.ActualQty)).ToList());
        if (data is null) return Ok(new { code = 400, msg = "操作失败", data = (object?)null });
        return Ok(new { code = 200, msg = "盘点完成", data });
    }
}

public record CreateStocktakeRequest(long WarehouseId, string? Remark);
public record StocktakeResultItem(long ItemId, decimal ActualQty);
