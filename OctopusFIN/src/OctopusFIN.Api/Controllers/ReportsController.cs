using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Services;

namespace OctopusFIN.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportsController(ReportsService svc) : ControllerBase
{
    /// <summary>试算平衡表（Trial Balance），支持按组织/利润中心维度过滤</summary>
    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance(
        [FromQuery] long? periodId, [FromQuery] long? organizationId, [FromQuery] long? profitCenterId)
    {
        var data = await svc.GetTrialBalanceAsync(periodId, organizationId, profitCenterId);
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>资产负债表，支持按组织/利润中心维度过滤</summary>
    [HttpGet("balance-sheet")]
    public async Task<IActionResult> BalanceSheet(
        [FromQuery] long? periodId, [FromQuery] long? organizationId, [FromQuery] long? profitCenterId)
    {
        var data = await svc.GetBalanceSheetAsync(periodId, organizationId, profitCenterId);
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>利润表（损益表），支持按组织/利润中心维度过滤</summary>
    [HttpGet("income-statement")]
    public async Task<IActionResult> IncomeStatement(
        [FromQuery] long? periodId, [FromQuery] long? organizationId, [FromQuery] long? profitCenterId)
    {
        var data = await svc.GetIncomeStatementAsync(periodId, organizationId, profitCenterId);
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>现金流量表（简化版：经营/投资/筹资），支持按组织/利润中心维度过滤</summary>
    [HttpGet("cash-flow")]
    public async Task<IActionResult> CashFlow(
        [FromQuery] long? periodId, [FromQuery] long? organizationId, [FromQuery] long? profitCenterId)
    {
        var data = await svc.GetCashFlowAsync(periodId, organizationId, profitCenterId);
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>阿米巴经营报表：按利润中心汇总收入/费用/利润</summary>
    [HttpGet("amoeba")]
    public async Task<IActionResult> Amoeba(
        [FromQuery] long? periodId, [FromQuery] long? organizationId)
    {
        var data = await svc.GetAmoebaReportAsync(periodId, organizationId);
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
