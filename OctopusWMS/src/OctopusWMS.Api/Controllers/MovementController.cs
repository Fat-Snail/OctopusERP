using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusWMS.Api.Filters;
using OctopusWMS.Api.Services;

namespace OctopusWMS.Api.Controllers;

/// <summary>P11：库内移库 + 跨仓调拨</summary>
[Authorize]
[ApiController]
public class MovementController(MovementService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpPost("api/inventory/move")]
    [OperationLog("库内移库")]
    public async Task<IActionResult> Move([FromBody] MoveRequest req)
    {
        var (data, error) = await svc.MoveAsync(req.InventoryId, req.ToLocationId, req.Quantity);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "移库成功", data });
    }

    [HttpGet("api/transfer")]
    public async Task<IActionResult> GetTransfers([FromQuery] string? status)
    {
        var data = await svc.GetListAsync(status);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost("api/transfer")]
    [OperationLog("创建跨仓调拨")]
    public async Task<IActionResult> CreateTransfer([FromBody] CreateTransferRequest req)
    {
        if (req.Quantity <= 0) return Ok(new { code = 400, msg = "数量必须大于 0" });
        if (req.FromWarehouseId == req.ToWarehouseId) return Ok(new { code = 400, msg = "源仓与目标仓不能相同" });

        var data = await svc.CreateTransferAsync(req.FromWarehouseId, req.ToWarehouseId, req.PlmProductId,
            req.ProductName, req.Quantity, UserId, req.Remark);
        return Ok(new { code = 200, msg = "调拨单已创建", data });
    }

    [HttpPut("api/transfer/{id:long}/ship")]
    [OperationLog("调拨发货")]
    public async Task<IActionResult> ShipTransfer(long id)
    {
        var (data, error) = await svc.ShipTransferAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已发货", data });
    }

    [HttpPut("api/transfer/{id:long}/receive")]
    [OperationLog("调拨收货")]
    public async Task<IActionResult> ReceiveTransfer(long id)
    {
        var (data, error) = await svc.ReceiveTransferAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已收货", data });
    }
}

public record MoveRequest(long InventoryId, long ToLocationId, decimal Quantity);

public record CreateTransferRequest(
    long FromWarehouseId,
    long ToWarehouseId,
    long? PlmProductId,
    string ProductName,
    decimal Quantity,
    string? Remark);
