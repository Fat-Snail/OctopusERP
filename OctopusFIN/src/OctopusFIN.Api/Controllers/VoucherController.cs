using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Filters;
using OctopusFIN.Api.Services;

namespace OctopusFIN.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/voucher")]
public class VoucherController(VoucherService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? periodId, [FromQuery] string? status, [FromQuery] string? source,
        [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var (rows, total) = await svc.GetListAsync(periodId, status, source, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data == null) return Ok(new { code = 404, msg = "凭证不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    [OperationLog("录入凭证")]
    public async Task<IActionResult> Create([FromBody] VoucherInput req)
    {
        var (data, error) = await svc.CreateAsync(req, UserId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "凭证已创建（草稿）", data });
    }

    [HttpPut("{id:long}/post")]
    [OperationLog("过账")]
    public async Task<IActionResult> Post(long id)
    {
        var (data, error) = await svc.PostAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已过账", data });
    }

    [HttpPut("{id:long}/void")]
    [OperationLog("作废凭证")]
    public async Task<IActionResult> Void(long id)
    {
        var (data, error) = await svc.VoidAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已作废", data });
    }
}
