using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>菜单管理接口</summary>
[ApiController]
[Route("api/system/menu")]
public class MenuController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public MenuController(ApplicationDbContext context) => _context = context;

    private MenuResponse MapMenu(Menu m) => new()
    {
        MenuId = m.MenuId,
        ParentId = m.ParentId,
        MenuName = m.MenuName,
        MenuType = m.MenuType,
        Path = m.Path,
        Component = m.Component,
        Permission = m.Permission,
        Icon = m.Icon,
        OrderNum = m.OrderNum,
        Status = m.Status,
        IsCache = m.IsCache,
        IsFrame = m.IsFrame,
        Visible = m.Visible,
    };

    private List<MenuResponse> BuildTree(List<MenuResponse> all, long parentId) =>
        all.Where(m => m.ParentId == parentId)
           .Select(m => { m.Children = BuildTree(all, m.MenuId); return m; })
           .OrderBy(m => m.OrderNum)
           .ToList();

    /// <summary>获取菜单树形结构</summary>
    [HttpGet("tree")]
    public ApiResponse<List<MenuResponse>> GetTree()
    {
        var all = _context.Menus.ToList().Select(MapMenu).ToList();
        return ApiResponse<List<MenuResponse>>.Success(BuildTree(all, 0));
    }

    /// <summary>获取菜单平铺列表（支持条件筛选）</summary>
    [HttpGet("list")]
    public ApiResponse<List<MenuResponse>> GetList(
        [FromQuery] string? menuName,
        [FromQuery] int? status,
        [FromQuery] string? menuType)
    {
        var query = _context.Menus.AsQueryable();
        if (!string.IsNullOrEmpty(menuName))
            query = query.Where(m => m.MenuName.Contains(menuName));
        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);
        if (!string.IsNullOrEmpty(menuType))
            query = query.Where(m => m.MenuType == menuType);
        return ApiResponse<List<MenuResponse>>.Success(
            query.OrderBy(m => m.OrderNum).ToList().Select(MapMenu).ToList());
    }

    /// <summary>根据菜单ID获取详情</summary>
    [HttpGet("{id:long}")]
    public ApiResponse<MenuResponse> GetById(long id)
    {
        var m = _context.Menus.FirstOrDefault(m => m.MenuId == id);
        if (m == null) return ApiResponse<MenuResponse>.Fail("菜单不存在", 404);
        return ApiResponse<MenuResponse>.Success(MapMenu(m));
    }

    /// <summary>新增菜单</summary>
    [HttpPost]
    public ApiResponse<MenuResponse> Create([FromBody] CreateMenuRequest req)
    {
        var menu = new Menu
        {
            ParentId = req.ParentId,
            MenuName = req.MenuName,
            MenuType = req.MenuType,
            Path = req.Path,
            Component = req.Component,
            Permission = req.Permission,
            Icon = req.Icon,
            OrderNum = req.OrderNum,
            Status = req.Status,
            IsCache = req.IsCache,
            IsFrame = req.IsFrame,
            Visible = req.Visible,
        };
        _context.Menus.Add(menu);
        _context.SaveChanges();
        return ApiResponse<MenuResponse>.Success(MapMenu(menu), "新增成功");
    }

    /// <summary>修改菜单</summary>
    [HttpPut]
    public ApiResponse<MenuResponse> Update([FromBody] UpdateMenuRequest req)
    {
        var m = _context.Menus.FirstOrDefault(m => m.MenuId == req.MenuId);
        if (m == null) return ApiResponse<MenuResponse>.Fail("菜单不存在", 404);
        m.ParentId = req.ParentId;
        m.MenuName = req.MenuName;
        m.MenuType = req.MenuType;
        m.Path = req.Path;
        m.Component = req.Component;
        m.Permission = req.Permission;
        m.Icon = req.Icon;
        m.OrderNum = req.OrderNum;
        m.Status = req.Status;
        m.IsCache = req.IsCache;
        m.IsFrame = req.IsFrame;
        m.Visible = req.Visible;
        _context.SaveChanges();
        return ApiResponse<MenuResponse>.Success(MapMenu(m), "修改成功");
    }

    /// <summary>删除菜单（有子菜单时不允许删除）</summary>
    [HttpDelete("{id:long}")]
    public ApiResponse<object?> Delete(long id)
    {
        if (_context.Menus.Any(m => m.ParentId == id))
            return ApiResponse<object?>.Fail("存在子菜单，不允许删除");
        var menu = _context.Menus.FirstOrDefault(m => m.MenuId == id);
        if (menu == null)
            return ApiResponse<object?>.Fail("菜单不存在", 404);
        _context.Menus.Remove(menu);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }

    /// <summary>获取角色已绑定的菜单ID列表</summary>
    [HttpGet("role/{roleId:long}")]
    public ApiResponse<List<long>> GetRoleMenuIds(long roleId)
    {
        var role = _context.Roles.FirstOrDefault(r => r.RoleId == roleId);
        if (role == null) return ApiResponse<List<long>>.Fail("角色不存在", 404);
        var menuIds = _context.RoleMenus.Where(rm => rm.RoleId == roleId).Select(rm => rm.MenuId).ToList();
        return ApiResponse<List<long>>.Success(menuIds);
    }
}
