using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/production-orders")]
public sealed class ProductionOrderController(ProductionOrderService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MesProductionOrder request)
    {
        try { return Ok(new { code = 200, msg = "生产订单已创建", data = await service.CreateAsync(request, 0) }); }
        catch (InvalidOperationException ex) { return Ok(new { code = 400, msg = ex.Message, data = (object?)null }); }
    }

    [HttpPost("from-snapshot")]
    public async Task<IActionResult> CreateFromSnapshot([FromBody] CreateProductionOrderRequest request)
    {
        try { return Ok(new { code = 200, msg = "生产订单已创建", data = await service.CreateFromSnapshotAsync(request, 0) }); }
        catch (InvalidOperationException ex) { return Ok(new { code = 400, msg = ex.Message, data = (object?)null }); }
    }

    public sealed record SwitchSnapshotRequest(long SnapshotId, string Reason);

    [HttpPost("{id:long}/switch-snapshot")]
    [Authorize(Policy = "ProductionOrderVersionSwitch")]
    public async Task<IActionResult> SwitchSnapshot(long id, [FromBody] SwitchSnapshotRequest request)
    {
        try { await service.SwitchSnapshotAsync(id, request.SnapshotId, request.Reason, 0); return Ok(new { code = 200, msg = "生产订单制造版本已切换" }); }
        catch (InvalidOperationException ex) { return Ok(new { code = 400, msg = ex.Message }); }
    }

    [HttpPost("{id:long}/explode")]
    public async Task<IActionResult> Explode(long id)
    {
        try { return Ok(new { code = 200, msg = "工序展开成功", data = await service.ExplodeOperationsAsync(id) }); }
        catch (InvalidOperationException ex) { return Ok(new { code = 400, msg = ex.Message, data = (object?)null }); }
    }
}
