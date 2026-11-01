using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MiniappTodo.Api.Persistence;

namespace MiniappTodo.Api.Services;

/// <summary>
/// 用户上下文：从 JWT claims 提取信息 + Lazy Provisioning 本地 UserProfile。
/// 关键：本服务永远不查 UMC，只信任 Keycloak token。
/// </summary>
public class UserContextService(TodoDbContext db)
{
    /// <summary>
    /// 拿当前用户的 Keycloak sub（UUID）。无 token 时抛 UnauthorizedAccessException。
    /// </summary>
    public string RequireSub(ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(sub)
            ? throw new UnauthorizedAccessException("Missing sub claim")
            : sub;
    }

    public class UserInfo
    {
        public string Sub { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? OctopusUserId { get; set; }
        public string? PrimaryDeptId { get; set; }
        public string? PrimaryDeptName { get; set; }
        public string? CompanyName { get; set; }
        public string? MiniappScope { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    /// <summary>从 ClaimsPrincipal 提取业务用户信息</summary>
    public UserInfo Extract(ClaimsPrincipal user) => new()
    {
        Sub = RequireSub(user),
        Username = user.FindFirst("preferred_username")?.Value ?? "",
        Name = user.FindFirst("name")?.Value
               ?? user.FindFirst("given_name")?.Value,
        Email = user.FindFirst("email")?.Value,
        OctopusUserId = user.FindFirst("octopus_user_id")?.Value,
        PrimaryDeptId = user.FindFirst("primary_dept_id")?.Value,
        PrimaryDeptName = user.FindFirst("primary_dept_name")?.Value,
        CompanyName = user.FindFirst("company_name")?.Value,
        MiniappScope = user.FindFirst("miniapp_scope")?.Value,
        Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
    };

    /// <summary>
    /// Lazy Provisioning：首次访问时按 JWT claims INSERT 本地 user_profile；
    /// 之后访问只更新 LastLoginAt。
    /// </summary>
    public async Task<UserProfile> EnsureProfileAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var info = Extract(user);
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.KcSub == info.Sub, ct);
        if (profile is null)
        {
            profile = new UserProfile
            {
                KcSub = info.Sub,
                SnapshotName = info.Name ?? info.Username,
                SnapshotEmail = info.Email,
                SnapshotDept = info.PrimaryDeptName,
                SnapshotCompany = info.CompanyName,
                LastLoginAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            profile.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return profile;
    }
}
