namespace MiniappTodo.Api.Persistence;

/// <summary>
/// 小程序 Todo 业务表。
/// 关键设计：
/// - 主键自增 long
/// - KcSub 是 Keycloak JWT 的 sub claim（UUID 字符串），作为"用户外键"
/// - 不存 UMC UserId，不查 UMC 用户表，所有用户上下文从 JWT claims 拿
/// - SnapshotName / SnapshotDept 是首次见到该用户时的快照，避免每次都从 token 取
/// </summary>
public class TodoItem
{
    public long TodoId { get; set; }
    public string KcSub { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DueDate { get; set; }
    public int Priority { get; set; } = 3;   // 1-5（5 最高）
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// 用户本地 profile（Lazy Provisioning）— 首次进小程序时按 JWT claims 创建。
/// 业务上其他表用 KcSub 关联即可，UserProfile 仅做名称/部门快照展示。
/// </summary>
public class UserProfile
{
    public string KcSub { get; set; } = string.Empty;
    public string SnapshotName { get; set; } = string.Empty;
    public string? SnapshotEmail { get; set; }
    public string? SnapshotDept { get; set; }
    public string? SnapshotCompany { get; set; }
    public DateTime LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
