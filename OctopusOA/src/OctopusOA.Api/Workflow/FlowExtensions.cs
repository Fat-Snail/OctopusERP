using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Workflow;

// ── 审批中心扩展点（二次开发核心） ──
// 业务方实现接口并注册，引擎零改动。对应设计文档 §5。

/// <summary>流程生命周期事件类型</summary>
public static class ProcessEventTypes
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
}

/// <summary>流程事件（携带实例 + 定义，监听器按 businessKey / code 路由）</summary>
public record ProcessEvent(string Type, FlowInstance Instance, FlowDefinition Definition);

/// <summary>流程事件监听器：审批通过/驳回/撤回时触发，业务方做跨系统联动</summary>
public interface IProcessEventListener
{
    void OnEvent(ProcessEvent evt);
}

/// <summary>事件分发器：聚合所有已注册监听器</summary>
public sealed class ProcessEventDispatcher
{
    private readonly List<IProcessEventListener> _listeners;

    public ProcessEventDispatcher(IEnumerable<IProcessEventListener> listeners)
    {
        _listeners = listeners.ToList();
    }

    public void Add(IProcessEventListener listener) => _listeners.Add(listener);

    public void Dispatch(string type, FlowInstance instance, FlowDefinition definition)
    {
        var evt = new ProcessEvent(type, instance, definition);
        foreach (var l in _listeners.ToList()) l.OnEvent(evt);
    }
}

/// <summary>服务任务上下文（节点 + 实例 + 配置 + DbContext）</summary>
public record ServiceTaskContext(FlowInstance Instance, FlowNode Node, FlowNodeConfig Config, OaDbContext Db);

/// <summary>服务任务处理器：service_task 节点执行时调用，按 ServiceKind 路由</summary>
public interface IServiceTaskHandler
{
    string Kind { get; }
    /// <summary>执行；返回 null 表示成功，否则为错误信息（引擎会终止流程）</summary>
    string? Execute(ServiceTaskContext ctx);
}

/// <summary>服务任务注册表：聚合所有已注册处理器</summary>
public sealed class ServiceTaskHandlerRegistry
{
    private readonly Dictionary<string, IServiceTaskHandler> _map = new(StringComparer.OrdinalIgnoreCase);

    public ServiceTaskHandlerRegistry(IEnumerable<IServiceTaskHandler> handlers)
    {
        foreach (var h in handlers) _map[h.Kind] = h;
    }

    public void Register(IServiceTaskHandler handler) => _map[handler.Kind] = handler;
    public IServiceTaskHandler? Get(string kind) => _map.TryGetValue(kind, out var h) ? h : null;
}
