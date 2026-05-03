using Microsoft.Extensions.DependencyInjection;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Services;

/// <summary>审批流引擎：提交、审批、驳回、撤回、审批人解析</summary>
public class ApprovalService
{
    private readonly OaDbContext _db;
    private readonly IServiceProvider _sp;

    public ApprovalService(OaDbContext db, IServiceProvider sp)
    {
        _db = db;
        _sp = sp;
    }

    /// <summary>提交审批申请</summary>
    public (Approval? approval, string? error) Submit(long applicantId, string applicantName, long templateId, string title, string formData)
    {
        var template = _db.Templates.FirstOrDefault(t => t.TemplateId == templateId && t.Status == 1);
        if (template == null) return (null, "模板不存在或已停用");

        var nodes = _db.Nodes.Where(n => n.TemplateId == templateId && n.Status == 1).OrderBy(n => n.NodeOrder).ToList();
        if (nodes.Count == 0) return (null, "模板未配置审批节点");

        var approval = new Approval
        {
            TemplateId = templateId,
            Title = title,
            ApplicantId = applicantId,
            ApplicantName = applicantName,
            CurrentNodeOrder = 1,
            Status = "pending",
            FormData = formData,
            CreateTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow,
        };

        _db.Approvals.Add(approval);
        _db.SaveChanges();
        return (approval, null);
    }

    /// <summary>审批通过</summary>
    public string? Approve(long approvalId, long approverId, string approverName, string? comment)
    {
        var approval = _db.Approvals.FirstOrDefault(a => a.ApprovalId == approvalId);
        if (approval == null) return "审批不存在";
        if (approval.Status != "pending") return "审批状态不是待审批";

        var canApprove = CanApproveCurrentNode(approval, approverId);
        if (!canApprove) return "您无权审批此节点";

        var currentNode = GetCurrentNode(approval);
        if (currentNode == null) return "当前审批节点不存在";

        _db.ApprovalRecords.Add(new ApprovalRecord
        {
            ApprovalId = approvalId,
            NodeId = currentNode.NodeId,
            NodeOrder = currentNode.NodeOrder,
            ApproverId = approverId,
            ApproverName = approverName,
            Action = "approve",
            Comment = comment,
            ActionTime = DateTime.UtcNow,
        });

        var nodes = _db.Nodes.Where(n => n.TemplateId == approval.TemplateId && n.Status == 1)
            .OrderBy(n => n.NodeOrder).ToList();
        var nextNode = nodes.FirstOrDefault(n => n.NodeOrder > approval.CurrentNodeOrder);

        if (nextNode != null)
        {
            approval.CurrentNodeOrder = nextNode.NodeOrder;
        }
        else
        {
            approval.Status = "approved";
        }

        approval.UpdateTime = DateTime.UtcNow;
        _db.SaveChanges();

        // 审批最终通过：按模板类型联动业务
        if (approval.Status == "approved")
        {
            var template = _db.Templates.FirstOrDefault(t => t.TemplateId == approval.TemplateId);
            if (template?.TemplateCode == "attendance_fix")
            {
                _sp.GetRequiredService<AttendanceService>().HandleApprovalFix(approval);
            }
        }
        return null;
    }

    /// <summary>驳回</summary>
    public string? Reject(long approvalId, long approverId, string approverName, string? comment)
    {
        var approval = _db.Approvals.FirstOrDefault(a => a.ApprovalId == approvalId);
        if (approval == null) return "审批不存在";
        if (approval.Status != "pending") return "审批状态不是待审批";

        var canApprove = CanApproveCurrentNode(approval, approverId);
        if (!canApprove) return "您无权审批此节点";

        var currentNode = GetCurrentNode(approval);
        if (currentNode == null) return "当前审批节点不存在";

        _db.ApprovalRecords.Add(new ApprovalRecord
        {
            ApprovalId = approvalId,
            NodeId = currentNode.NodeId,
            NodeOrder = currentNode.NodeOrder,
            ApproverId = approverId,
            ApproverName = approverName,
            Action = "reject",
            Comment = comment,
            ActionTime = DateTime.UtcNow,
        });

        approval.Status = "rejected";
        approval.UpdateTime = DateTime.UtcNow;
        _db.SaveChanges();
        return null;
    }

    /// <summary>申请人撤回</summary>
    public string? Cancel(long approvalId, long applicantId)
    {
        var approval = _db.Approvals.FirstOrDefault(a => a.ApprovalId == approvalId);
        if (approval == null) return "审批不存在";
        if (approval.ApplicantId != applicantId) return "只有申请人可以撤回";
        if (approval.Status != "pending") return "只有待审批状态可以撤回";

        approval.Status = "cancelled";
        approval.UpdateTime = DateTime.UtcNow;
        _db.SaveChanges();
        return null;
    }

    /// <summary>判断用户是否可以审批当前节点</summary>
    public bool CanApproveCurrentNode(Approval approval, long userId)
    {
        var currentNode = GetCurrentNode(approval);
        if (currentNode == null) return false;

        var user = _db.SyncUsers.FirstOrDefault(u => u.UmcUserId == userId);

        return currentNode.ApproverType switch
        {
            "user" => currentNode.ApproverValue == userId.ToString(),
            "role" => user?.OaRoles.Contains(currentNode.ApproverValue ?? "") == true,
            "dept_leader" => user?.OaRoles.Any(r => r is "oa_admin" or "oa_manager") == true,
            _ => false,
        };
    }

    /// <summary>获取当前审批节点</summary>
    public WorkflowNode? GetCurrentNode(Approval approval)
    {
        return _db.Nodes.FirstOrDefault(n =>
            n.TemplateId == approval.TemplateId &&
            n.NodeOrder == approval.CurrentNodeOrder &&
            n.Status == 1);
    }

    /// <summary>获取用户待审批列表</summary>
    public List<Approval> GetPendingForUser(long userId)
    {
        return _db.Approvals
            .Where(a => a.Status == "pending")
            .ToList()
            .Where(a => CanApproveCurrentNode(a, userId))
            .ToList();
    }
}
