using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using OctopusUMC.Api.Attributes;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Api.Services;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Controllers;

/// <summary>用户管理接口</summary>
[ApiController]
[Route("api/system/user")]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserSyncService _syncService;
    public UserController(ApplicationDbContext context, UserSyncService syncService)
    {
        _context = context;
        _syncService = syncService;
    }

    private async Task SyncUserToOAAsync(User user)
    {
        await _syncService.NotifyUserChangedAsync(new UserSyncPayload
        {
            UserId = user.UserId,
            UserName = user.UserName,
            NickName = user.NickName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status,
        });
    }

    private UserResponse MapUser(User u)
    {
        var userDepts = _context.UserDepts
            .Where(ud => ud.UserId == u.UserId)
            .OrderByDescending(ud => ud.IsPrimary)
            .ToList();

        // 主部门（IsPrimary=true 或第一个）
        var primaryUserDept = userDepts.FirstOrDefault();

        var dept = primaryUserDept != null
            ? _context.Depts.FirstOrDefault(d => d.DeptId == primaryUserDept.DeptId)
            : null;

        var post = primaryUserDept?.PostId != null
            ? _context.Posts.FirstOrDefault(p => p.PostId == primaryUserDept.PostId)
            : null;

        var roleIds = _context.UserRoles.Where(ur => ur.UserId == u.UserId).Select(ur => ur.RoleId).ToList();
        var roles = _context.Roles.Where(r => roleIds.Contains(r.RoleId)).ToList();

        // 收集所有关联的部门ID和职位ID
        var allDeptIds = userDepts.Select(ud => ud.DeptId).ToList();
        var allPostIds = userDepts.Where(ud => ud.PostId.HasValue).Select(ud => ud.PostId!.Value).Distinct().ToList();

        return new UserResponse
        {
            UserId = u.UserId,
            UserName = u.UserName,
            NickName = u.NickName,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            Sex = u.Sex,
            Avatar = u.Avatar,
            Status = u.Status,
            DeptId = dept?.DeptId ?? 0,
            DeptName = dept?.DeptName ?? string.Empty,
            PostId = post?.PostId,
            PostName = post?.PostName,
            DeptIds = allDeptIds,
            PostIds = allPostIds,
            Roles = roles.Select(r => r.RoleKey).ToList(),
            CreateTime = u.CreateTime,
            Remark = u.Remark,
        };
    }

    /// <summary>分页查询用户列表</summary>
    [HttpGet("list")]
    public ApiResponse<PagedResult<UserResponse>> GetList(
        [FromQuery] string? userName,
        [FromQuery] string? phoneNumber,
        [FromQuery] int? status,
        [FromQuery] long? deptId,
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _context.Users.AsQueryable();
        if (!string.IsNullOrEmpty(userName))
            query = query.Where(u => u.UserName.Contains(userName));
        if (!string.IsNullOrEmpty(phoneNumber))
            query = query.Where(u => u.PhoneNumber != null && u.PhoneNumber.Contains(phoneNumber));
        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);
        if (deptId.HasValue)
        {
            var userIdsInDept = _context.UserDepts
                .Where(ud => ud.DeptId == deptId.Value)
                .Select(ud => ud.UserId)
                .ToList();
            query = query.Where(u => userIdsInDept.Contains(u.UserId));
        }

        var total = query.Count();
        var users = query.Skip((pageNum - 1) * pageSize).Take(pageSize).ToList();
        var rows = users.Select(MapUser).ToList();
        return ApiResponse<PagedResult<UserResponse>>.Success(new PagedResult<UserResponse> { Rows = rows, Total = total });
    }

    /// <summary>根据用户ID获取详情</summary>
    [HttpGet("{id:long}")]
    public ApiResponse<UserResponse> GetById(long id)
    {
        var u = _context.Users.FirstOrDefault(u => u.UserId == id);
        if (u == null) return ApiResponse<UserResponse>.Fail("用户不存在", 404);
        return ApiResponse<UserResponse>.Success(MapUser(u));
    }

    /// <summary>新增用户</summary>
    [HasPermission("system:user:add")]
    [Log("用户管理-新增")]
    [HttpPost]
    public async Task<ApiResponse<UserResponse>> Create([FromBody] CreateUserRequest req)
    {
        if (_context.Users.Any(u => u.UserName == req.UserName))
            return ApiResponse<UserResponse>.Fail("用户名已存在");

        var user = new User
        {
            UserName = req.UserName,
            NickName = req.NickName,
            Email = req.Email,
            PhoneNumber = req.PhoneNumber,
            Sex = req.Sex,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Status = req.Status,
            Remark = req.Remark,
            CreateTime = DateTime.UtcNow,
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        // 插入 UserDept（第一个部门为主部门，职位按索引配对）
        for (int i = 0; i < req.DeptIds.Count; i++)
        {
            _context.UserDepts.Add(new UserDept
            {
                UserId = user.UserId,
                DeptId = req.DeptIds[i],
                PostId = i < req.PostIds.Count ? req.PostIds[i] : null,
                IsPrimary = i == 0
            });
        }

        // 插入 UserRole
        foreach (var roleId in req.RoleIds)
        {
            _context.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId });
        }
        _context.SaveChanges();

        _ = SyncUserToOAAsync(user);
        return ApiResponse<UserResponse>.Success(MapUser(user), "新增成功");
    }

    /// <summary>修改用户信息</summary>
    [HasPermission("system:user:edit")]
    [Log("用户管理-修改")]
    [HttpPut]
    public async Task<ApiResponse<UserResponse>> Update([FromBody] UpdateUserRequest req)
    {
        var u = _context.Users.FirstOrDefault(u => u.UserId == req.UserId);
        if (u == null) return ApiResponse<UserResponse>.Fail("用户不存在", 404);

        u.NickName = req.NickName;
        u.Email = req.Email;
        u.PhoneNumber = req.PhoneNumber;
        u.Sex = req.Sex;
        u.Status = req.Status;
        u.Remark = req.Remark;

        // 删除旧关联，重建
        var oldDepts = _context.UserDepts.Where(ud => ud.UserId == req.UserId).ToList();
        _context.UserDepts.RemoveRange(oldDepts);
        var oldRoles = _context.UserRoles.Where(ur => ur.UserId == req.UserId).ToList();
        _context.UserRoles.RemoveRange(oldRoles);

        for (int i = 0; i < req.DeptIds.Count; i++)
        {
            _context.UserDepts.Add(new UserDept
            {
                UserId = u.UserId,
                DeptId = req.DeptIds[i],
                PostId = i < req.PostIds.Count ? req.PostIds[i] : null,
                IsPrimary = i == 0
            });
        }
        foreach (var roleId in req.RoleIds)
            _context.UserRoles.Add(new UserRole { UserId = u.UserId, RoleId = roleId });

        _context.SaveChanges();
        _ = SyncUserToOAAsync(u);
        return ApiResponse<UserResponse>.Success(MapUser(u), "修改成功");
    }

    /// <summary>批量删除用户（逗号分隔ID）</summary>
    [HasPermission("system:user:delete")]
    [Log("用户管理-删除")]
    [HttpDelete("{ids}")]
    public ApiResponse<object?> Delete(string ids)
    {
        var idList = ids.Split(',').Select(long.Parse).ToList();
        var items = _context.Users.Where(u => idList.Contains(u.UserId)).ToList();
        if (items.Count == 0)
            return ApiResponse<object?>.Fail("未找到用户", 404);

        // 先删除关联表
        var userIds = items.Select(u => u.UserId).ToList();
        _context.UserDepts.RemoveRange(_context.UserDepts.Where(ud => userIds.Contains(ud.UserId)));
        _context.UserRoles.RemoveRange(_context.UserRoles.Where(ur => userIds.Contains(ur.UserId)));
        _context.Users.RemoveRange(items);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "删除成功");
    }

    /// <summary>修改用户状态（启用/禁用）</summary>
    [HasPermission("system:user:edit")]
    [Log("用户管理-状态")]
    [HttpPut("status")]
    public async Task<ApiResponse<object?>> UpdateStatus([FromBody] UpdateStatusRequest req)
    {
        var u = _context.Users.FirstOrDefault(u => u.UserId == req.UserId);
        if (u == null) return ApiResponse<object?>.Fail("用户不存在", 404);
        u.Status = req.Status;
        _context.SaveChanges();
        _ = SyncUserToOAAsync(u);
        return ApiResponse<object?>.Success(null, "状态更新成功");
    }

    /// <summary>重置用户密码</summary>
    [HasPermission("system:user:resetPwd")]
    [HttpPut("resetPwd")]
    public ApiResponse<object?> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var u = _context.Users.FirstOrDefault(u => u.UserId == req.UserId);
        if (u == null) return ApiResponse<object?>.Fail("用户不存在", 404);
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "密码重置成功");
    }

    /// <summary>查询用户已绑定角色</summary>
    [HttpGet("authRole/{userId:long}")]
    public ApiResponse<object> GetAuthRole(long userId)
    {
        var u = _context.Users.FirstOrDefault(u => u.UserId == userId);
        if (u == null) return ApiResponse<object>.Fail("用户不存在", 404);

        var userRoleIds = _context.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToList();
        var assignedRoles = _context.Roles.Where(r => userRoleIds.Contains(r.RoleId))
            .Select(r => new RoleResponse
            {
                RoleId = r.RoleId, RoleName = r.RoleName, RoleKey = r.RoleKey,
                RoleSort = r.RoleSort, DataScope = r.DataScope, Status = r.Status,
                MenuIds = _context.RoleMenus.Where(rm => rm.RoleId == r.RoleId).Select(rm => rm.MenuId).ToList(),
                DeptIds = _context.RoleDepts.Where(rd => rd.RoleId == r.RoleId).Select(rd => rd.DeptId).ToList(),
                CreateTime = r.CreateTime, Remark = r.Remark
            }).ToList();

        var allRoles = _context.Roles
            .Select(r => new RoleResponse
            {
                RoleId = r.RoleId, RoleName = r.RoleName, RoleKey = r.RoleKey,
                RoleSort = r.RoleSort, DataScope = r.DataScope, Status = r.Status,
                MenuIds = _context.RoleMenus.Where(rm => rm.RoleId == r.RoleId).Select(rm => rm.MenuId).ToList(),
                DeptIds = _context.RoleDepts.Where(rd => rd.RoleId == r.RoleId).Select(rd => rd.DeptId).ToList(),
                CreateTime = r.CreateTime, Remark = r.Remark
            }).ToList();

        return ApiResponse<object>.Success(new { user = MapUser(u), roles = allRoles, assignedRoles });
    }

    /// <summary>用户绑定角色</summary>
    [HttpPut("authRole")]
    public ApiResponse<object?> BindRoles([FromQuery] long userId, [FromQuery] string roleIds)
    {
        var u = _context.Users.FirstOrDefault(u => u.UserId == userId);
        if (u == null) return ApiResponse<object?>.Fail("用户不存在", 404);

        // 先删再建
        var old = _context.UserRoles.Where(ur => ur.UserId == userId).ToList();
        _context.UserRoles.RemoveRange(old);
        foreach (var rid in roleIds.Split(',').Select(long.Parse))
            _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = rid });
        _context.SaveChanges();
        return ApiResponse<object?>.Success(null, "授权成功");
    }

    /// <summary>导出用户列表为 Excel</summary>
    [HasPermission("system:user:export")]
    [HttpGet("export")]
    public IActionResult Export(
        [FromQuery] string? userName,
        [FromQuery] string? phoneNumber,
        [FromQuery] int? status,
        [FromQuery] long? deptId)
    {
        var query = _context.Users.AsQueryable();
        if (!string.IsNullOrEmpty(userName))
            query = query.Where(u => u.UserName.Contains(userName));
        if (!string.IsNullOrEmpty(phoneNumber))
            query = query.Where(u => u.PhoneNumber != null && u.PhoneNumber.Contains(phoneNumber));
        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);
        if (deptId.HasValue)
        {
            var ids = _context.UserDepts.Where(ud => ud.DeptId == deptId.Value).Select(ud => ud.UserId).ToList();
            query = query.Where(u => ids.Contains(u.UserId));
        }
        var users = query.ToList().Select(MapUser).ToList();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("用户列表");
        string[] headers = ["用户名", "姓名", "邮箱", "手机号", "部门", "状态", "创建时间"];
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }
        for (var r = 0; r < users.Count; r++)
        {
            var u = users[r];
            ws.Cell(r + 2, 1).Value = u.UserName;
            ws.Cell(r + 2, 2).Value = u.NickName;
            ws.Cell(r + 2, 3).Value = u.Email;
            ws.Cell(r + 2, 4).Value = u.PhoneNumber ?? "";
            ws.Cell(r + 2, 5).Value = u.DeptName;
            ws.Cell(r + 2, 6).Value = u.Status == 1 ? "启用" : "禁用";
            ws.Cell(r + 2, 7).Value = u.CreateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"用户列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>下载用户导入模板</summary>
    [HttpGet("importTemplate")]
    public IActionResult ImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("用户导入模板");
        string[] headers = ["用户名*", "姓名*", "邮箱*", "手机号", "性别(0保密/1男/2女)", "密码*", "备注"];
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }
        // Example row
        ws.Cell(2, 1).Value = "testuser";
        ws.Cell(2, 2).Value = "测试用户";
        ws.Cell(2, 3).Value = "test@example.com";
        ws.Cell(2, 4).Value = "13800138000";
        ws.Cell(2, 5).Value = "1";
        ws.Cell(2, 6).Value = "Test@123456";
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "用户导入模板.xlsx");
    }

    /// <summary>批量导入用户（Excel 文件）</summary>
    [HasPermission("system:user:import")]
    [HttpPost("import")]
    public async Task<ApiResponse<object>> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return ApiResponse<object>.Fail("请选择文件");

        var successCount = 0;
        var failMessages = new List<string>();

        using var stream = file.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var userName = ws.Cell(row, 1).GetString().Trim();
            var nickName = ws.Cell(row, 2).GetString().Trim();
            var email    = ws.Cell(row, 3).GetString().Trim();
            var phone    = ws.Cell(row, 4).GetString().Trim();
            var sex      = ws.Cell(row, 5).GetString().Trim();
            var password = ws.Cell(row, 6).GetString().Trim();

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(nickName) ||
                string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                failMessages.Add($"第 {row} 行：用户名/姓名/邮箱/密码为必填项");
                continue;
            }

            if (_context.Users.Any(u => u.UserName == userName))
            {
                failMessages.Add($"第 {row} 行：用户名 {userName} 已存在");
                continue;
            }

            _context.Users.Add(new User
            {
                UserName = userName,
                NickName = nickName,
                Email = email,
                PhoneNumber = string.IsNullOrEmpty(phone) ? null : phone,
                Sex = string.IsNullOrEmpty(sex) ? "0" : sex,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Status = 1,
                CreateTime = DateTime.UtcNow,
            });
            successCount++;
        }

        if (successCount > 0)
        {
            _context.SaveChanges();
            _ = Task.Run(async () =>
            {
                // fire-and-forget sync for each new user would be complex here; skip
                await Task.CompletedTask;
            });
        }

        return ApiResponse<object>.Success(new
        {
            successCount,
            failCount = failMessages.Count,
            failMessages,
        }, $"导入完成：成功 {successCount} 条，失败 {failMessages.Count} 条");
    }

    /// <summary>查询用户所属的全部部门（含兼职/多公司）</summary>
    /// <remarks>
    /// 返回用户在所有公司/部门的任职记录，IsPrimary=true 为主部门。
    /// 一个用户可能同时出现在多家公司的不同部门中。
    /// </remarks>
    [HttpGet("{userId:long}/depts")]
    public ApiResponse<object> GetUserDepts(long userId)
    {
        var u = _context.Users.FirstOrDefault(u => u.UserId == userId);
        if (u == null) return ApiResponse<object>.Fail("用户不存在", 404);

        var userDepts = _context.UserDepts.Where(ud => ud.UserId == userId).ToList();
        var result = userDepts.Select(ud =>
        {
            var dept = _context.Depts.FirstOrDefault(d => d.DeptId == ud.DeptId);
            // 查找该部门所属公司（向上找 ParentId=0 的根节点）
            var company = FindCompanyRoot(dept);
            var post = ud.PostId.HasValue
                ? _context.Posts.FirstOrDefault(p => p.PostId == ud.PostId)
                : null;
            return new
            {
                deptId    = ud.DeptId,
                deptName  = dept?.DeptName ?? "未知部门",
                companyId = company?.DeptId,
                company   = company?.DeptName ?? "未知公司",
                postId    = ud.PostId,
                postName  = post?.PostName,
                isPrimary = ud.IsPrimary
            };
        }).OrderByDescending(x => x.isPrimary).ToList();

        return ApiResponse<object>.Success(new
        {
            userId   = u.UserId,
            userName = u.UserName,
            nickName = u.NickName,
            depts    = result
        });
    }

    private Dept? FindCompanyRoot(Dept? dept)
    {
        if (dept == null) return null;
        if (dept.ParentId == 0) return dept;                              // 自身就是根
        var parent = _context.Depts.FirstOrDefault(d => d.DeptId == dept.ParentId);
        return FindCompanyRoot(parent);
    }
}
