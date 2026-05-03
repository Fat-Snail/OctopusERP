using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.Services;

namespace OctopusOA.Api.Controllers;

/// <summary>通讯录（所有登录用户可见）</summary>
[ApiController]
[Route("api/contact")]
[Authorize]
public class ContactController : ControllerBase
{
    private readonly ContactService _service;
    public ContactController(ContactService service) => _service = service;

    /// <summary>部门树（含每个节点的用户数）</summary>
    [HttpGet("dept/tree")]
    public IActionResult GetDeptTree()
    {
        return Ok(new { code = 200, msg = "ok", data = _service.GetDeptTree() });
    }

    /// <summary>员工列表（可按部门 ID 筛选 + 关键字搜索）</summary>
    [HttpGet("users")]
    public IActionResult GetUsers([FromQuery] long? deptId, [FromQuery] string? keyword)
    {
        var list = _service.SearchUsers(deptId, keyword);
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>员工详情</summary>
    [HttpGet("user/{umcUserId:long}")]
    public IActionResult GetUser(long umcUserId)
    {
        var user = _service.GetUser(umcUserId);
        if (user == null) return Ok(new { code = 404, msg = "用户不存在" });
        return Ok(new { code = 200, msg = "ok", data = user });
    }
}
