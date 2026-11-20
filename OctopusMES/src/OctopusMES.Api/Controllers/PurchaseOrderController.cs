using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/purchase")]
public class PurchaseOrderController(PurchaseOrderService svc, PortalIdentityService portalIdentity) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] long? supplierId, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        // P16 行级过滤：门户供应商强制只看本人采购单，忽略前端传入的 supplierId（防越权）
        var boundId = await portalIdentity.GetSupplierIdAsync(UserId);
        if (boundId.HasValue) supplierId = boundId.Value;

        var data = await svc.GetListAsync(status, supplierId, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data is null) return Ok(new { code = 404, msg = "采购单不存在", data = (object?)null });

        var boundId = await portalIdentity.GetSupplierIdAsync(UserId);
        if (boundId.HasValue && data.SupplierId != boundId.Value)
            return Ok(new { code = 404, msg = "采购单不存在", data = (object?)null });

        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MesPurchaseOrder req)
    {
        var data = await svc.CreateAsync(req, UserId);
        return Ok(new { code = 200, msg = "创建成功", data });
    }

    [HttpPut("{id}/submit")]
    public async Task<IActionResult> Submit(long id)
    {
        var data = await svc.SubmitAsync(id);
        if (data is null) return Ok(new { code = 400, msg = "操作失败", data = (object?)null });
        return Ok(new { code = 200, msg = "提交成功", data });
    }

    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(long id)
    {
        var data = await svc.ApproveAsync(id);
        if (data is null) return Ok(new { code = 400, msg = "操作失败", data = (object?)null });
        return Ok(new { code = 200, msg = "审批通过", data });
    }

    [HttpPut("{id}/reject")]
    public async Task<IActionResult> Reject(long id)
    {
        var data = await svc.RejectAsync(id);
        if (data is null) return Ok(new { code = 400, msg = "操作失败", data = (object?)null });
        return Ok(new { code = 200, msg = "已驳回", data });
    }
}
