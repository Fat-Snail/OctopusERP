namespace OctopusUMC.Api.DTOs;

/// <summary>角色信息响应</summary>
public class RoleResponse
{
    public long RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public int RoleSort { get; set; }
    public string DataScope { get; set; } = "1";
    public int Status { get; set; }
    public List<long> MenuIds { get; set; } = new();
    public List<long> DeptIds { get; set; } = new();
    public DateTime CreateTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>创建角色请求</summary>
public class CreateRoleRequest
{
    public string RoleName { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public int RoleSort { get; set; }
    public string DataScope { get; set; } = "1";
    public List<long> MenuIds { get; set; } = new();
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>修改角色请求</summary>
public class UpdateRoleRequest
{
    public long RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public int RoleSort { get; set; }
    public string DataScope { get; set; } = "1";
    public List<long> MenuIds { get; set; } = new();
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>角色绑定菜单请求</summary>
public class RoleMenuRequest
{
    public long RoleId { get; set; }
    public List<long> MenuIds { get; set; } = new();
}

/// <summary>角色绑定数据权限请求</summary>
public class RoleDeptRequest
{
    public long RoleId { get; set; }
    public string DataScope { get; set; } = "1";
    public List<long> DeptIds { get; set; } = new();
}
