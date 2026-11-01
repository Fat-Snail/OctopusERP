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

    [HttpGet("plm-sync")]
    public async Task<IActionResult> PlmSync()
    {
        var data = await svc.GetPlmSyncMetricsAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>P12-3：工单时效统计</summary>
    [HttpGet("wo-efficiency")]
    public async Task<IActionResult> WoEfficiency()
    {
        var data = await svc.GetWoEfficiencyAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>P12-3：工单趋势（按月）</summary>
    [HttpGet("wo-trend")]
    public async Task<IActionResult> WoTrend([FromQuery] int months = 6)
    {
        var data = await svc.GetWoTrendAsync(months);
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>P12-3：设备/工序负载</summary>
    [HttpGet("capacity-load")]
    public async Task<IActionResult> CapacityLoad()
    {
        var data = await svc.GetCapacityLoadAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>P12-3：瓶颈识别（按节拍偏差比）</summary>
    [HttpGet("bottleneck")]
    public async Task<IActionResult> Bottleneck()
    {
        var data = await svc.GetBottleneckAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>P12-3：操作工合格率排名</summary>
    [HttpGet("operator-ranking")]
    public async Task<IActionResult> OperatorRanking()
    {
        var data = await svc.GetOperatorRankingAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>P12-3：不良品 Pareto（按工序聚合 + 累计占比）</summary>
    [HttpGet("defect-pareto")]
    public async Task<IActionResult> DefectPareto()
    {
        var data = await svc.GetDefectParetoAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }
}
