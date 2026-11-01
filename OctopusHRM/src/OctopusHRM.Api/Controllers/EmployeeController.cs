using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusHRM.Api.Filters;
using OctopusHRM.Api.Persistence;
using OctopusHRM.Api.Services;

namespace OctopusHRM.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/employee")]
public class EmployeeController(EmployeeService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? keyword)
    {
        var data = await svc.GetListAsync(status, keyword);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data == null) return Ok(new { code = 404, msg = "员工不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    [OperationLog("新增员工")]
    public async Task<IActionResult> Create([FromBody] HrmEmployee req)
    {
        var (data, error) = await svc.CreateAsync(req);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "创建成功", data });
    }

    [HttpPut("{id:long}/resign")]
    [OperationLog("员工离职")]
    public async Task<IActionResult> Resign(long id, [FromBody] ResignRequest req)
    {
        var (data, error) = await svc.ResignAsync(id, req.ResignedAt);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "已办理离职", data });
    }
}

public record ResignRequest(DateTime ResignedAt);
