using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.Services;
using System.Security.Claims;

namespace OctopusOA.Api.Controllers;

/// <summary>工作台聚合接口</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _service;
    public DashboardController(DashboardService service) => _service = service;

    private long GetCurrentUserId() =>
        long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>首页卡片汇总</summary>
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        return Ok(new { code = 200, msg = "ok", data = _service.GetSummary(GetCurrentUserId()) });
    }

    /// <summary>待办列表（可按 type 过滤：approval/notice/attendance）</summary>
    [HttpGet("todos")]
    public IActionResult GetTodos([FromQuery] string? type)
    {
        var list = _service.GetTodos(GetCurrentUserId(), type);
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }
}
