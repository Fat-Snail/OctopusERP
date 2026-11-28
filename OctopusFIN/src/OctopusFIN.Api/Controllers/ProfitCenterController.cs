using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Filters;
using OctopusFIN.Api.Services;

namespace OctopusFIN.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/profit-center")]
public class ProfitCenterController(ProfitCenterService svc) : ControllerBase
{
    /// <summary>利润中心列表（分页 + 按组织筛选）</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? organizationId, [FromQuery] string? status,
        [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var (rows, total) = await svc.ListAsync(organizationId, status, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data == null) return Ok(new { code = 404, msg = "利润中心不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    [OperationLog("创建利润中心")]
    public async Task<IActionResult> Create([FromBody] ProfitCenterInput req)
    {
        var (data, error) = await svc.CreateAsync(req);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "利润中心已创建", data });
    }

    [HttpPut("{id:long}")]
    [OperationLog("更新利润中心")]
    public async Task<IActionResult> Update(long id, [FromBody] ProfitCenterInput req)
    {
        var (data, error) = await svc.UpdateAsync(id, req);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "利润中心已更新", data });
    }

    [HttpDelete("{id:long}")]
    [OperationLog("删除利润中心")]
    public async Task<IActionResult> Delete(long id)
    {
        var (ok, error) = await svc.DeleteAsync(id);
        if (!ok) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "利润中心已删除", data = (object?)null });
    }

    // ===== 组织 =====

    [HttpGet("orgs")]
    public async Task<IActionResult> ListOrgs(
        [FromQuery] string? status, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var (rows, total) = await svc.ListOrgsAsync(status, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpPost("orgs")]
    [OperationLog("创建组织")]
    public async Task<IActionResult> CreateOrg([FromBody] OrgInput req)
    {
        var (data, error) = await svc.CreateOrgAsync(req);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "组织已创建", data });
    }
}
