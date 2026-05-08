using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.Attributes;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Api.Services;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>部门管理接口</summary>
[ApiController]
[Route("api/system/dept")]
public class DeptController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly DeptSyncService _syncService;
    public DeptController(ApplicationDbContext context, DeptSyncService syncService)
    {
        _context = context;
        _syncService = syncService;
    }

    private void PushSync(Dept d, string action = "upsert")
    {
        _ = _syncService.NotifyDeptChangedAsync(new DeptSyncPayload
        {
            Action = action,
            DeptId = d.DeptId,
            ParentId = d.ParentId,
            DeptName = d.DeptName,
            OrderNum = d.OrderNum,
            Status = d.Status,
        });
    }

    private DeptResponse MapDept(Dept d) => new()
    {
        DeptId = d.DeptId,
        ParentId = d.ParentId,
        DeptName = d.DeptName,
        OrderNum = d.OrderNum,
        Status = d.Status,
        CreateTime = d.CreateTime,
    };

    private List<DeptResponse> BuildTree(List<DeptResponse> all, long parentId) =>
        all.Where(d => d.ParentId == parentId)
           .Select(d => { d.Children = BuildTree(all, d.DeptId); return d; })
           .OrderBy(d => d.OrderNum)
           .ToList();

    /// <summary>获取部门树形结构</summary>
    [HttpGet("tree")]
    public ApiResponse<List<DeptResponse>> GetTree()
    {
        var all = _context.Depts.ToList().Select(MapDept).ToList();
        return ApiResponse<List<DeptResponse>>.Success(BuildTree(all, 0));
    }

    /// <summary>获取部门平铺列表（支持条件筛选）</summary>
    [HttpGet("list")]
    public ApiResponse<List<DeptResponse>> GetList(
        [FromQuery] string? deptName,
        [FromQuery] int? status)
    {
        var query = _context.Depts.AsQueryable();
        if (!string.IsNullOrEmpty(deptName))
            query = query.Where(d => d.DeptName.Contains(deptName));
        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);
        return ApiResponse<List<DeptResponse>>.Success(query.OrderBy(d => d.OrderNum).ToList().Select(MapDept).ToList());
    }

    /// <summary>根据ID获取部门详情</summary>
    [HttpGet("{id:long}")]
    public ApiResponse<DeptResponse> GetById(long id)
    {
        var d = _context.Depts.FirstOrDefault(d => d.DeptId == id);
        if (d == null) return ApiResponse<DeptResponse>.Fail("部门不存在", 404);
        return ApiResponse<DeptResponse>.Success(MapDept(d));
    }

    /// <summary>获取排除指定节点的部门树（编辑时防止自循环）</summary>
    [HttpGet("exclude/{id:long}")]
    public ApiResponse<List<DeptResponse>> GetExclude(long id)
    {
        var all = _context.Depts
            .Where(d => d.DeptId != id)
            .ToList()
            .Select(MapDept).ToList();
        return ApiResponse<List<DeptResponse>>.Success(BuildTree(all, 0));
    }

    /// <summary>新增部门</summary>
    [HasPermission("system:dept:add")]
    [Log("部门管理-新增")]
    [HttpPost]
    public ApiResponse<DeptResponse> Create([FromBody] CreateDeptRequest req)
    {
        var dept = new Dept
        {
            ParentId = req.ParentId,
            DeptName = req.DeptName,
            OrderNum = req.OrderNum,
            Status = req.Status,
            CreateTime = DateTime.UtcNow,
        };
        _context.Depts.Add(dept);
        _context.SaveChanges();
        PushSync(dept);
        return ApiResponse<DeptResponse>.Success(MapDept(dept), "新增成功");
    }

    /// <summary>修改部门</summary>
    [HasPermission("system:dept:edit")]
    [Log("部门管理-修改")]
    [HttpPut]
    public ApiResponse<DeptResponse> Update([FromBody] UpdateDeptRequest req)
    {
        var d = _context.Depts.FirstOrDefault(d => d.DeptId == req.DeptId);
        if (d == null) return ApiResponse<DeptResponse>.Fail("部门不存在", 404);
        d.ParentId = req.ParentId;
        d.DeptName = req.DeptName;
        d.OrderNum = req.OrderNum;
        d.Status = req.Status;
        _context.SaveChanges();
        PushSync(d);
        return ApiResponse<DeptResponse>.Success(MapDept(d), "修改成功");
    }

    /// <summary>删除部门（有子部门或用户时不允许删除）</summary>
    [HasPermission("system:dept:delete")]
    [Log("部门管理-删除")]
    [HttpDelete("{id:long}")]
    public ApiResponse<object?> Delete(long id)
    {
        if (_context.Depts.Any(d => d.ParentId == id))
            return ApiResponse<object?>.Fail("存在子部门，不允许删除");
        if (_context.UserDepts.Any(ud => ud.DeptId == id))
            return ApiResponse<object?>.Fail("部门下存在用户，不允许删除");
        var dept = _context.Depts.FirstOrDefault(d => d.DeptId == id);
        if (dept == null)
            return ApiResponse<object?>.Fail("部门不存在", 404);
        _context.Depts.Remove(dept);
        _context.SaveChanges();
        PushSync(dept, "delete");
        return ApiResponse<object?>.Success(null, "删除成功");
    }
}
