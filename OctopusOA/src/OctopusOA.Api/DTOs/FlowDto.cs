namespace OctopusOA.Api.DTOs;

// ── 审批中心（图流程）DTO ──────────────────────────

public class SubmitFlowRequest
{
    public long DefinitionId { get; set; }
    /// <summary>按流程编码发起（子系统集成时无需知道 definitionId）</summary>
    public string? DefinitionCode { get; set; }
    public string Title { get; set; } = "";
    public string FormData { get; set; } = "{}";
    public string? BusinessKey { get; set; }
    public string? Variables { get; set; }
}

public class FlowTaskRequest
{
    public string? Comment { get; set; }
}

/// <summary>委托/加签请求：toUserId 目标用户，toUserName 显示名</summary>
public class FlowDelegateRequest
{
    public long ToUserId { get; set; }
    public string ToUserName { get; set; } = "";
    public string? Comment { get; set; }
}

/// <summary>驳回到指定节点请求</summary>
public class FlowRejectToRequest
{
    public string TargetNodeId { get; set; } = "";
    public string? Comment { get; set; }
}

public class FlowDefinitionResponse
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string? Category { get; set; }
    public string? BusinessKey { get; set; }
    public string FormSchema { get; set; } = "{}";
}

public class SaveFlowDefinitionRequest
{
    /// <summary>0 表示新建，&gt;0 表示更新</summary>
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? BusinessKey { get; set; }
    public string FormSchema { get; set; } = "{}";
    public string GraphJson { get; set; } = "{}";
    public int Status { get; set; } = 1;
}

public class FlowDefinitionDetailResponse : FlowDefinitionResponse
{
    public string GraphJson { get; set; } = "{}";
    public int Status { get; set; } = 1;
}

public class FlowTaskResponse
{
    public long TaskId { get; set; }
    public long InstanceId { get; set; }
    public string NodeId { get; set; } = "";
    public string NodeName { get; set; } = "";
    public string Title { get; set; } = "";
    public long ApplicantId { get; set; }
    public string ApplicantName { get; set; } = "";
    public string? BusinessKey { get; set; }
    public string InstanceStatus { get; set; } = "";
    public DateTime? Deadline { get; set; }
    public DateTime CreateTime { get; set; }
}

public class FlowInstanceResponse
{
    public long InstanceId { get; set; }
    public long DefinitionId { get; set; }
    public string Title { get; set; } = "";
    public long ApplicantId { get; set; }
    public string ApplicantName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? BusinessKey { get; set; }
    public string FormData { get; set; } = "{}";
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public DateTime? Deadline { get; set; }
    public bool IsOverdue { get; set; }
    public List<FlowTaskDetailResponse> Tasks { get; set; } = new();
    public List<FlowHistoryResponse> History { get; set; } = new();
}

public class FlowTaskDetailResponse
{
    public long TaskId { get; set; }
    public string NodeId { get; set; } = "";
    public string NodeName { get; set; } = "";
    public long AssigneeId { get; set; }
    public string AssigneeName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Action { get; set; }
    public string? Comment { get; set; }
    public DateTime? ActionTime { get; set; }
}

public class FlowHistoryResponse
{
    public string NodeName { get; set; } = "";
    public string Action { get; set; } = "";
    public long ActorId { get; set; }
    public string ActorName { get; set; } = "";
    public string? Comment { get; set; }
    public DateTime CreateTime { get; set; }
}
