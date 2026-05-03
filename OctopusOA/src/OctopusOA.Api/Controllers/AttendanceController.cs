using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Services;
using System.Security.Claims;

namespace OctopusOA.Api.Controllers;

/// <summary>考勤打卡接口</summary>
[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _service;

    public AttendanceController(AttendanceService service) => _service = service;

    private long GetCurrentUserId() =>
        long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>上班打卡</summary>
    [HttpPost("check-in")]
    public IActionResult CheckIn()
    {
        var (record, err) = _service.CheckIn(GetCurrentUserId(), GetClientIp());
        if (err != null) return Ok(new { code = 500, msg = err });
        return Ok(new { code = 200, msg = "打卡成功", data = record });
    }

    /// <summary>下班打卡</summary>
    [HttpPost("check-out")]
    public IActionResult CheckOut()
    {
        var (record, err) = _service.CheckOut(GetCurrentUserId(), GetClientIp());
        if (err != null) return Ok(new { code = 500, msg = err });
        return Ok(new { code = 200, msg = "打卡成功", data = record });
    }

    /// <summary>今日打卡状态</summary>
    [HttpGet("today")]
    public IActionResult GetToday()
    {
        return Ok(new { code = 200, msg = "ok", data = _service.GetToday(GetCurrentUserId()) });
    }

    /// <summary>我的月度考勤</summary>
    [HttpGet("mine")]
    public IActionResult Mine([FromQuery] string? month)
    {
        var list = _service.GetMine(GetCurrentUserId(), month);
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>考勤规则</summary>
    [HttpGet("rule")]
    public IActionResult GetRule()
    {
        var r = _service.GetRule();
        return Ok(new
        {
            code = 200, msg = "ok",
            data = new AttendanceRuleDto
            {
                Id = r.Id, Name = r.Name,
                WorkStartTime = r.WorkStartTime, WorkEndTime = r.WorkEndTime,
                LateThresholdMin = r.LateThresholdMin, EarlyLeaveThresholdMin = r.EarlyLeaveThresholdMin,
                IpWhiteList = r.IpWhiteList, Status = r.Status,
            }
        });
    }

    /// <summary>修改考勤规则</summary>
    [HttpPut("rule")]
    public IActionResult UpdateRule([FromBody] AttendanceRuleDto dto)
    {
        _service.UpdateRule(dto);
        return Ok(new { code = 200, msg = "修改成功" });
    }

    /// <summary>月度统计</summary>
    [HttpGet("stats")]
    public IActionResult GetStats([FromQuery] string? month)
    {
        var data = _service.GetStats(month);
        return Ok(new { code = 200, msg = "ok", data = new { rows = data, total = data.Count } });
    }

    /// <summary>异常考勤列表</summary>
    [HttpGet("abnormal")]
    public IActionResult GetAbnormal([FromQuery] string? month)
    {
        var data = _service.GetAbnormal(month);
        return Ok(new { code = 200, msg = "ok", data = new { rows = data, total = data.Count } });
    }

    // ═══ 班次管理 ═══

    /// <summary>班次列表</summary>
    [HttpGet("shift/list")]
    public IActionResult GetShiftList()
    {
        var list = _service.GetAllShifts().Select(s => new AttendanceRuleDto
        {
            Id = s.Id, Code = s.Code, Name = s.Name,
            WorkStartTime = s.WorkStartTime, WorkEndTime = s.WorkEndTime,
            LateThresholdMin = s.LateThresholdMin,
            EarlyLeaveThresholdMin = s.EarlyLeaveThresholdMin,
            IpWhiteList = s.IpWhiteList, IsDefault = s.IsDefault, Status = s.Status,
        }).ToList();
        return Ok(new { code = 200, msg = "ok", data = list });
    }

    /// <summary>新建班次</summary>
    [HttpPost("shift")]
    public IActionResult CreateShift([FromBody] AttendanceRuleDto dto)
    {
        var (shift, err) = _service.CreateShift(dto);
        if (err != null) return Ok(new { code = 500, msg = err });
        return Ok(new { code = 200, msg = "创建成功", data = shift });
    }

    /// <summary>修改班次</summary>
    [HttpPut("shift")]
    public IActionResult UpdateShift([FromBody] AttendanceRuleDto dto)
    {
        var err = _service.UpdateShift(dto);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "修改成功" });
    }

    /// <summary>删除班次</summary>
    [HttpDelete("shift/{id:long}")]
    public IActionResult DeleteShift(long id)
    {
        var err = _service.DeleteShift(id);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "删除成功" });
    }

    /// <summary>设为默认班次</summary>
    [HttpPut("shift/{id:long}/default")]
    public IActionResult SetDefaultShift(long id)
    {
        var err = _service.SetDefaultShift(id);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "已设为默认班次" });
    }

    /// <summary>所有用户班次分配情况</summary>
    [HttpGet("user-shift/list")]
    public IActionResult GetUserShifts()
    {
        var list = _service.GetAllUserShifts();
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>分配用户班次</summary>
    [HttpPut("user-shift")]
    public IActionResult AssignUserShift([FromBody] AssignUserShiftRequest req)
    {
        var err = _service.AssignUserShift(req.UmcUserId, req.ShiftId);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "分配成功" });
    }
}
