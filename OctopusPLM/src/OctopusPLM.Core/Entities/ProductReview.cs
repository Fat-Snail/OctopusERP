namespace OctopusPLM.Core.Entities;

/// <summary>商品审核记录</summary>
public class ProductReview
{
    public long ReviewId { get; set; }
    public long ProductId { get; set; }
    public long ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;

    /// <summary>submit / approve / reject / cancel</summary>
    public string Action { get; set; } = string.Empty;

    public string? Comment { get; set; }

    /// <summary>审核节点（对接 OA 审批流时映射到 WorkflowNodeId）</summary>
    public long? WorkflowNodeId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
}
