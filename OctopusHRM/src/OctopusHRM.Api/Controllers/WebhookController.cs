using Microsoft.AspNetCore.Mvc;
using OctopusHRM.Api.Services;

namespace OctopusHRM.Api.Controllers;

/// <summary>OA 入职 → HRM 镜像员工档案；offer 接受 → 自动建档</summary>
[ApiController]
[Route("api/webhook")]
public class WebhookController(EmployeeService svc) : ControllerBase
{
    [HttpPost("oa/employee-hired")]
    public async Task<IActionResult> OnEmployeeHired([FromBody] OaEmployeeHiredRequest req)
    {
        var (emp, err) = await svc.CreateFromOaAsync(req.OaEmployeeId, req.UmcUserId,
            req.FullName ?? "未知", req.Department, req.Position, req.Phone, req.Email);
        if (err != null) return Ok(new { code = 400, msg = err, data = (object?)null });
        return Ok(new { code = 200, msg = "员工档案已创建", data = emp });
    }
}

public record OaEmployeeHiredRequest(
    long OaEmployeeId,
    long? UmcUserId,
    string? FullName,
    string? Department,
    string? Position,
    string? Phone,
    string? Email);
