using System.Globalization;
using System.Text.Json;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Workflow;

public record StartFlowRequest(
    long DefinitionId,
    string Title,
    long ApplicantId,
    string ApplicantName,
    string FormData,
    string? BusinessKey = null,
    string? Variables = null,
    string? DefinitionCode = null);

public interface IFlowEngine
{
    (FlowInstance? instance, string? error) Start(StartFlowRequest req);
    string? CompleteTask(long taskId, long userId, string userName, string action, string? comment);
    string? Cancel(long instanceId, long applicantId);
    List<FlowTask> GetPendingTasks(long userId);
    FlowInstance? GetInstance(long instanceId);

    /// <summary>委托/转交：把当前任务转给他人处理（任务本身不推进流程）</summary>
    string? DelegateTask(long taskId, long fromUserId, string fromUserName, long toUserId, string toUserName, string? comment);
    /// <summary>加签：在当前节点追加一名协办人（其处理完后节点才算完成）</summary>
    string? AddSignTask(long taskId, long fromUserId, string fromUserName, long toUserId, string toUserName, string? comment);
    /// <summary>驳回到指定节点：退回重审（取消当前流程尾部任务，在目标节点重新放置任务）</summary>
    string? RejectTo(long taskId, long userId, string userName, string targetNodeId, string? comment);
}

/// <summary>
/// 轻量图流程引擎（自研）：令牌(token)模型 + 排他/并行网关 + 单人/会签/或签。
/// 无 HTTP/Controller 依赖，可嵌入 OA 或未来独立为 OctopusFlow 服务。
/// </summary>
public class FlowEngine : IFlowEngine
{
    private readonly OaDbContext _db;
    private readonly ApproverStrategyRegistry _strategies;
    private readonly ServiceTaskHandlerRegistry _serviceTasks;
    private readonly ProcessEventDispatcher _events;

    public FlowEngine(OaDbContext db, ApproverStrategyRegistry strategies, ServiceTaskHandlerRegistry serviceTasks, ProcessEventDispatcher events)
    {
        _db = db;
        _strategies = strategies;
        _serviceTasks = serviceTasks;
        _events = events;
    }

    public (FlowInstance? instance, string? error) Start(StartFlowRequest req)
    {
        var def = req.DefinitionId > 0
            ? _db.FlowDefinitions.FirstOrDefault(d => d.Id == req.DefinitionId && d.Status == 1)
            : (string.IsNullOrWhiteSpace(req.DefinitionCode)
                ? null
                : _db.FlowDefinitions.FirstOrDefault(d => d.Code == req.DefinitionCode && d.Status == 1));
        if (def == null) return (null, "流程定义不存在或已停用");

        var graph = FlowGraph.FromJson(def.GraphJson);
        var start = graph.Nodes.FirstOrDefault(n => n.Type == FlowNodeTypes.Start);
        if (start == null) return (null, "流程缺少开始节点");

        var instance = new FlowInstance
        {
            DefinitionId = def.Id,
            DefinitionVersion = def.Version,
            BusinessKey = req.BusinessKey,
            Title = req.Title,
            ApplicantId = req.ApplicantId,
            ApplicantName = req.ApplicantName,
            Status = "pending",
            FormData = req.FormData,
            Variables = req.Variables ?? "{}",
        };
        _db.FlowInstances.Add(instance);
        _db.SaveChanges();

        var exec = new FlowExecution { InstanceId = instance.Id, NodeId = start.Id, Status = "active" };
        _db.FlowExecutions.Add(exec);
        _db.SaveChanges();

        var err = AdvanceExecution(exec, graph, instance);
        if (err != null)
        {
            instance.Status = "cancelled";
            return (null, err);
        }
        _db.SaveChanges();
        FireStatusEvent(instance);
        return (instance, null);
    }

    public string? CompleteTask(long taskId, long userId, string userName, string action, string? comment)
    {
        var task = _db.FlowTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return "任务不存在";
        if (task.Status != "pending") return "任务已处理";
        if (task.AssigneeId != userId) return "您无权处理此任务";

        var instance = _db.FlowInstances.FirstOrDefault(i => i.Id == task.InstanceId);
        if (instance == null || instance.Status != "pending") return "流程实例状态异常";

        var def = _db.FlowDefinitions.FirstOrDefault(d => d.Id == instance.DefinitionId);
        if (def == null) return "流程定义不存在";

        var graph = FlowGraph.FromJson(def.GraphJson);
        var node = graph.Node(task.NodeId);
        if (node == null) return "节点不存在";

        if (action == "reject")
        {
            task.Status = "rejected";
            task.Action = "reject";
            task.Comment = comment;
            task.ActionTime = DateTime.UtcNow;
            RecordHistory(instance, node, "reject", userId, userName, comment);
            _db.SaveChanges();

            // 或签：一个驳回不立刻结束，只有全部驳回才结束；单人/会签：任一驳回即结束
            var shouldReject = node.Type != FlowNodeTypes.OrSign
                || !_db.FlowTasks.Any(t => t.InstanceId == instance.Id && t.NodeId == node.Id && t.Status == "pending");
            if (shouldReject) RejectInstance(instance);

            _db.SaveChanges();
            FireStatusEvent(instance);
            return null;
        }

        // approve
        task.Status = "approved";
        task.Action = "approve";
        task.Comment = comment;
        task.ActionTime = DateTime.UtcNow;
        RecordHistory(instance, node, "approve", userId, userName, comment);
        _db.SaveChanges();

        if (IsNodeSatisfied(node, instance))
        {
            CancelPendingTasksAtNode(instance, node);
            _db.SaveChanges();

            var exec = _db.FlowExecutions.FirstOrDefault(e => e.Id == task.ExecutionId);
            if (exec != null && exec.Status == "active")
            {
                var outEdge = graph.OutEdge(node.Id);
                if (outEdge == null) return $"节点缺少出口: {node.Id}";
                exec.NodeId = outEdge.To;
                var err = AdvanceExecution(exec, graph, instance);
                if (err != null) return err;
            }
        }

        _db.SaveChanges();
        FireStatusEvent(instance);
        return null;
    }

    public string? Cancel(long instanceId, long applicantId)
    {
        var instance = _db.FlowInstances.FirstOrDefault(i => i.Id == instanceId);
        if (instance == null) return "流程实例不存在";
        if (instance.ApplicantId != applicantId) return "只有申请人可以撤回";
        if (instance.Status != "pending") return "只有审批中状态可以撤回";

        instance.Status = "cancelled";
        instance.UpdateTime = DateTime.UtcNow;
        instance.Deadline = null;
        CancelActive(instance);
        _db.SaveChanges();
        FireStatusEvent(instance);
        return null;
    }

    public string? DelegateTask(long taskId, long fromUserId, string fromUserName, long toUserId, string toUserName, string? comment)
    {
        var task = _db.FlowTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return "任务不存在";
        if (task.Status != "pending") return "任务已处理";
        if (task.AssigneeId != fromUserId) return "您无权转交此任务";
        if (toUserId == fromUserId) return "不能转交给自己";

        var instance = _db.FlowInstances.FirstOrDefault(i => i.Id == task.InstanceId);
        if (instance == null || instance.Status != "pending") return "流程实例状态异常";

        var oldName = task.AssigneeName;
        task.AssigneeId = toUserId;
        task.AssigneeName = toUserName;
        task.Action = "delegate";
        task.Comment = comment;
        RecordHistory(instance, _db.FlowDefinitions.FirstOrDefault(d => d.Id == instance.DefinitionId) is { } def
            ? FlowGraph.FromJson(def.GraphJson).Node(task.NodeId) ?? new FlowNode { Id = task.NodeId, Name = task.NodeId }
            : new FlowNode { Id = task.NodeId, Name = task.NodeId },
            "delegate", fromUserId, fromUserName, $"转交 {oldName} → {toUserName}{(string.IsNullOrWhiteSpace(comment) ? "" : $"：{comment}")}");
        _db.SaveChanges();
        FireStatusEvent(instance);
        return null;
    }

    public string? AddSignTask(long taskId, long fromUserId, string fromUserName, long toUserId, string toUserName, string? comment)
    {
        var task = _db.FlowTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return "任务不存在";
        if (task.Status != "pending") return "任务已处理";
        if (task.AssigneeId != fromUserId) return "您无权加签此任务";
        if (toUserId == fromUserId) return "已在当前处理人中";

        var dup = _db.FlowTasks.FirstOrDefault(t => t.InstanceId == task.InstanceId && t.NodeId == task.NodeId
            && t.AssigneeId == toUserId && t.Status == "pending");
        if (dup != null) return "该用户已在当前节点待办中";

        var instance = _db.FlowInstances.FirstOrDefault(i => i.Id == task.InstanceId);
        if (instance == null || instance.Status != "pending") return "流程实例状态异常";

        _db.FlowTasks.Add(new FlowTask
        {
            InstanceId = task.InstanceId,
            NodeId = task.NodeId,
            ExecutionId = task.ExecutionId,
            AssigneeId = toUserId,
            AssigneeName = toUserName,
            Status = "pending",
            TaskKind = "add_sign",
            Comment = comment,
            Deadline = task.Deadline,
        });
        var def0 = _db.FlowDefinitions.FirstOrDefault(d => d.Id == instance.DefinitionId);
        var node0 = def0 != null ? FlowGraph.FromJson(def0.GraphJson).Node(task.NodeId) : null;
        RecordHistory(instance, node0 ?? new FlowNode { Id = task.NodeId, Name = task.NodeId },
            "add_sign", fromUserId, fromUserName, $"加签 {toUserName}{(string.IsNullOrWhiteSpace(comment) ? "" : $"：{comment}")}");
        _db.SaveChanges();
        FireStatusEvent(instance);
        return null;
    }

    public string? RejectTo(long taskId, long userId, string userName, string targetNodeId, string? comment)
    {
        var task = _db.FlowTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return "任务不存在";
        if (task.Status != "pending") return "任务已处理";
        if (task.AssigneeId != userId) return "您无权驳回此任务";

        var instance = _db.FlowInstances.FirstOrDefault(i => i.Id == task.InstanceId);
        if (instance == null || instance.Status != "pending") return "流程实例状态异常";
        var def = _db.FlowDefinitions.FirstOrDefault(d => d.Id == instance.DefinitionId);
        if (def == null) return "流程定义不存在";
        var graph = FlowGraph.FromJson(def.GraphJson);
        var target = graph.Node(targetNodeId);
        if (target == null) return "目标节点不存在";
        if (target.Type is FlowNodeTypes.Start or FlowNodeTypes.End) return "不能驳回到开始或结束节点";
        if (targetNodeId == task.NodeId) return "不能驳回到当前节点";

        // 记录驳回历史（当前任务）
        RecordHistory(instance, graph.Node(task.NodeId) ?? new FlowNode { Id = task.NodeId, Name = task.NodeId },
            "reject_to", userId, userName, $"驳回至「{target.Name}」{(string.IsNullOrWhiteSpace(comment) ? "" : $"：{comment}")}");

        // 取消所有 active 执行与 pending 任务（整条流程尾部重置）
        foreach (var e in _db.FlowExecutions.Where(e => e.InstanceId == instance.Id && e.Status == "active"))
            e.Status = "cancelled";
        foreach (var t in _db.FlowTasks.Where(t => t.InstanceId == instance.Id && t.Status == "pending"))
        {
            t.Status = "cancelled";
            t.Action = "cancelled";
        }

        // 在当前任务的执行令牌上重置到目标节点，重新放置任务
        var exec = task.ExecutionId.HasValue
            ? _db.FlowExecutions.FirstOrDefault(e => e.Id == task.ExecutionId.Value)
            : null;
        if (exec == null)
            exec = new FlowExecution { InstanceId = instance.Id, NodeId = target.Id };
        exec.NodeId = target.Id;
        exec.Status = "active";
        if (exec.Id == 0) _db.FlowExecutions.Add(exec);

        instance.UpdateTime = DateTime.UtcNow;
        _db.SaveChanges();

        var err = PlaceTasksAtNode(exec, target, graph, instance);
        if (err != null) return err;
        _db.SaveChanges();
        FireStatusEvent(instance);
        return null;
    }

    public List<FlowTask> GetPendingTasks(long userId)
        => _db.FlowTasks.Where(t => t.Status == "pending" && t.AssigneeId == userId).ToList();

    public FlowInstance? GetInstance(long instanceId)
        => _db.FlowInstances.FirstOrDefault(i => i.Id == instanceId);

    // ── 令牌推进 ──────────────────────────────────────────

    private string? AdvanceExecution(FlowExecution exec, FlowGraph graph, FlowInstance instance)
    {
        var guard = 0;
        while (true)
        {
            if (++guard > 1000) return "流程存在循环或节点过多";

            var node = graph.Node(exec.NodeId);
            if (node == null) return $"节点不存在: {exec.NodeId}";

            switch (node.Type)
            {
                case FlowNodeTypes.Start:
                    var firstEdge = graph.OutEdge(node.Id);
                    if (firstEdge == null) return $"开始节点缺少出口: {node.Id}";
                    exec.NodeId = firstEdge.To;
                    continue;

                case FlowNodeTypes.End:
                    exec.Status = "completed";
                    _db.SaveChanges();
                    CompleteInstanceIfAllDone(instance);
                    return null;

                case FlowNodeTypes.Approval:
                case FlowNodeTypes.CounterSign:
                case FlowNodeTypes.OrSign:
                    return PlaceTasksAtNode(exec, node, graph, instance);

                case FlowNodeTypes.ExclusiveGateway:
                    var next = PickExclusiveTarget(node, graph, instance);
                    if (next == null) return $"排他网关无可用分支: {node.Id}";
                    exec.NodeId = next.Id;
                    continue;

                case FlowNodeTypes.ParallelGateway:
                    var perr = HandleParallelGateway(exec, node, graph, instance);
                    if (perr != null) return perr;
                    return null;

                case FlowNodeTypes.ServiceTask:
                    return ExecuteServiceTask(exec, node, graph, instance);

                default:
                    return $"未知节点类型: {node.Type}";
            }
        }
    }

    private string? PlaceTasksAtNode(FlowExecution exec, FlowNode node, FlowGraph graph, FlowInstance instance)
    {
        var config = node.Config ?? new FlowNodeConfig();
        var assignees = _strategies.Resolve(config, instance, _db).Distinct().ToList();

        if (assignees.Count == 0)
        {
            RecordHistory(instance, node, "skip", 0, "system", "无审批人，自动跳过");
            var outEdge = graph.OutEdge(node.Id);
            if (outEdge == null) return $"节点缺少出口: {node.Id}";
            exec.NodeId = outEdge.To;
            return AdvanceExecution(exec, graph, instance);
        }

        foreach (var uid in assignees)
        {
            var name = _db.SyncUsers.FirstOrDefault(u => u.UmcUserId == uid)?.NickName ?? uid.ToString();
            _db.FlowTasks.Add(new FlowTask
            {
                InstanceId = instance.Id,
                NodeId = node.Id,
                ExecutionId = exec.Id,
                AssigneeId = uid,
                AssigneeName = name,
                Status = "pending",
                Deadline = config.SlaHours.HasValue ? DateTime.UtcNow.AddHours(config.SlaHours.Value) : null,
            });
        }
        instance.Deadline = config.SlaHours.HasValue ? DateTime.UtcNow.AddHours(config.SlaHours.Value) : null;
        _db.SaveChanges();
        return null;
    }

    private string? HandleParallelGateway(FlowExecution exec, FlowNode node, FlowGraph graph, FlowInstance instance)
    {
        var incoming = graph.InEdges(node.Id).Count;
        var outgoing = graph.OutEdges(node.Id);

        if (outgoing.Count > 1)
        {
            // 分叉：生成一组子令牌
            var forkGroup = Guid.NewGuid().ToString("N");
            exec.Status = "completed";
            var children = new List<FlowExecution>();
            foreach (var edge in outgoing)
            {
                var child = new FlowExecution
                {
                    InstanceId = instance.Id,
                    NodeId = edge.To,
                    ParentId = exec.Id,
                    ForkGroup = forkGroup,
                    JoinExpected = outgoing.Count,
                    Status = "active",
                };
                _db.FlowExecutions.Add(child);
                children.Add(child);
            }
            _db.SaveChanges();
            foreach (var child in children)
            {
                var err = AdvanceExecution(child, graph, instance);
                if (err != null) return err;
            }
            return null;
        }

        if (incoming > 1)
        {
            // 汇合：等同一分叉组的令牌到齐后，合并为单个令牌继续
            exec.Status = "completed";
            _db.SaveChanges();

            var arrived = _db.FlowExecutions.Count(e =>
                e.InstanceId == instance.Id &&
                e.ForkGroup == exec.ForkGroup &&
                e.NodeId == node.Id &&
                e.Status == "completed");

            if (arrived >= exec.JoinExpected)
            {
                var outEdge = graph.OutEdge(node.Id);
                if (outEdge == null) return $"并行汇合节点缺少出口: {node.Id}";
                var merged = new FlowExecution { InstanceId = instance.Id, NodeId = outEdge.To, Status = "active" };
                _db.FlowExecutions.Add(merged);
                _db.SaveChanges();
                return AdvanceExecution(merged, graph, instance);
            }
            return null;
        }

        return $"并行网关配置错误（既非分叉也非汇合）: {node.Id}";
    }

    private string? ExecuteServiceTask(FlowExecution exec, FlowNode node, FlowGraph graph, FlowInstance instance)
    {
        var config = node.Config ?? new FlowNodeConfig();
        string? err = null;
        if (!string.IsNullOrEmpty(config.ServiceKind))
        {
            var handler = _serviceTasks.Get(config.ServiceKind);
            if (handler == null) return $"未注册的服务任务: {config.ServiceKind}";
            err = handler.Execute(new ServiceTaskContext(instance, node, config, _db));
        }

        RecordHistory(instance, node, err == null ? "complete" : "error", 0, "system", err);
        if (err != null) return err;

        var outEdge = graph.OutEdge(node.Id);
        if (outEdge == null) return $"服务任务缺少出口: {node.Id}";
        exec.NodeId = outEdge.To;
        return AdvanceExecution(exec, graph, instance);
    }

    /// <summary>按实例最终状态分发生命周期事件（供二次开发监听器做跨系统联动）</summary>
    private void FireStatusEvent(FlowInstance instance)
    {
        var def = _db.FlowDefinitions.FirstOrDefault(d => d.Id == instance.DefinitionId);
        if (def == null) return;
        var type = instance.Status switch
        {
            "approved" => ProcessEventTypes.Approved,
            "rejected" => ProcessEventTypes.Rejected,
            "cancelled" => ProcessEventTypes.Cancelled,
            _ => null,
        };
        if (type != null) _events.Dispatch(type, instance, def);
    }

    private FlowNode? PickExclusiveTarget(FlowNode node, FlowGraph graph, FlowInstance instance)
    {
        var edges = graph.OutEdges(node.Id);
        var data = MergeVariables(instance);

        foreach (var edge in edges)
        {
            if (string.IsNullOrWhiteSpace(edge.Condition)) continue;
            if (ExpressionEvaluator.Evaluate(edge.Condition, data))
                return graph.Node(edge.To);
        }

        var defaultEdge = edges.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Condition));
        return defaultEdge == null ? null : graph.Node(defaultEdge.To);
    }

    // ── 节点/实例状态判定 ──────────────────────────────────

    private bool IsNodeSatisfied(FlowNode node, FlowInstance instance)
    {
        var hasAddSign = _db.FlowTasks.Any(t => t.InstanceId == instance.Id && t.NodeId == node.Id && t.TaskKind == "add_sign");
        return node.Type switch
        {
            FlowNodeTypes.CounterSign => !_db.FlowTasks.Any(t =>
                t.InstanceId == instance.Id && t.NodeId == node.Id && t.Status == "pending"),
            _ when hasAddSign => !_db.FlowTasks.Any(t =>   // 有加签：所有人（含加签）都处理完才满足
                t.InstanceId == instance.Id && t.NodeId == node.Id && t.Status == "pending"),
            _ => true, // approval / or_sign：任一通过即满足（单人亦成立）
        };
    }

    private void CancelPendingTasksAtNode(FlowInstance instance, FlowNode node)
    {
        var remaining = _db.FlowTasks
            .Where(t => t.InstanceId == instance.Id && t.NodeId == node.Id && t.Status == "pending")
            .ToList();
        foreach (var t in remaining)
        {
            t.Status = "cancelled";
            t.Action = "cancelled";
        }
    }

    private void RejectInstance(FlowInstance instance)
    {
        instance.Status = "rejected";
        instance.UpdateTime = DateTime.UtcNow;
        instance.Deadline = null;
        CancelActive(instance);
    }

    private void CancelActive(FlowInstance instance)
    {
        foreach (var e in _db.FlowExecutions.Where(e => e.InstanceId == instance.Id && e.Status == "active"))
            e.Status = "cancelled";
        foreach (var t in _db.FlowTasks.Where(t => t.InstanceId == instance.Id && t.Status == "pending"))
        {
            t.Status = "cancelled";
            t.Action = "cancelled";
        }
    }

    private void CompleteInstanceIfAllDone(FlowInstance instance)
    {
        if (instance.Status != "pending") return;
        var anyActive = _db.FlowExecutions.Any(e => e.InstanceId == instance.Id && e.Status == "active");
        if (!anyActive)
        {
            instance.Status = "approved";
            instance.UpdateTime = DateTime.UtcNow;
            instance.Deadline = null;
            instance.IsOverdue = false;
        }
    }

    private void RecordHistory(FlowInstance instance, FlowNode node, string action, long actorId, string actorName, string? comment)
    {
        _db.FlowHistories.Add(new FlowHistory
        {
            InstanceId = instance.Id,
            NodeId = node.Id,
            NodeName = node.Name,
            Action = action,
            ActorId = actorId,
            ActorName = actorName,
            Comment = comment,
        });
    }

    private Dictionary<string, object?> MergeVariables(FlowInstance instance)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        AddJsonObject(instance.FormData, data);
        AddJsonObject(instance.Variables, data);
        data["applicantId"] = instance.ApplicantId;
        data["applicantName"] = instance.ApplicantName;
        data["title"] = instance.Title;
        return data;
    }

    private static void AddJsonObject(string json, Dictionary<string, object?> target)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (dict == null) return;
            foreach (var kv in dict) target[kv.Key] = Normalize(kv.Value);
        }
        catch { /* 非法 JSON 忽略 */ }
    }

    private static object? Normalize(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.ToString(),
    };
}
