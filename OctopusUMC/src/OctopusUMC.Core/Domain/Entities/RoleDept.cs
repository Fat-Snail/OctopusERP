namespace OctopusUMC.Core.Domain.Entities;

/// <summary>角色-部门 关联（用于自定义数据权限范围）</summary>
public class RoleDept
{
    public long RoleId { get; set; }
    public long DeptId { get; set; }
}
