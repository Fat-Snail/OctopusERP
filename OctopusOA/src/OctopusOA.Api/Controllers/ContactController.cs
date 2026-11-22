using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Services;
using System.Text.Json;

namespace OctopusOA.Api.Controllers;

/// <summary>通讯录（所有登录用户可见）</summary>
[ApiController]
[Route("api/contact")]
[Authorize]
public class ContactController : ControllerBase
{
    private readonly ContactService _service;
    private readonly IDistributedCache _cache;

    // P1 热点缓存：部门树（TTL 5 分钟；UMC 同步 SyncDept 时主动失效）
    private const string DeptTreeCacheKey = "oa:dept:tree";
    private static readonly DistributedCacheEntryOptions DeptTreeCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
    };

    public ContactController(ContactService service, IDistributedCache cache)
    {
        _service = service;
        _cache = cache;
    }

    /// <summary>部门树（含每个节点的用户数，缓存读路径）</summary>
    [HttpGet("dept/tree")]
    public async Task<IActionResult> GetDeptTree()
    {
        var cached = await _cache.GetStringAsync(DeptTreeCacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            var hit = JsonSerializer.Deserialize<List<ContactDeptNode>>(cached);
            if (hit != null) return Ok(new { code = 200, msg = "ok", data = hit });
        }

        var tree = _service.GetDeptTree();
        await _cache.SetStringAsync(DeptTreeCacheKey, JsonSerializer.Serialize(tree), DeptTreeCacheOptions);
        return Ok(new { code = 200, msg = "ok", data = tree });
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
