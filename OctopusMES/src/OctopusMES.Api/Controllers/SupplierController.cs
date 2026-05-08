using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/supplier")]
public class SupplierController(SupplierService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? keyword, [FromQuery] string? status, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var data = await svc.GetListAsync(keyword, status, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data is null) return Ok(new { code = 404, msg = "供应商不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MesSupplier req)
    {
        var data = await svc.CreateAsync(req);
        return Ok(new { code = 200, msg = "创建成功", data });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] MesSupplier req)
    {
        var data = await svc.UpdateAsync(req);
        if (data is null) return Ok(new { code = 404, msg = "供应商不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var ok = await svc.DeleteAsync(id);
        return Ok(new { code = ok ? 200 : 404, msg = ok ? "删除成功" : "供应商不存在", data = (object?)null });
    }
}
