using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusWMS.Api.Services;

namespace OctopusWMS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/stats")]
public class StatsController(WmsStatsService svc) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var data = await svc.GetSummaryAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }
}
