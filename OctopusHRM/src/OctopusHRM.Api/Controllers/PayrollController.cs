using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusHRM.Api.Filters;
using OctopusHRM.Api.Services;

namespace OctopusHRM.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/payroll")]
public class PayrollController(PayrollService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var data = await svc.GetRunsAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetRun(long id)
    {
        var data = await svc.GetRunAsync(id);
        if (data == null) return Ok(new { code = 404, msg = "算薪批次不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost("start")]
    [OperationLog("启动算薪")]
    public async Task<IActionResult> Start([FromBody] StartPayrollRequest req)
    {
        var (data, error) = await svc.StartRunAsync(req.Year, req.Month, UserId);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已启动", data });
    }

    [HttpPut("{id:long}/calculate")]
    [OperationLog("计算薪资")]
    public async Task<IActionResult> Calculate(long id)
    {
        var (data, error) = await svc.CalculateRunAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已计算", data });
    }

    [HttpPut("{id:long}/approve-pay")]
    [OperationLog("核准发薪")]
    public async Task<IActionResult> ApprovePay(long id)
    {
        var (data, error) = await svc.ApprovePayAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已发薪（FIN 凭证已生成）", data });
    }
}

public record StartPayrollRequest(int Year, int Month);
