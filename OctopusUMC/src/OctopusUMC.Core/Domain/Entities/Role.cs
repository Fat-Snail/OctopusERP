namespace OctopusUMC.Core.Domain.Entities;

public class Role
{
    public long RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public int RoleSort { get; set; }
    public string DataScope { get; set; } = "1"; // 1=全部 2=本部门及子部门 3=本部门 4=仅本人 5=自定义
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }

    // 导航属性
    public List<RoleMenu> RoleMenus { get; set; } = new();
    public List<RoleDept> RoleDepts { get; set; } = new();
}
