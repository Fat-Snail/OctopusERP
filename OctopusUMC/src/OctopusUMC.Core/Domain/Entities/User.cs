namespace OctopusUMC.Core.Domain.Entities;

public class User
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Sex { get; set; } = "0";
    public string? Avatar { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }

    // 导航属性
    public List<UserDept> UserDepts { get; set; } = new();
    public List<UserRole> UserRoles { get; set; } = new();
}
