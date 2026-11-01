using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Filters;
using OctopusFIN.Api.Persistence;
using OctopusFIN.Api.Services;

namespace OctopusFIN.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/account")]
public class AccountController(AccountService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? type, [FromQuery] string? keyword)
    {
        var data = await svc.GetListAsync(type, keyword);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data == null) return Ok(new { code = 404, msg = "科目不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    [OperationLog("新增科目")]
    public async Task<IActionResult> Create([FromBody] FinAccount req)
    {
        var (data, error) = await svc.CreateAsync(req);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "创建成功", data });
    }

    [HttpPut("{id:long}")]
    [OperationLog("修改科目")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateAccountRequest req)
    {
        var (data, error) = await svc.UpdateAsync(id, req.AccountName, req.Remark, req.Status);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data });
    }
}

public record UpdateAccountRequest(string? AccountName, string? Remark, string? Status);
