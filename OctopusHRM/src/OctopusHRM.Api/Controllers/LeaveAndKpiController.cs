using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusHRM.Api.Filters;
using OctopusHRM.Api.Persistence;
using OctopusHRM.Api.Services;

namespace OctopusHRM.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/leave")]
public class LeaveController(LeaveBalanceService svc) : ControllerBase
{
    [HttpGet("balance/{employeeId:long}")]
    public async Task<IActionResult> GetBalance(long employeeId, [FromQuery] int? year)
    {
        var data = await svc.GetByEmployeeAsync(employeeId, year);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPut("balance")]
    [OperationLog("调整假期余额")]
    public async Task<IActionResult> Adjust([FromBody] AdjustBalanceRequest req)
    {
        var (data, error) = await svc.AdjustAsync(req.EmployeeId, req.LeaveType, req.Year, req.TotalDays);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已调整", data });
    }

    /// <summary>OA 请假审批通过 → 扣减余额（webhook 入口）</summary>
    [HttpPost("consume")]
    [AllowAnonymous]
    [OperationLog("OA 请假扣减")]
    public async Task<IActionResult> Consume([FromBody] ConsumeLeaveRequest req)
    {
        var (data, error) = await svc.ConsumeFromOaAsync(req.EmployeeId, req.LeaveType, req.Year, req.Days);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已扣减", data });
    }
}

[Authorize]
[ApiController]
[Route("api/kpi")]
public class KpiController(KpiService svc) : ControllerBase
{
    [HttpGet("{employeeId:long}")]
    public async Task<IActionResult> List(long employeeId)
    {
        var data = await svc.GetByEmployeeAsync(employeeId);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    [OperationLog("创建 KPI")]
    public async Task<IActionResult> Create([FromBody] HrmKpi req)
    {
        var data = await svc.CreateAsync(req);
        return Ok(new { code = 200, msg = "已创建", data });
    }

    [HttpPut("{id:long}/submit")]
    [OperationLog("提交 KPI 评分")]
    public async Task<IActionResult> Submit(long id, [FromBody] SubmitKpiRequest req)
    {
        var reviewerId = long.TryParse(User.FindFirst("sub")?.Value, out var u) ? u : 1;
        var data = await svc.SubmitAsync(id, req.Score, req.Comment, reviewerId);
        if (data == null) return Ok(new { code = 404, msg = "KPI 不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "已提交", data });
    }
}

public record AdjustBalanceRequest(long EmployeeId, string LeaveType, int Year, decimal TotalDays);
public record ConsumeLeaveRequest(long EmployeeId, string LeaveType, int Year, decimal Days);
public record SubmitKpiRequest(decimal Score, string? Comment);
