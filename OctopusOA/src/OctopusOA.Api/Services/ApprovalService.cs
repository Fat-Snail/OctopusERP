using Microsoft.Extensions.DependencyInjection;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Services;

/// <summary>审批流引擎：提交、审批、驳回、撤回、审批人解析</summary>
public class ApprovalService
{
    private readonly OaDbContext _db;
    private readonly IServiceProvider _sp;
    private readonly WebhookSender _webhook;
    private readonly NotificationService _notif;

    public ApprovalService(OaDbContext db, IServiceProvider sp, WebhookSender webhook, NotificationService notif)
    {
        _db = db;
        _sp = sp;
        _webhook = webhook;
        _notif = notif;
    }

    /// <summary>提交审批申请</summary>
    public (Approval? approval, string? error) Submit(long applicantId, string applicantName, long templateId, string title, string formData)
    {
        var template = _db.Templates.FirstOrDefault(t => t.TemplateId == templateId && t.Status == 1);
        if (template == null) return (null, "模板不存在或已停用");

        var nodes = _db.Nodes.Where(n => n.TemplateId == templateId && n.Status == 1).OrderBy(n => n.NodeOrder).ToList();
        if (nodes.Count == 0) return (null, "模板未配置审批节点");

        var firstNode = nodes.First();
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
            CurrentNodeDeadline = firstNode.SlaHours.HasValue
                ? DateTime.UtcNow.AddHours(firstNode.SlaHours.Value) : null,
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

        // P17：跳过满足 SkipCondition 的下游节点（如 amount < 1000 → 跳过总经理）
        WorkflowNode? nextNode = null;
        foreach (var n in nodes.Where(n => n.NodeOrder > approval.CurrentNodeOrder))
        {
            if (ShouldSkipNode(n, approval))
            {
                _db.ApprovalRecords.Add(new ApprovalRecord
                {
                    ApprovalId = approvalId,
                    NodeId = n.NodeId,
                    NodeOrder = n.NodeOrder,
                    ApproverId = 0,
                    ApproverName = "system",
                    Action = "skip",
                    Comment = $"满足跳过条件：{n.SkipCondition}",
                    ActionTime = DateTime.UtcNow,
                });
                continue;
            }
            nextNode = n;
            break;
        }

        if (nextNode != null)
        {
            approval.CurrentNodeOrder = nextNode.NodeOrder;
            approval.CurrentNodeDeadline = nextNode.SlaHours.HasValue
                ? DateTime.UtcNow.AddHours(nextNode.SlaHours.Value) : null;
            approval.IsOverdue = false;
            approval.IsSlaWarned = false;

            // P17：通知下游节点的抄送人
            NotifyCcUsers(nextNode, approval);
        }
        else
        {
            approval.Status = "approved";
            approval.CurrentNodeDeadline = null;
            approval.IsOverdue = false;
            approval.IsSlaWarned = false;
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
            // 同时推送 CRM 和 MES：各系统按 OaApprovalId 查找自己关心的单据，无关则忽略（幂等）
            var payload = new { OaApprovalId = approvalId, Approved = true, Comment = comment };
            _webhook.PostToCrm("/api/approval-callback", payload);
            _webhook.PostToMes("/api/webhook/approval-callback", payload);
            _ = _notif.NotifyUserAsync(approval.ApplicantId, "approval", $"您的申请「{approval.Title}」已通过审批");
            // P9：同时推送到 UMC 统一通知中心，让申请人在所有子系统的铃铛都能看到
            _webhook.PostToUmcNotify(approval.ApplicantId, "oa", "approval_result",
                $"审批通过：{approval.Title}",
                message: comment,
                linkUrl: $"/approval/detail/{approvalId}");
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
        var rejectPayload = new { OaApprovalId = approvalId, Approved = false, Comment = comment };
        _webhook.PostToCrm("/api/approval-callback", rejectPayload);
        _webhook.PostToMes("/api/webhook/approval-callback", rejectPayload);
        _ = _notif.NotifyUserAsync(approval.ApplicantId, "approval", $"您的申请「{approval.Title}」已被驳回");
        // P9：UMC 统一通知中心
        _webhook.PostToUmcNotify(approval.ApplicantId, "oa", "approval_result",
            $"审批驳回：{approval.Title}",
            message: comment,
            linkUrl: $"/approval/detail/{approvalId}");
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

    /// <summary>判断用户是否可以审批当前节点（含转交场景）</summary>
    public bool CanApproveCurrentNode(Approval approval, long userId)
    {
        var currentNode = GetCurrentNode(approval);
        if (currentNode == null) return false;

        // P17：检查转交记录 — 若已转交给 userId，则其有权审批
        var delegated = _db.ApprovalDelegates.Any(d =>
            d.ApprovalId == approval.ApprovalId &&
            d.NodeOrder == approval.CurrentNodeOrder &&
            d.ToUserId == userId);
        if (delegated) return true;

        // 若该节点已被转交，原审批人失去权限
        var transferredAway = _db.ApprovalDelegates.Any(d =>
            d.ApprovalId == approval.ApprovalId &&
            d.NodeOrder == approval.CurrentNodeOrder &&
            d.FromUserId == userId);
        if (transferredAway) return false;

        var user = _db.SyncUsers.FirstOrDefault(u => u.UmcUserId == userId);

        return currentNode.ApproverType switch
        {
            "user" => currentNode.ApproverValue == userId.ToString(),
            "role" => user?.OaRoles.Contains(currentNode.ApproverValue ?? "") == true,
            "dept_leader" => user?.OaRoles.Any(r => r is "oa_admin" or "oa_manager") == true,
            _ => false,
        };
    }

    /// <summary>P17：转交给他人（自己卸责，新审批人接管当前节点）</summary>
    public string? Delegate(long approvalId, long fromUserId, string fromUserName, long toUserId, string toUserName, string? reason)
    {
        var approval = _db.Approvals.FirstOrDefault(a => a.ApprovalId == approvalId);
        if (approval == null) return "审批不存在";
        if (approval.Status != "pending") return "审批状态不是待审批";
        if (fromUserId == toUserId) return "不能转交给自己";
        if (!CanApproveCurrentNode(approval, fromUserId)) return "您无权审批当前节点，无法转交";

        _db.ApprovalDelegates.Add(new ApprovalDelegate
        {
            ApprovalId = approvalId,
            NodeOrder = approval.CurrentNodeOrder,
            FromUserId = fromUserId,
            FromUserName = fromUserName,
            ToUserId = toUserId,
            ToUserName = toUserName,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        // 推送通知给受转交人
        _ = _notif.NotifyUserAsync(toUserId, "approval", $"{fromUserName} 转交了审批「{approval.Title}」给您");
        return null;
    }

    /// <summary>P17：判断节点是否应被跳过（基于 SkipCondition 表达式 + FormData 求值）</summary>
    public bool ShouldSkipNode(WorkflowNode node, Approval approval)
    {
        if (string.IsNullOrWhiteSpace(node.SkipCondition)) return false;
        try
        {
            var formData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(approval.FormData)
                ?? new Dictionary<string, object?>();
            return EvaluateCondition(node.SkipCondition, formData);
        }
        catch { return false; }
    }

    /// <summary>支持的简单表达式：amount &lt; 1000、amount &gt;= 5000、status == "urgent"</summary>
    private static bool EvaluateCondition(string expr, Dictionary<string, object?> data)
    {
        // 仅支持二元比较：&lt; &lt;= &gt; &gt;= == !=
        var ops = new[] { "<=", ">=", "==", "!=", "<", ">" };
        foreach (var op in ops)
        {
            var idx = expr.IndexOf(op, StringComparison.Ordinal);
            if (idx < 0) continue;
            var left = expr.Substring(0, idx).Trim();
            var right = expr.Substring(idx + op.Length).Trim();
            data.TryGetValue(left, out var leftVal);
            // 数字比较
            if (decimal.TryParse(right, out var rNum) && leftVal != null
                && decimal.TryParse(leftVal.ToString(), out var lNum))
            {
                return op switch
                {
                    "<"  => lNum <  rNum,
                    "<=" => lNum <= rNum,
                    ">"  => lNum >  rNum,
                    ">=" => lNum >= rNum,
                    "==" => lNum == rNum,
                    "!=" => lNum != rNum,
                    _ => false,
                };
            }
            // 字符串比较
            var rStr = right.Trim('"', '\'');
            var lStr = leftVal?.ToString() ?? "";
            return op switch
            {
                "==" => lStr == rStr,
                "!=" => lStr != rStr,
                _ => false,
            };
        }
        return false;
    }

    /// <summary>获取当前审批节点</summary>
    public WorkflowNode? GetCurrentNode(Approval approval)
    {
        return _db.Nodes.FirstOrDefault(n =>
            n.TemplateId == approval.TemplateId &&
            n.NodeOrder == approval.CurrentNodeOrder &&
            n.Status == 1);
    }

    /// <summary>获取用户待审批列表（含被转交给我的）</summary>
    public List<Approval> GetPendingForUser(long userId)
    {
        return _db.Approvals
            .Where(a => a.Status == "pending")
            .ToList()
            .Where(a => CanApproveCurrentNode(a, userId))
            .ToList();
    }

    /// <summary>P17：节点流转到时通知抄送人</summary>
    private void NotifyCcUsers(WorkflowNode node, Approval approval)
    {
        if (string.IsNullOrEmpty(node.CcUsersJson)) return;
        try
        {
            var ccIds = System.Text.Json.JsonSerializer.Deserialize<long[]>(node.CcUsersJson);
            if (ccIds == null) return;
            foreach (var uid in ccIds)
            {
                _ = _notif.NotifyUserAsync(uid, "approval", $"审批「{approval.Title}」流转到「{node.NodeName}」（抄送）");
            }
        }
        catch { /* JSON 错误静默忽略 */ }
    }
}
