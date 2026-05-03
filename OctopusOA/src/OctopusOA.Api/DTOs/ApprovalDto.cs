namespace OctopusOA.Api.DTOs;

// ── 模板 ──────────────────────────────────────────

public class TemplateResponse
{
    public long TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string FormSchema { get; set; } = "{}";
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public List<NodeResponse> Nodes { get; set; } = new();
}

public class NodeResponse
{
    public long NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public int NodeOrder { get; set; }
    public string ApproverType { get; set; } = string.Empty;
    public string? ApproverValue { get; set; }
}

public class CreateTemplateRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string FormSchema { get; set; } = "{}";
    public int Status { get; set; } = 1;
}

public class UpdateTemplateRequest : CreateTemplateRequest
{
    public long TemplateId { get; set; }
}

public class SetNodesRequest
{
    public List<NodeRequest> Nodes { get; set; } = new();
}

public class NodeRequest
{
    public string NodeName { get; set; } = string.Empty;
    public int NodeOrder { get; set; }
    public string ApproverType { get; set; } = "role";
    public string? ApproverValue { get; set; }
}

// ── 审批 ──────────────────────────────────────────

public class SubmitApprovalRequest
{
    public long TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FormData { get; set; } = "{}";
}

public class ApproveRequest
{
    public string? Comment { get; set; }
}

public class RejectRequest
{
    public string? Comment { get; set; }
}

public class ApprovalResponse
{
    public long ApprovalId { get; set; }
    public long TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long ApplicantId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public int CurrentNodeOrder { get; set; }
    public string? CurrentNodeName { get; set; }
    public int TotalNodes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FormData { get; set; } = "{}";
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<RecordResponse> Records { get; set; } = new();
}

public class RecordResponse
{
    public long RecordId { get; set; }
    public int NodeOrder { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public long ApproverId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime ActionTime { get; set; }
}
