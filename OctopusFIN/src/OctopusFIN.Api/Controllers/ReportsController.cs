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

    /// <summary>Q3-③ 月结报告：期间凭证统计 + 损益汇总 + 结转凭证 + 科目余额快照</summary>
    [HttpGet("month-end-closing/{periodId:long}")]
    public async Task<IActionResult> MonthEndClosing(long periodId, [FromServices] ClosingService closingSvc)
    {
        var (data, error) = await closingSvc.GetMonthEndReportAsync(periodId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }
}
