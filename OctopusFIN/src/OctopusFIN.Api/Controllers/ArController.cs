using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Filters;
using OctopusFIN.Api.Services;

namespace OctopusFIN.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/ar")]
public class ArController(ArService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var data = await svc.GetListAsync(status);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data == null) return Ok(new { code = 404, msg = "应收发票不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost("{id:long}/receipt")]
    [OperationLog("登记收款")]
    public async Task<IActionResult> Receipt(long id, [FromBody] RegisterReceiptRequest req)
    {
        var (data, error) = await svc.RegisterReceiptAsync(id, req.Amount, req.PaymentMethod, req.BankReference, UserId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "收款已登记", data });
    }
}

public record RegisterReceiptRequest(decimal Amount, string? PaymentMethod, string? BankReference);
