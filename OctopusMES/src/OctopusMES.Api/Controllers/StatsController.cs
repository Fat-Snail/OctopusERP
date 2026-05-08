using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/stats")]
public class StatsController(MesStatsService svc) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var data = await svc.GetSummaryAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }
}
