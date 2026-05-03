using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.Services;
using System.Security.Claims;

namespace OctopusOA.Api.Controllers;

/// <summary>公告中心（所有登录用户可见）</summary>
[ApiController]
[Route("api/notice")]
[Authorize]
public class NoticeController : ControllerBase
{
    private readonly NoticeService _service;
    public NoticeController(NoticeService service) => _service = service;

    private long GetCurrentUserId() =>
        long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>公告列表（带已读状态）</summary>
    [HttpGet("list")]
    public IActionResult GetList([FromQuery] string? type, [FromQuery] int? status)
    {
        var userId = GetCurrentUserId();
        var list = _service.GetList(userId, type, status);
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>详情（自动标记已读）</summary>
    [HttpGet("{id:long}")]
    public IActionResult GetById(long id)
    {
        var item = _service.GetById(id, GetCurrentUserId());
        if (item == null) return Ok(new { code = 404, msg = "公告不存在" });
        return Ok(new { code = 200, msg = "ok", data = item });
    }

    /// <summary>未读数量</summary>
    [HttpGet("unread/count")]
    public IActionResult GetUnreadCount()
    {
        return Ok(new { code = 200, msg = "ok", data = _service.GetUnreadCount(GetCurrentUserId()) });
    }

    /// <summary>手动标记已读</summary>
    [HttpPut("{id:long}/read")]
    public IActionResult MarkRead(long id)
    {
        _service.MarkRead(id, GetCurrentUserId());
        return Ok(new { code = 200, msg = "已标记为已读" });
    }

    /// <summary>首页最新公告</summary>
    [HttpGet("latest")]
    public IActionResult GetLatest([FromQuery] int limit = 5)
    {
        return Ok(new { code = 200, msg = "ok", data = _service.GetLatest(GetCurrentUserId(), limit) });
    }
}
