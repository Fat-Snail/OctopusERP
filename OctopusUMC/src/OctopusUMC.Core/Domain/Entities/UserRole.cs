namespace OctopusUMC.Core.Domain.Entities;

/// <summary>用户-角色 关联</summary>
public class UserRole
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
}
