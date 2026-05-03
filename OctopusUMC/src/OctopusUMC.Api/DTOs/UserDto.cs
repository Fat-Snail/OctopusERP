namespace OctopusUMC.Api.DTOs;

/// <summary>用户信息响应</summary>
public class UserResponse
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Sex { get; set; } = "0";
    public string? Avatar { get; set; }
    public int Status { get; set; }
    public long DeptId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public long? PostId { get; set; }
    public string? PostName { get; set; }
    public List<long> DeptIds { get; set; } = new();
    public List<long> PostIds { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public DateTime CreateTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>创建用户请求</summary>
public class CreateUserRequest
{
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Sex { get; set; } = "0";
    public string Password { get; set; } = string.Empty;
    public List<long> DeptIds { get; set; } = new();
    public List<long> PostIds { get; set; } = new();
    public List<long> RoleIds { get; set; } = new();
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>修改用户请求</summary>
public class UpdateUserRequest
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Sex { get; set; } = "0";
    public List<long> DeptIds { get; set; } = new();
    public List<long> PostIds { get; set; } = new();
    public List<long> RoleIds { get; set; } = new();
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>重置密码请求</summary>
public class ResetPasswordRequest
{
    public long UserId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>更新用户状态请求</summary>
public class UpdateStatusRequest
{
    public long UserId { get; set; }
    public int Status { get; set; }
}
