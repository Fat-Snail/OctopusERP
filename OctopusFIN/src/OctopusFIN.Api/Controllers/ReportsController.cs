using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Services;

namespace OctopusFIN.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportsController(ReportsService svc) : ControllerBase
{
    /// <summary>试算平衡表（Trial Balance）</summary>
    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance([FromQuery] long? periodId)
    {
        var data = await svc.GetTrialBalanceAsync(periodId);
        return Ok(new { code = 200, msg = "ok", data });
    }
}
