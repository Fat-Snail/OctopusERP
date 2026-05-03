namespace OctopusUMC.Api.DTOs;

/// <summary>部门信息响应</summary>
public class DeptResponse
{
    public long DeptId { get; set; }
    public long ParentId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public List<DeptResponse> Children { get; set; } = new();
}

/// <summary>创建部门请求</summary>
public class CreateDeptRequest
{
    public long ParentId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public int Status { get; set; } = 1;
}

/// <summary>修改部门请求</summary>
public class UpdateDeptRequest
{
    public long DeptId { get; set; }
    public long ParentId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public int Status { get; set; } = 1;
}
