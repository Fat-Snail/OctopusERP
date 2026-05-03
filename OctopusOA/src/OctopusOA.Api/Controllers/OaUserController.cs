using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Controllers;

/// <summary>OA 用户管理接口</summary>
[ApiController]
[Route("api/oa")]
[Authorize]
public class OaUserController : ControllerBase
{
    private readonly OaDbContext _db;
    public OaUserController(OaDbContext db) => _db = db;

    /// <summary>OA 角色定义</summary>
    public static readonly Dictionary<string, string> RoleDefinitions = new()
    {
        ["oa_admin"] = "OA 管理员",
        ["oa_user"] = "普通员工",
        ["oa_manager"] = "部门主管",
    };

    /// <summary>已同步用户列表</summary>
    [HttpGet("user/list")]
    public IActionResult GetUserList()
    {
        var list = _db.SyncUsers.ToList().Select(u => new
        {
            u.Id, u.UmcUserId, u.UserName, u.NickName, u.Email,
            u.PhoneNumber, u.Avatar, u.Status, u.OaRoles, u.LastSyncAt
        }).ToList();
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>修改用户 OA 角色</summary>
    [HttpPut("user/{id:long}/roles")]
    public IActionResult UpdateRoles(long id, [FromBody] UpdateOaRolesRequest req)
    {
        var user = _db.SyncUsers.FirstOrDefault(u => u.Id == id);
        if (user == null) return Ok(new { code = 404, msg = "用户不存在" });

        user.OaRoles = req.OaRoles.Where(r => RoleDefinitions.ContainsKey(r)).ToList();
        _db.SaveChanges();
        return Ok(new { code = 200, msg = "角色更新成功" });
    }

    /// <summary>OA 角色列表</summary>
    [HttpGet("role/list")]
    public IActionResult GetRoleList()
    {
        var list = RoleDefinitions.Select(kv => new { roleKey = kv.Key, roleName = kv.Value }).ToList();
        return Ok(new { code = 200, msg = "ok", data = list });
    }
}

public class UpdateOaRolesRequest
{
    public List<string> OaRoles { get; set; } = new();
}
