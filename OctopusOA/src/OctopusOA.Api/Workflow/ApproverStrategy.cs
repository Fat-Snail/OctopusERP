using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Workflow;

/// <summary>审批人策略：把节点配置解析为待审批人列表（可扩展，见 ApproverStrategyRegistry.Register）</summary>
public interface IApproverStrategy
{
    string Name { get; }
    IReadOnlyList<long> Resolve(FlowNodeConfig config, FlowInstance instance, OaDbContext db);
}

/// <summary>审批人策略注册表：内置策略 + 二次开发可注册自定义策略</summary>
public sealed class ApproverStrategyRegistry
{
    private readonly Dictionary<string, IApproverStrategy> _map = new(StringComparer.OrdinalIgnoreCase);

    public ApproverStrategyRegistry()
    {
        Register(new UserStrategy());
        Register(new RoleStrategy());
        Register(new DeptLeaderStrategy());
    }

    public void Register(IApproverStrategy strategy) => _map[strategy.Name] = strategy;

    public IReadOnlyList<long> Resolve(FlowNodeConfig config, FlowInstance instance, OaDbContext db)
        => _map.TryGetValue(config.ApproverType, out var s)
            ? s.Resolve(config, instance, db)
            : Array.Empty<long>();
}

public sealed class UserStrategy : IApproverStrategy
{
    public string Name => "user";
    public IReadOnlyList<long> Resolve(FlowNodeConfig config, FlowInstance instance, OaDbContext db)
        => long.TryParse(config.ApproverValue, out var id) ? new[] { id } : Array.Empty<long>();
}

public sealed class RoleStrategy : IApproverStrategy
{
    public string Name => "role";
    public IReadOnlyList<long> Resolve(FlowNodeConfig config, FlowInstance instance, OaDbContext db)
    {
        var role = config.ApproverValue ?? "";
        // OaRoles 是 JSON 列，先加载到内存再过滤（与现有 ApprovalService 一致）
        return db.SyncUsers.ToList()
            .Where(u => u.OaRoles.Contains(role))
            .Select(u => u.UmcUserId)
            .ToList();
    }
}

public sealed class DeptLeaderStrategy : IApproverStrategy
{
    public string Name => "dept_leader";
    public IReadOnlyList<long> Resolve(FlowNodeConfig config, FlowInstance instance, OaDbContext db)
    {
        // 与现有 CanApproveCurrentNode 语义一致：oa_admin / oa_manager 可审批
        return db.SyncUsers.ToList()
            .Where(u => u.OaRoles.Any(r => r is "oa_admin" or "oa_manager"))
            .Select(u => u.UmcUserId)
            .ToList();
    }
}
