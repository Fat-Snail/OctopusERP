using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.Attributes;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>角色管理接口</summary>
[ApiController]
[Route("api/system/role")]
public class RoleController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public RoleController(ApplicationDbContext context) => _context = context;

    private RoleResponse MapRole(Role r) => new()
    {
        RoleId = r.RoleId,
        RoleName = r.RoleName,
        RoleKey = r.RoleKey,
        RoleSort = r.RoleSort,
        DataScope = r.DataScope,
        Status = r.Status,
        MenuIds = _context.RoleMenus.Where(rm => rm.RoleId == r.RoleId).Select(rm => rm.MenuId).ToList(),
        DeptIds = _context.RoleDepts.Where(rd => rd.RoleId == r.RoleId).Select(rd => rd.DeptId).ToList(),
        CreateTime = r.CreateTime,
        Remark = r.Remark,
    };

    /// <summary>分页查询角色列表</summary>
    [HttpGet("list")]
    public ApiResponse<PagedResult<RoleResponse>> GetList(
        [FromQuery] string? roleName,
        [FromQuery] string? roleKey,
        [FromQuery] int? status,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.Roles.AsQueryable();
        if (!string.IsNullOrEmpty(roleName))
            query = query.Where(r => r.RoleName.Contains(roleName));
        if (!string.IsNullOrEmpty(roleKey))
            query = query.Where(r => r.RoleKey.Contains(roleKey));
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var total = query.Count();
        var rows = query.OrderBy(r => r.RoleSort)
            .Skip((pageNum - 1) * pageSize).Take(pageSize)
            .ToList()
            .Select(MapRole).ToList();
        return ApiResponse<PagedResult<RoleResponse>>.Success(new() { Rows = rows, Total = total });
    }

    /// <summary>根据角色ID获取详情</summary>
    [HttpGet("{id:long}")]
    public ApiResponse<RoleResponse> GetById(long id)
    {
        var r = _context.Roles.FirstOrDefault(r => r.RoleId == id);
        if (r == null) return ApiResponse<RoleResponse>.Fail("角色不存在", 404);
        return ApiResponse<RoleResponse>.Success(MapRole(r));
    }

    /// <summary>新增角色</summary>
    [HasPermission("system:role:add")]
    [Log("角色管理-新增")]
    [HttpPost]
    public ApiResponse<RoleResponse> Create([FromBody] CreateRoleRequest req)
    {
        if (_context.Roles.Any(r => r.RoleKey == req.RoleKey))
            return ApiResponse<RoleResponse>.Fail("角色标识已存在");

        var role = new Role
        {
            RoleName = req.RoleName,
            RoleKey = req.RoleKey,
            RoleSort = req.RoleSort,
            DataScope = req.DataScope,
            Status = req.Status,
            Remark = req.Remark,
            CreateTime = DateTime.UtcNow,
        };
        _context.Roles.Add(role);
        _context.SaveChanges();

        // 插入 RoleMenu 关联
        foreach (var menuId in req.MenuIds)
            _context.RoleMenus.Add(new RoleMenu { RoleId = role.RoleId, MenuId = menuId });
        _context.SaveChanges();

        return ApiResponse<RoleResponse>.Success(MapRole(role), "新增成功");
    }

    /// <summary>修改角色</summary>
    [HasPermission("system:role:edit")]
    [Log("角色管理-修改")]
    [HttpPut]
    public ApiResponse<RoleResponse> Update([FromBody] UpdateRoleRequest req)
    {
        var r = _context.Roles.FirstOrDefault(r => r.RoleId == req.RoleId);
        if (r == null) return ApiResponse<RoleResponse>.Fail("角色不存在", 404);
        r.RoleName = req.RoleName;
        r.RoleKey = req.RoleKey;
        r.RoleSort = req.RoleSort;
        r.DataScope = req.DataScope;
        r.Status = req.Status;
        r.Remark = req.Remark;

        // 重建 RoleMenu 关联
        var oldMenus = _context.RoleMenus.Where(rm => rm.RoleId == req.RoleId).ToList();
        _context.RoleMenus.RemoveRange(oldMenus);
        foreach (var menuId in req.MenuIds)
            _context.RoleMenus.Add(new RoleMenu { RoleId = r.RoleId, MenuId = menuId });

        _context.SaveChanges();
        return ApiResponse<RoleResponse>.Success(MapRole(r), "修改成功");
    }

    /// <summary>批量删除角色（逗号分隔ID）</summary>
    [HasPermission("system:role:delete")]
    [Log("角色管理-删除")]
    [HttpDelete("{ids}")]
    public ApiResponse<object?> Delete(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.Roles.Where(r => idList.Contains(r.RoleId)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("角色不存在", 404);

        // 先删除关联表
        _context.RoleMenus.RemoveRange(_context.RoleMenus.Where(rm => idList.Contains(rm.RoleId)));
        _context.RoleDepts.RemoveRange(_context.RoleDepts.Where(rd => idList.Contains(rd.RoleId)));
        _context.UserRoles.RemoveRange(_context.UserRoles.Where(ur => idList.Contains(ur.RoleId)));
        _context.Roles.RemoveRange(items);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }

    /// <summary>修改角色状态</summary>
    [HttpPut("status")]
    public ApiResponse<object?> UpdateStatus([FromBody] UpdateStatusRequest req)
    {
        var r = _context.Roles.FirstOrDefault(r => r.RoleId == req.UserId);
        if (r == null) return ApiResponse<object?>.Fail("角色不存在", 404);
        r.Status = req.Status;
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "状态更新成功");
    }

    /// <summary>角色绑定菜单权限</summary>
    [HttpPost("menu")]
    public ApiResponse<object?> BindMenu([FromBody] RoleMenuRequest req)
    {
        var r = _context.Roles.FirstOrDefault(r => r.RoleId == req.RoleId);
        if (r == null) return ApiResponse<object?>.Fail("角色不存在", 404);

        var old = _context.RoleMenus.Where(rm => rm.RoleId == req.RoleId).ToList();
        _context.RoleMenus.RemoveRange(old);
        foreach (var menuId in req.MenuIds)
            _context.RoleMenus.Add(new RoleMenu { RoleId = req.RoleId, MenuId = menuId });
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "菜单权限设置成功");
    }

    /// <summary>角色绑定数据权限</summary>
    [HttpPost("dept")]
    public ApiResponse<object?> BindDept([FromBody] RoleDeptRequest req)
    {
        var r = _context.Roles.FirstOrDefault(r => r.RoleId == req.RoleId);
        if (r == null) return ApiResponse<object?>.Fail("角色不存在", 404);

        var old = _context.RoleDepts.Where(rd => rd.RoleId == req.RoleId).ToList();
        _context.RoleDepts.RemoveRange(old);
        r.DataScope = req.DataScope;
        if (req.DataScope == "5")
        {
            foreach (var deptId in req.DeptIds)
                _context.RoleDepts.Add(new RoleDept { RoleId = req.RoleId, DeptId = deptId });
        }
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "数据权限设置成功");
    }
}
