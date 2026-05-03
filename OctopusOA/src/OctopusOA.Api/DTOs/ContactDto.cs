namespace OctopusOA.Api.DTOs;

public class ContactDeptNode
{
    public long DeptId { get; set; }
    public long ParentId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public int UserCount { get; set; }
    public List<ContactDeptNode> Children { get; set; } = new();
}

public class ContactUser
{
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
    public int Status { get; set; }
    public List<ContactDeptInfo> Depts { get; set; } = new();
}

public class ContactDeptInfo
{
    public long DeptId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public string? PostName { get; set; }
    public bool IsPrimary { get; set; }
}

// ── UMC → OA 部门同步 payload ──

public class DeptSyncPayload
{
    public string Action { get; set; } = string.Empty; // upsert / delete
    public long DeptId { get; set; }
    public long ParentId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public int Status { get; set; }
}
