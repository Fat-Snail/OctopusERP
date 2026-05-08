using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusCRM.Api.Services;

namespace OctopusCRM.Api.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize]
public class StatsController(StatsService statsService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await statsService.GetSummaryAsync();
        return Ok(new { code = 200, msg = "ok", data = summary });
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> GetPipeline()
    {
        var pipeline = await statsService.GetPipelineAsync();
        return Ok(new { code = 200, msg = "ok", data = pipeline });
    }

    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue()
    {
        var overdue = await statsService.GetOverdueAsync();
        return Ok(new { code = 200, msg = "ok", data = overdue });
    }

    [HttpGet("bi/efficiency")]
    public async Task<IActionResult> GetBiEfficiency()
    {
        var data = await statsService.GetEfficiencyAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("bi/otd-trend")]
    public async Task<IActionResult> GetOtdTrend([FromQuery] int months = 6)
    {
        var data = await statsService.GetOtdTrendAsync(months);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("bi/approval-backlog")]
    public async Task<IActionResult> GetApprovalBacklog()
    {
        var data = await statsService.GetApprovalBacklogAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("bi/contract-timeline/{id:long}")]
    public async Task<IActionResult> GetContractTimeline(long id)
    {
        var data = await statsService.GetContractTimelineAsync(id);
        if (data == null) return NotFound(new { code = 404, msg = "合同不存在" });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("bi/contracts")]
    public async Task<IActionResult> GetContractBriefList()
    {
        var data = await statsService.GetContractBriefListAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }
}
