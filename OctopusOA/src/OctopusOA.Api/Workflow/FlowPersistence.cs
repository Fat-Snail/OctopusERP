namespace OctopusOA.Api.Workflow;

// ── 审批中心：图流程持久化实体（wf_ 前缀，与业务数据分离） ──

/// <summary>流程定义（版本化，GraphJson 存图）</summary>
public class FlowDefinition
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Version { get; set; } = 1;
    public int Status { get; set; } = 1;            // 1 启用 0 停用
    public string GraphJson { get; set; } = "{}";
    public string FormSchema { get; set; } = "{}";
    public string? Category { get; set; }
    /// <summary>业务键类型（如 purchase_order），用于子系统识别与事件路由</summary>
    public string? BusinessKey { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>流程实例（一次发起）</summary>
public class FlowInstance
{
    public long Id { get; set; }
    public long DefinitionId { get; set; }
    public int DefinitionVersion { get; set; } = 1;
    /// <summary>关联子系统单据键，如 "purchase_order:123"</summary>
    public string? BusinessKey { get; set; }
    public string Title { get; set; } = "";
    public long ApplicantId { get; set; }
    public string ApplicantName { get; set; } = "";
    public string Status { get; set; } = "pending";  // draft/pending/approved/rejected/cancelled
    public string FormData { get; set; } = "{}";
    /// <summary>运行时流程变量（表单之外，如子系统注入的上下文）</summary>
    public string Variables { get; set; } = "{}";
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
    /// <summary>当前节点 SLA 截止时间</summary>
    public DateTime? Deadline { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsSlaWarned { get; set; }
}

/// <summary>执行令牌（token）：承载流程在节点间的推进；并行分叉产生多个令牌</summary>
public class FlowExecution
{
    public long Id { get; set; }
    public long InstanceId { get; set; }
    public string NodeId { get; set; } = "";
    public long? ParentId { get; set; }
    /// <summary>并行分叉组 id（同一分叉的兄弟令牌共享）</summary>
    public string? ForkGroup { get; set; }
    /// <summary>分叉组需要汇聚的令牌数（用于并行网关 join）</summary>
    public int JoinExpected { get; set; }
    public string Status { get; set; } = "active";   // active/completed/cancelled
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>人工任务（会签/或签/单人的最小执行单元）</summary>
public class FlowTask
{
    public long Id { get; set; }
    public long InstanceId { get; set; }
    public string NodeId { get; set; } = "";
    public long? ExecutionId { get; set; }
    public long AssigneeId { get; set; }
    public string AssigneeName { get; set; } = "";
    public string Status { get; set; } = "pending";  // pending/approved/rejected/cancelled
    public string? Action { get; set; }
    public string? Comment { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? ActionTime { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>流程历史轨迹（只读，供详情页时间线）</summary>
public class FlowHistory
{
    public long Id { get; set; }
    public long InstanceId { get; set; }
    public string NodeId { get; set; } = "";
    public string NodeName { get; set; } = "";
    public string Action { get; set; } = "";         // approve/reject/skip/complete/...
    public long ActorId { get; set; }
    public string ActorName { get; set; } = "";
    public string? Comment { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
