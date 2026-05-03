using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Services;
using System.Security.Claims;

namespace OctopusOA.Api.Controllers;

/// <summary>会议室预订接口</summary>
[ApiController]
[Route("api/meeting")]
[Authorize]
public class MeetingController : ControllerBase
{
    private readonly MeetingService _service;

    public MeetingController(MeetingService service) => _service = service;

    private (long userId, string userName) GetCurrentUser()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
        var name = User.FindFirst("name")?.Value ?? User.Identity?.Name ?? "unknown";
        return (long.Parse(sub), name);
    }

    // ── 会议室 ──

    /// <summary>会议室列表</summary>
    [HttpGet("room/list")]
    public IActionResult GetRoomList([FromQuery] int? status)
    {
        var list = _service.GetRoomList(status);
        return Ok(new { code = 200, msg = "ok", data = list });
    }

    /// <summary>会议室详情</summary>
    [HttpGet("room/{id:long}")]
    public IActionResult GetRoom(long id)
    {
        var room = _service.GetRoom(id);
        return room == null
            ? Ok(new { code = 404, msg = "会议室不存在" })
            : Ok(new { code = 200, msg = "ok", data = room });
    }

    /// <summary>新建会议室</summary>
    [HttpPost("room")]
    public IActionResult CreateRoom([FromBody] CreateMeetingRoomRequest req)
    {
        var room = _service.CreateRoom(req);
        return Ok(new { code = 200, msg = "创建成功", data = room });
    }

    /// <summary>修改会议室</summary>
    [HttpPut("room")]
    public IActionResult UpdateRoom([FromBody] UpdateMeetingRoomRequest req)
    {
        var err = _service.UpdateRoom(req);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "修改成功" });
    }

    /// <summary>删除会议室</summary>
    [HttpDelete("room/{id:long}")]
    public IActionResult DeleteRoom(long id)
    {
        var err = _service.DeleteRoom(id);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "删除成功" });
    }

    /// <summary>会议室日历（某房间的预订时段）</summary>
    [HttpGet("room/{id:long}/calendar")]
    public IActionResult GetRoomCalendar(long id, [FromQuery] string? date, [FromQuery] string? range)
    {
        // range = day / week，默认 week
        var (startUtc, endUtc) = ParseRange(date, range ?? "week");
        var list = _service.GetRoomCalendar(id, startUtc, endUtc);
        return Ok(new { code = 200, msg = "ok", data = list });
    }

    // ── 预订 ──

    /// <summary>预订</summary>
    [HttpPost("booking")]
    public IActionResult Book([FromBody] CreateBookingRequest req)
    {
        var (userId, userName) = GetCurrentUser();
        var (booking, err, code) = _service.Book(req, userId, userName);
        return err != null
            ? Ok(new { code, msg = err })
            : Ok(new { code = 200, msg = "预订成功", data = booking });
    }

    /// <summary>取消预订</summary>
    [HttpPut("booking/{id:long}/cancel")]
    public IActionResult Cancel(long id)
    {
        var (userId, _) = GetCurrentUser();
        var (err, code) = _service.Cancel(id, userId);
        return err != null ? Ok(new { code, msg = err }) : Ok(new { code = 200, msg = "已取消" });
    }

    /// <summary>我的预订</summary>
    [HttpGet("booking/mine")]
    public IActionResult Mine()
    {
        var (userId, _) = GetCurrentUser();
        var list = _service.GetMine(userId);
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>今日参与的预订（含我发起的 + 我被邀请的）</summary>
    [HttpGet("booking/today")]
    public IActionResult Today()
    {
        var (userId, _) = GetCurrentUser();
        var list = _service.GetToday(userId);
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>总览日历（所有会议室的预订）</summary>
    [HttpGet("calendar")]
    public IActionResult GetCalendar([FromQuery] string? date, [FromQuery] string? range)
    {
        var (startUtc, endUtc) = ParseRange(date, range ?? "week");
        var list = _service.GetAllCalendar(startUtc, endUtc);
        return Ok(new { code = 200, msg = "ok", data = list });
    }

    /// <summary>解析日期范围（北京时间日期 → UTC 起止）</summary>
    private static (DateTime startUtc, DateTime endUtc) ParseRange(string? date, string range)
    {
        var anchorBeijing = string.IsNullOrEmpty(date)
            ? DateTime.UtcNow.AddHours(8).Date
            : DateTime.Parse(date).Date;

        DateTime startBeijing, endBeijing;
        if (range == "day")
        {
            startBeijing = anchorBeijing;
            endBeijing = anchorBeijing.AddDays(1);
        }
        else // week：周一起始
        {
            var dow = ((int)anchorBeijing.DayOfWeek + 6) % 7;
            startBeijing = anchorBeijing.AddDays(-dow);
            endBeijing = startBeijing.AddDays(7);
        }
        return (startBeijing.AddHours(-8), endBeijing.AddHours(-8));
    }
}
