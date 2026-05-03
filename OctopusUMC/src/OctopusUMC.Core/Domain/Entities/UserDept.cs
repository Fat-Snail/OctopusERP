namespace OctopusUMC.Core.Domain.Entities;

/// <summary>用户-部门-职位 关联（支持一个用户属于多个部门，在不同部门担任不同职位）</summary>
public class UserDept
{
    public long UserId { get; set; }
    public long DeptId { get; set; }
    public long? PostId { get; set; }   // 在该部门担任的职位（可为空）
    public bool IsPrimary { get; set; } // 是否为主部门（显示用）
}
