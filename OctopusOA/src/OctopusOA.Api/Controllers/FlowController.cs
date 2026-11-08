using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Filters;
using OctopusOA.Api.Persistence;
using OctopusOA.Api.Workflow;
using System.Security.Claims;

namespace OctopusOA.Api.Controllers;

/// <summary>审批中心：图流程引擎接口（与旧 /api/approval 并存，Phase 1 新增）</summary>
[ApiController]
[Route("api/flow")]
[Authorize]
public class FlowController : ControllerBase
{
    private readonly OaDbContext _db;
    private readonly IFlowEngine _engine;

    public FlowController(OaDbContext db, IFlowEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    private (long userId, string userName) GetCurrentUser()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
        var name = User.FindFirst("name")?.Value ?? User.Identity?.Name ?? "unknown";
        return (long.Parse(sub), name);
    }

    /// <summary>可用流程定义</summary>
    [HttpGet("definitions")]
    public IActionResult Definitions()
    {
        var list = _db.FlowDefinitions.Where(d => d.Status == 1).Select(d => new FlowDefinitionResponse
        {
            Id = d.Id, Code = d.Code, Name = d.Name, Version = d.Version,
            Category = d.Category, BusinessKey = d.BusinessKey, FormSchema = d.FormSchema,
        }).ToList();
        return Ok(new { code = 200, msg = "ok", data = list });
    }

    /// <summary>流程定义详情（含流程图 JSON，供设计器读取）</summary>
    [HttpGet("definitions/{id:long}")]
    public IActionResult DefinitionDetail(long id)
    {
        var d = _db.FlowDefinitions.FirstOrDefault(x => x.Id == id);
        if (d == null) return Ok(new { code = 404, msg = "流程定义不存在" });
        return Ok(new { code = 200, msg = "ok", data = new FlowDefinitionDetailResponse
        {
            Id = d.Id, Code = d.Code, Name = d.Name, Version = d.Version,
            Category = d.Category, BusinessKey = d.BusinessKey, FormSchema = d.FormSchema,
            GraphJson = d.GraphJson, Status = d.Status,
        }});
    }

    /// <summary>保存流程定义（新建/更新，设计器使用）</summary>
    [HttpPost("definitions")]
    [OperationLog("保存流程定义")]
    public IActionResult SaveDefinition([FromBody] SaveFlowDefinitionRequest req)
    {
        FlowGraph graph;
        try { graph = FlowGraph.FromJson(req.GraphJson); }
        catch { return Ok(new { code = 400, msg = "流程图 JSON 无效" }); }
        if (graph.Nodes.All(n => n.Type != FlowNodeTypes.Start))
            return Ok(new { code = 400, msg = "流程图缺少开始节点" });

        FlowDefinition? existing = req.Id > 0
            ? _db.FlowDefinitions.FirstOrDefault(d => d.Id == req.Id)
            : null;
        if (req.Id > 0 && existing == null)
            return Ok(new { code = 404, msg = "流程定义不存在" });

        FlowDefinition def;
        if (existing != null)
        {
            def = existing;
            def.Version += 1;
        }
        else
        {
            def = new FlowDefinition { Version = 1 };
            _db.FlowDefinitions.Add(def);
        }

        def.Code = req.Code;
        def.Name = req.Name;
        def.Category = req.Category;
        def.BusinessKey = req.BusinessKey;
        def.FormSchema = req.FormSchema;
        def.GraphJson = req.GraphJson;
        def.Status = req.Status;
        _db.SaveChanges();
        return Ok(new { code = 200, msg = "保存成功", data = new { def.Id, def.Version } });
    }

    /// <summary>发起流程</summary>
    [HttpPost("submit")]
    [OperationLog("发起流程")]
    public IActionResult Submit([FromBody] SubmitFlowRequest req)
    {
        var (userId, userName) = GetCurrentUser();
        var (instance, error) = _engine.Start(new StartFlowRequest(
            req.DefinitionId, req.Title, userId, userName, req.FormData, req.BusinessKey, req.Variables, req.DefinitionCode));
        if (error != null) return Ok(new { code = 500, msg = error });
        return Ok(new { code = 200, msg = "发起成功", data = new { instanceId = instance!.Id } });
    }

    /// <summary>待我处理的任务</summary>
    [HttpGet("pending")]
    public IActionResult Pending()
    {
        var (userId, _) = GetCurrentUser();
        var tasks = _engine.GetPendingTasks(userId).OrderByDescending(t => t.CreateTime).ToList();
        var rows = tasks.Select(MapTask).ToList();
        return Ok(new { code = 200, msg = "ok", data = new { rows, total = rows.Count } });
    }

    /// <summary>我发起的流程</summary>
    [HttpGet("mine")]
    public IActionResult Mine()
    {
        var (userId, _) = GetCurrentUser();
        var rows = _db.FlowInstances.Where(i => i.ApplicantId == userId)
            .OrderByDescending(i => i.CreateTime)
            .Select(i => new
            {
                i.Id, i.DefinitionId, i.Title, i.ApplicantId, i.ApplicantName,
                i.Status, i.BusinessKey, i.CreateTime, i.UpdateTime, i.Deadline, i.IsOverdue,
            }).ToList();
        return Ok(new { code = 200, msg = "ok", data = new { rows, total = rows.Count } });
    }

    /// <summary>流程实例详情（含任务与历史）</summary>
    [HttpGet("{id:long}")]
    public IActionResult Detail(long id)
    {
        var instance = _engine.GetInstance(id);
        if (instance == null) return Ok(new { code = 404, msg = "流程实例不存在" });
        return Ok(new { code = 200, msg = "ok", data = MapInstance(instance) });
    }

    /// <summary>通过任务</summary>
    [HttpPut("task/{taskId:long}/approve")]
    [OperationLog("流程审批通过")]
    public IActionResult Approve(long taskId, [FromBody] FlowTaskRequest req)
    {
        var (userId, userName) = GetCurrentUser();
        var error = _engine.CompleteTask(taskId, userId, userName, "approve", req.Comment);
        if (error != null) return Ok(new { code = error.Contains("无权") ? 403 : 500, msg = error });
        return Ok(new { code = 200, msg = "审批通过" });
    }

    /// <summary>驳回任务</summary>
    [HttpPut("task/{taskId:long}/reject")]
    [OperationLog("流程审批驳回")]
    public IActionResult Reject(long taskId, [FromBody] FlowTaskRequest req)
    {
        var (userId, userName) = GetCurrentUser();
        var error = _engine.CompleteTask(taskId, userId, userName, "reject", req.Comment);
        if (error != null) return Ok(new { code = error.Contains("无权") ? 403 : 500, msg = error });
        return Ok(new { code = 200, msg = "已驳回" });
    }

    /// <summary>申请人撤回</summary>
    [HttpPut("{id:long}/cancel")]
    public IActionResult Cancel(long id)
    {
        var (userId, _) = GetCurrentUser();
        var error = _engine.Cancel(id, userId);
        if (error != null) return Ok(new { code = error.Contains("申请人") ? 403 : 500, msg = error });
        return Ok(new { code = 200, msg = "已撤回" });
    }

    // ── 映射 ──────────────────────────────────────────

    private FlowTaskResponse MapTask(FlowTask t)
    {
        var instance = _db.FlowInstances.FirstOrDefault(i => i.Id == t.InstanceId);
        var def = _db.FlowDefinitions.FirstOrDefault(d => d.Id == instance!.DefinitionId);
        var graph = FlowGraph.FromJson(def?.GraphJson ?? "{}");
        return new FlowTaskResponse
        {
            TaskId = t.Id,
            InstanceId = t.InstanceId,
            NodeId = t.NodeId,
            NodeName = graph.Node(t.NodeId)?.Name ?? "未知节点",
            Title = instance!.Title,
            ApplicantId = instance.ApplicantId,
            ApplicantName = instance.ApplicantName,
            BusinessKey = instance.BusinessKey,
            InstanceStatus = instance.Status,
            Deadline = t.Deadline,
            CreateTime = t.CreateTime,
        };
    }

    private FlowInstanceResponse MapInstance(FlowInstance i)
    {
        var def = _db.FlowDefinitions.FirstOrDefault(d => d.Id == i.DefinitionId);
        var graph = FlowGraph.FromJson(def?.GraphJson ?? "{}");

        var tasks = _db.FlowTasks.Where(t => t.InstanceId == i.Id).OrderBy(t => t.Id).ToList()
            .Select(t => new FlowTaskDetailResponse
            {
                TaskId = t.Id,
                NodeId = t.NodeId,
                NodeName = graph.Node(t.NodeId)?.Name ?? "未知节点",
                AssigneeId = t.AssigneeId,
                AssigneeName = t.AssigneeName,
                Status = t.Status,
                Action = t.Action,
                Comment = t.Comment,
                ActionTime = t.ActionTime,
            }).ToList();

        var history = _db.FlowHistories.Where(h => h.InstanceId == i.Id).OrderBy(h => h.Id)
            .Select(h => new FlowHistoryResponse
            {
                NodeName = h.NodeName, Action = h.Action, ActorId = h.ActorId,
                ActorName = h.ActorName, Comment = h.Comment, CreateTime = h.CreateTime,
            }).ToList();

        return new FlowInstanceResponse
        {
            InstanceId = i.Id,
            DefinitionId = i.DefinitionId,
            Title = i.Title,
            ApplicantId = i.ApplicantId,
            ApplicantName = i.ApplicantName,
            Status = i.Status,
            BusinessKey = i.BusinessKey,
            FormData = i.FormData,
            CreateTime = i.CreateTime,
            UpdateTime = i.UpdateTime,
            Deadline = i.Deadline,
            IsOverdue = i.IsOverdue,
            Tasks = tasks,
            History = history,
        };
    }
}
