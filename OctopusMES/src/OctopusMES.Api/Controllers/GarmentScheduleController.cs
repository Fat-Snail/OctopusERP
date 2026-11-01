using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/garment-schedule")]
public class GarmentScheduleController(GarmentScheduleService svc) : ControllerBase
{
    /// <summary>日历视图：[startDate, endDate] 范围内所有组的排程矩阵</summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string? season)
    {
        if (endDate < startDate) return Ok(new { code = 400, msg = "endDate 必须 >= startDate", data = (object?)null });
        if ((endDate - startDate).TotalDays > 366)
            return Ok(new { code = 400, msg = "日期范围不得超过 366 天", data = (object?)null });
        var data = await svc.GetCalendarAsync(startDate.Date, endDate.Date, season);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("teams")]
    public async Task<IActionResult> Teams()
    {
        var data = await svc.ListTeamsAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] string? season,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] long? teamId,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 20)
    {
        var data = await svc.ListSchedulesAsync(keyword, season, from, to, teamId, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var data = await svc.GetByIdAsync(id);
        if (data is null) return Ok(new { code = 404, msg = "排程不存在", data = (object?)null });
        return Ok(new { code = 200, msg = "ok", data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MesGarmentSchedule req)
    {
        var userId = long.TryParse(User.FindFirstValue("sub"), out var u) ? u : 0;
        var (data, err) = await svc.CreateAsync(req, userId);
        if (err != null) return Ok(new { code = 403, msg = err, data = (object?)data });
        return Ok(new { code = 200, msg = "保存成功", data });
    }

    /// <summary>归档概览：按年→月聚合（早于 ArchiveCutoff 的数据）</summary>
    [HttpGet("archive/summary")]
    public async Task<IActionResult> ArchiveSummary()
    {
        var data = await svc.GetArchiveSummaryAsync();
        return Ok(new { code = 200, msg = "ok", data });
    }

    /// <summary>批量创建：日历多选天数后一次保存到指定组的多个日期</summary>
    [HttpPost("batch")]
    public async Task<IActionResult> BatchCreate([FromBody] GarmentScheduleService.BatchCreateRequest req)
    {
        var userId = long.TryParse(User.FindFirstValue("sub"), out var u) ? u : 0;
        var (created, updated, schedules, err) = await svc.BatchCreateAsync(req, userId);
        if (err != null) return Ok(new { code = 400, msg = err, data = (object?)null });
        return Ok(new
        {
            code = 200,
            msg = $"批量保存成功：新增 {created} 条，更新 {updated} 条",
            data = new
            {
                created,
                updated,
                total = created + updated,
                scheduleIds = schedules.Select(s => s.ScheduleId).ToList(),
            }
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] MesGarmentSchedule req)
    {
        var (data, err) = await svc.UpdateAsync(req);
        if (data is null) return Ok(new { code = 404, msg = err ?? "排程不存在", data = (object?)null });
        if (err != null) return Ok(new { code = 403, msg = err, data });
        return Ok(new { code = 200, msg = "更新成功", data });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var (ok, err) = await svc.DeleteAsync(id);
        if (ok) return Ok(new { code = 200, msg = "删除成功", data = (object?)null });
        var code = err != null && err.Contains("已归档") ? 403 : 404;
        return Ok(new { code, msg = err ?? "排程不存在", data = (object?)null });
    }

    /// <summary>月度汇总：对应汇总 sheet（组 × 月份的车缝/裁床/預算）</summary>
    [HttpGet("monthly-summary")]
    public async Task<IActionResult> MonthlySummary([FromQuery] string? season)
    {
        var data = await svc.GetMonthlySummaryAsync(season);
        return Ok(new { code = 200, msg = "ok", data });
    }

    public record PublishRequest(long OaApprovalId);

    /// <summary>发布排程：drafts → pending_approval (OA 审批中)</summary>
    [HttpPost("{id}/publish")]
    public async Task<IActionResult> Publish(long id, [FromBody] PublishRequest req)
    {
        var (s, err) = await svc.PublishAsync(id, req.OaApprovalId);
        if (s == null) return Ok(new { code = 404, msg = err, data = (object?)null });
        if (err != null) return Ok(new { code = 400, msg = err, data = s });
        return Ok(new { code = 200, msg = "已发布，等待审批", data = s });
    }
}
