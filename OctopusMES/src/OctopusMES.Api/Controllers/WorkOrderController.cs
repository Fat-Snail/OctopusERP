using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusMES.Api.Filters;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workorder")]
public class WorkOrderController(WorkOrderService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var data = await svc.GetListAsync(status, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data is null) return Ok(new { code = 404, msg = "工单不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MesWorkOrder req)
    {
        var data = await svc.CreateAsync(req, UserId);
        return Ok(new { code = 200, msg = "创建成功", data });
    }

    [HttpPut("{id}/start")]
    [OperationLog("开工")]
    public async Task<IActionResult> Start(long id)
    {
        var data = await svc.StartAsync(id);
        if (data is null) return Ok(new { code = 400, msg = "操作失败", data = (object?)null });
        return Ok(new { code = 200, msg = "开工成功", data });
    }

    [HttpPut("{id}/complete")]
    [OperationLog("完工")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteRequest req)
    {
        var data = await svc.CompleteAsync(id, req.CompletedQty);
        if (data is null) return Ok(new { code = 400, msg = "操作失败", data = (object?)null });
        return Ok(new { code = 200, msg = "完工成功", data });
    }

    [HttpPut("process/{processId}/status")]
    public async Task<IActionResult> UpdateProcess(long processId, [FromBody] UpdateProcessRequest req)
    {
        var data = await svc.UpdateProcessAsync(processId, req.Status);
        if (data is null) return Ok(new { code = 404, msg = "工序不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data });
    }

    /// <summary>P12：工序报工（PDA 扫码用）</summary>
    [HttpPost("process/{processId}/report")]
    [OperationLog("工序报工")]
    public async Task<IActionResult> ReportProcess(long processId, [FromBody] ReportProcessRequest req)
    {
        var input = new ReportProcessInput(
            req.QualifiedQty, req.DefectQty, req.ReworkQty,
            req.DowntimeMinutes, req.DowntimeReason,
            req.OperatorId, req.OperatorName, req.EquipmentId,
            req.ActualCycleSec, req.Finish);
        var data = await svc.ReportProcessAsync(processId, input);
        if (data is null) return Ok(new { code = 404, msg = "工序不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "报工成功", data });
    }
}

[Authorize]
[ApiController]
[Route("api/equipment")]
public class EquipmentController(WorkOrderService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var data = await svc.GetEquipmentsAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    [OperationLog("新增设备")]
    public async Task<IActionResult> Create([FromBody] MesEquipment req)
    {
        var data = await svc.CreateEquipmentAsync(req);
        return Ok(new { code = 200, msg = "创建成功", data });
    }

    [HttpPut("{id:long}/status")]
    [OperationLog("设备状态变更")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateEquipmentStatusRequest req)
    {
        var data = await svc.UpdateEquipmentStatusAsync(id, req.Status);
        if (data is null) return Ok(new { code = 404, msg = "设备不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "更新成功", data });
    }
}

public record CompleteRequest(decimal CompletedQty);
public record UpdateProcessRequest(string Status);
public record ReportProcessRequest(
    decimal QualifiedQty,
    decimal DefectQty,
    decimal ReworkQty,
    int DowntimeMinutes,
    string? DowntimeReason,
    long? OperatorId,
    string? OperatorName,
    long? EquipmentId,
    int? ActualCycleSec,
    bool Finish);
public record UpdateEquipmentStatusRequest(string Status);
