using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Controllers;

/// <summary>审批流程模板管理</summary>
[ApiController]
[Route("api/approval/template")]
[Authorize]
public class TemplateController : ControllerBase
{
    private readonly OaDbContext _db;
    public TemplateController(OaDbContext db) => _db = db;

    /// <summary>模板列表</summary>
    [HttpGet("list")]
    public IActionResult GetList()
    {
        var list = _db.Templates.ToList().Select(MapTemplate).ToList();
        return Ok(new { code = 200, msg = "ok", data = list });
    }

    /// <summary>模板详情（含节点）</summary>
    [HttpGet("{id:long}")]
    public IActionResult GetById(long id)
    {
        var t = _db.Templates.FirstOrDefault(t => t.TemplateId == id);
        if (t == null) return Ok(new { code = 404, msg = "模板不存在" });
        return Ok(new { code = 200, msg = "ok", data = MapTemplate(t) });
    }

    /// <summary>创建模板</summary>
    [HttpPost]
    public IActionResult Create([FromBody] CreateTemplateRequest req)
    {
        var code = string.IsNullOrWhiteSpace(req.TemplateCode)
            ? $"WF-{DateTime.UtcNow:yyyyMMdd}-{_db.Templates.Count() + 1:D3}"
            : req.TemplateCode;

        if (_db.Templates.Any(t => t.TemplateCode == code))
            return Ok(new { code = 500, msg = "模板编码已存在" });

        var t = new WorkflowTemplate
        {
            TemplateName = req.TemplateName,
            TemplateCode = code,
            Description = req.Description,
            Icon = req.Icon,
            FormSchema = req.FormSchema,
            Status = req.Status,
            CreateTime = DateTime.UtcNow,
        };
        _db.Templates.Add(t);
        _db.SaveChanges();
        return Ok(new { code = 200, msg = "创建成功", data = MapTemplate(t) });
    }

    /// <summary>修改模板</summary>
    [HttpPut]
    public IActionResult Update([FromBody] UpdateTemplateRequest req)
    {
        var t = _db.Templates.FirstOrDefault(t => t.TemplateId == req.TemplateId);
        if (t == null) return Ok(new { code = 404, msg = "模板不存在" });

        t.TemplateName = req.TemplateName;
        t.TemplateCode = req.TemplateCode;
        t.Description = req.Description;
        t.Icon = req.Icon;
        t.FormSchema = req.FormSchema;
        t.Status = req.Status;
        _db.SaveChanges();
        return Ok(new { code = 200, msg = "修改成功", data = MapTemplate(t) });
    }

    /// <summary>删除模板</summary>
    [HttpDelete("{id:long}")]
    public IActionResult Delete(long id)
    {
        var t = _db.Templates.FirstOrDefault(t => t.TemplateId == id);
        if (t == null) return Ok(new { code = 404, msg = "模板不存在" });

        if (_db.Approvals.Any(a => a.TemplateId == id))
            return Ok(new { code = 500, msg = "模板已被使用，不可删除" });

        _db.Templates.Remove(t);
        var nodes = _db.Nodes.Where(n => n.TemplateId == id).ToList();
        _db.Nodes.RemoveRange(nodes);
        _db.SaveChanges();
        return Ok(new { code = 200, msg = "删除成功" });
    }

    /// <summary>设置模板节点（整体替换）</summary>
    [HttpPost("{id:long}/nodes")]
    public IActionResult SetNodes(long id, [FromBody] SetNodesRequest req)
    {
        var t = _db.Templates.FirstOrDefault(t => t.TemplateId == id);
        if (t == null) return Ok(new { code = 404, msg = "模板不存在" });

        var old = _db.Nodes.Where(n => n.TemplateId == id).ToList();
        _db.Nodes.RemoveRange(old);
        foreach (var n in req.Nodes)
        {
            _db.Nodes.Add(new WorkflowNode
            {
                TemplateId = id,
                NodeName = n.NodeName,
                NodeOrder = n.NodeOrder,
                ApproverType = n.ApproverType,
                ApproverValue = n.ApproverValue,
                Status = 1,
            });
        }
        _db.SaveChanges();
        return Ok(new { code = 200, msg = "节点设置成功", data = MapTemplate(t) });
    }

    private TemplateResponse MapTemplate(WorkflowTemplate t) => new()
    {
        TemplateId = t.TemplateId,
        TemplateName = t.TemplateName,
        TemplateCode = t.TemplateCode,
        Description = t.Description,
        Icon = t.Icon,
        FormSchema = t.FormSchema,
        Status = t.Status,
        CreateTime = t.CreateTime,
        Nodes = _db.Nodes.Where(n => n.TemplateId == t.TemplateId)
            .OrderBy(n => n.NodeOrder)
            .ToList()
            .Select(n => new NodeResponse
            {
                NodeId = n.NodeId,
                NodeName = n.NodeName,
                NodeOrder = n.NodeOrder,
                ApproverType = n.ApproverType,
                ApproverValue = n.ApproverValue,
            }).ToList(),
    };
}
