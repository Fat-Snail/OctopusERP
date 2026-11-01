using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Filters;
using OctopusFIN.Api.Services;

namespace OctopusFIN.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/period")]
public class PeriodController(PeriodService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var data = await svc.GetListAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current()
    {
        var data = await svc.GetCurrentAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPut("{id:long}/close")]
    [OperationLog("月结")]
    public async Task<IActionResult> Close(long id)
    {
        var (data, error) = await svc.CloseAsync(id, UserId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已关账", data });
    }

    [HttpPut("{id:long}/reopen")]
    [OperationLog("反结账")]
    public async Task<IActionResult> Reopen(long id)
    {
        var (data, error) = await svc.ReopenAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已重新打开", data });
    }
}
