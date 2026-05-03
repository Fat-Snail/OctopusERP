using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;
using OctopusOA.Api.Services;
using System.Security.Claims;

namespace OctopusOA.Api.Controllers;

/// <summary>审批操作接口</summary>
[ApiController]
[Route("api/approval")]
[Authorize]
public class ApprovalController : ControllerBase
{
    private readonly OaDbContext _db;
    private readonly ApprovalService _service;

    public ApprovalController(OaDbContext db, ApprovalService service)
    {
        _db = db;
        _service = service;
    }

    private (long userId, string userName) GetCurrentUser()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
        var name = User.FindFirst("name")?.Value ?? User.Identity?.Name ?? "unknown";
        return (long.Parse(sub), name);
    }

    /// <summary>可用模板列表（发起时选择）</summary>
    [HttpGet("templates")]
    public IActionResult GetAvailableTemplates()
    {
        var list = _db.Templates.Where(t => t.Status == 1).Select(t => new
        {
            t.TemplateId, t.TemplateName, t.TemplateCode, t.Description, t.Icon, t.FormSchema
        }).ToList();
        return Ok(new { code = 200, msg = "ok", data = list });
    }

    /// <summary>提交审批申请</summary>
    [HttpPost("submit")]
    public IActionResult Submit([FromBody] SubmitApprovalRequest req)
    {
        var (userId, userName) = GetCurrentUser();
        var (approval, error) = _service.Submit(userId, userName, req.TemplateId, req.Title, req.FormData);
        if (error != null) return Ok(new { code = 500, msg = error });
        return Ok(new { code = 200, msg = "提交成功", data = MapApproval(approval!) });
    }

    /// <summary>我的申请列表</summary>
    [HttpGet("mine")]
    public IActionResult Mine()
    {
        var (userId, _) = GetCurrentUser();
        var list = _db.Approvals
            .Where(a => a.ApplicantId == userId)
            .OrderByDescending(a => a.CreateTime)
            .ToList()
            .Select(MapApproval)
            .ToList();
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>待我审批列表</summary>
    [HttpGet("pending")]
    public IActionResult Pending()
    {
        var (userId, _) = GetCurrentUser();
        var list = _service.GetPendingForUser(userId)
            .OrderByDescending(a => a.CreateTime)
            .Select(MapApproval)
            .ToList();
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>审批详情（含流程记录）</summary>
    [HttpGet("{id:long}")]
    public IActionResult GetById(long id)
    {
        var a = _db.Approvals.FirstOrDefault(a => a.ApprovalId == id);
        if (a == null) return Ok(new { code = 404, msg = "审批不存在" });
        return Ok(new { code = 200, msg = "ok", data = MapApproval(a) });
    }

    /// <summary>审批通过</summary>
    [HttpPut("{id:long}/approve")]
    public IActionResult Approve(long id, [FromBody] ApproveRequest req)
    {
        var (userId, userName) = GetCurrentUser();
        var error = _service.Approve(id, userId, userName, req.Comment);
        if (error != null)
        {
            var code = error.Contains("无权") ? 403 : 500;
            return Ok(new { code, msg = error });
        }
        return Ok(new { code = 200, msg = "审批通过" });
    }

    /// <summary>驳回</summary>
    [HttpPut("{id:long}/reject")]
    public IActionResult Reject(long id, [FromBody] RejectRequest req)
    {
        var (userId, userName) = GetCurrentUser();
        var error = _service.Reject(id, userId, userName, req.Comment);
        if (error != null)
        {
            var code = error.Contains("无权") ? 403 : 500;
            return Ok(new { code, msg = error });
        }
        return Ok(new { code = 200, msg = "已驳回" });
    }

    /// <summary>撤回（仅申请人）</summary>
    [HttpPut("{id:long}/cancel")]
    public IActionResult Cancel(long id)
    {
        var (userId, _) = GetCurrentUser();
        var error = _service.Cancel(id, userId);
        if (error != null)
        {
            var code = error.Contains("申请人") ? 403 : 500;
            return Ok(new { code, msg = error });
        }
        return Ok(new { code = 200, msg = "已撤回" });
    }

    /// <summary>全部审批（oa_admin）</summary>
    [HttpGet("all")]
    public IActionResult All()
    {
        var list = _db.Approvals
            .OrderByDescending(a => a.CreateTime)
            .ToList()
            .Select(MapApproval)
            .ToList();
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    private ApprovalResponse MapApproval(Approval a)
    {
        var template = _db.Templates.FirstOrDefault(t => t.TemplateId == a.TemplateId);
        var nodes = _db.Nodes.Where(n => n.TemplateId == a.TemplateId).OrderBy(n => n.NodeOrder).ToList();
        var currentNode = nodes.FirstOrDefault(n => n.NodeOrder == a.CurrentNodeOrder);
        var rawRecords = _db.ApprovalRecords
            .Where(r => r.ApprovalId == a.ApprovalId)
            .OrderBy(r => r.NodeOrder)
            .ToList();
        var records = rawRecords.Select(r =>
        {
            var node = _db.Nodes.FirstOrDefault(n => n.NodeId == r.NodeId);
            return new RecordResponse
            {
                RecordId = r.RecordId,
                NodeOrder = r.NodeOrder,
                NodeName = node?.NodeName ?? "未知节点",
                ApproverId = r.ApproverId,
                ApproverName = r.ApproverName,
                Action = r.Action,
                Comment = r.Comment,
                ActionTime = r.ActionTime,
            };
        }).ToList();

        return new ApprovalResponse
        {
            ApprovalId = a.ApprovalId,
            TemplateId = a.TemplateId,
            TemplateName = template?.TemplateName ?? "未知模板",
            Title = a.Title,
            ApplicantId = a.ApplicantId,
            ApplicantName = a.ApplicantName,
            CurrentNodeOrder = a.CurrentNodeOrder,
            CurrentNodeName = currentNode?.NodeName,
            TotalNodes = nodes.Count,
            Status = a.Status,
            FormData = a.FormData,
            CreateTime = a.CreateTime,
            UpdateTime = a.UpdateTime,
            Records = records,
        };
    }
}
