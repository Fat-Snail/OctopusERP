using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Api.Services;

namespace OctopusPLM.Api.Controllers;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public int OrderNum { get; set; }
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public int OrderNum { get; set; }
}

public class CreateAttributeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string AttributeType { get; set; } = "text";
    public string InputType { get; set; } = "single_line";
    public string? Unit { get; set; }
    public string ValueScope { get; set; } = "global";
}

public class UpdateAttributeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
}

public class AddAttributeValueRequest
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int OrderNum { get; set; }
}

public class BindAttributeRequest
{
    public long AttributeId { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSaleAxis { get; set; }
    public string GroupType { get; set; } = "basic";
    public string? GroupName { get; set; }
    public int OrderNum { get; set; }
}

public class CreateModelVersionRequest
{
    public string? ChangeSummary { get; set; }
}

public class UpdateModelVersionRequest
{
    public string? ChangeSummary { get; set; }
}

// ── CategoryController ────────────────────────────────────────────────────────

/// <summary>商品类目</summary>
[ApiController]
[Route("api/category")]
[Authorize]
public class CategoryController : ControllerBase
{
    private readonly CategoryService _service;

    public CategoryController(CategoryService service) => _service = service;

    /// <summary>类目树（含子节点）</summary>
    [HttpGet("tree")]
    public async Task<IActionResult> GetTree()
    {
        var tree = await _service.GetTreeAsync();
        return Ok(new { code = 200, msg = "ok", data = tree });
    }

    /// <summary>创建类目</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest req)
    {
        var (cat, err) = await _service.CreateCategoryAsync(req.Name, req.ParentId, req.OrderNum);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "创建成功", data = new { cat!.CategoryId } });
    }

    /// <summary>更新类目</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCategoryRequest req)
    {
        var err = await _service.UpdateCategoryAsync(id, req.Name, req.OrderNum);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "更新成功" });
    }

    /// <summary>删除类目（无子节点且无关联商品才允许）</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var err = await _service.DeleteCategoryAsync(id);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "删除成功" });
    }

    /// <summary>类目下绑定的属性（含枚举值，按 GroupName 分组）</summary>
    [HttpGet("{id:long}/attributes")]
    public async Task<IActionResult> GetAttributes(long id)
    {
        var attributes = await _service.GetAttributesAsync(id);
        return Ok(new { code = 200, msg = "ok", data = attributes });
    }

    /// <summary>绑定属性到类目</summary>
    [HttpPost("{id:long}/attributes")]
    public async Task<IActionResult> BindAttribute(long id, [FromBody] BindAttributeRequest req)
    {
        var (binding, err) = await _service.BindAttributeAsync(id, req.AttributeId, req.IsRequired, req.IsSaleAxis, req.GroupType, req.GroupName, req.OrderNum);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "绑定成功", data = new { binding!.Id } });
    }

    /// <summary>解绑类目属性</summary>
    [HttpDelete("{id:long}/attributes/{bindId:long}")]
    public async Task<IActionResult> UnbindAttribute(long id, long bindId)
    {
        var err = await _service.UnbindAttributeAsync(id, bindId);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "解绑成功" });
    }

    /// <summary>类目模型版本列表</summary>
    [HttpGet("{id:long}/model/versions")]
    public async Task<IActionResult> GetModelVersions(long id)
    {
        var versions = await _service.GetModelVersionsAsync(id);
        return Ok(new { code = 200, msg = "ok", data = versions });
    }

    /// <summary>获取指定版本的属性（按 GroupName 分组）</summary>
    [HttpGet("{id:long}/model/versions/{versionId:long}/attributes")]
    public async Task<IActionResult> GetVersionAttributes(long id, long versionId)
    {
        var attrs = await _service.GetVersionAttributesAsync(id, versionId);
        return Ok(new { code = 200, msg = "ok", data = attrs });
    }

    /// <summary>新建草稿版本（克隆当前 active 版属性）</summary>
    [HttpPost("{id:long}/model/versions")]
    public async Task<IActionResult> CreateModelVersion(long id, [FromBody] CreateModelVersionRequest req)
    {
        var (ver, err) = await _service.CreateModelVersionAsync(id, req.ChangeSummary);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "创建成功", data = new { ver!.ModelVersionId, ver.VersionNo } });
    }

    /// <summary>更新草稿版本变更说明</summary>
    [HttpPut("{id:long}/model/versions/{versionId:long}")]
    public async Task<IActionResult> UpdateModelVersion(long id, long versionId, [FromBody] UpdateModelVersionRequest req)
    {
        var err = await _service.UpdateModelVersionAsync(id, versionId, req.ChangeSummary);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "更新成功" });
    }

    /// <summary>发布草稿版本（将其设为 active，归档旧 active）</summary>
    [HttpPost("{id:long}/model/versions/{versionId:long}/publish")]
    public async Task<IActionResult> PublishModelVersion(long id, long versionId)
    {
        var err = await _service.PublishModelVersionAsync(id, versionId);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "发布成功" });
    }

    /// <summary>删除草稿版本</summary>
    [HttpDelete("{id:long}/model/versions/{versionId:long}")]
    public async Task<IActionResult> DeleteModelVersion(long id, long versionId)
    {
        var err = await _service.DeleteModelVersionAsync(id, versionId);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "删除成功" });
    }

    /// <summary>向草稿版本绑定属性</summary>
    [HttpPost("{id:long}/model/versions/{versionId:long}/attributes")]
    public async Task<IActionResult> BindAttributeToVersion(long id, long versionId, [FromBody] BindAttributeRequest req)
    {
        var (binding, err) = await _service.BindAttributeToVersionAsync(id, versionId, req.AttributeId, req.IsRequired, req.IsSaleAxis, req.GroupType, req.GroupName, req.OrderNum);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "绑定成功", data = new { binding!.Id } });
    }

    /// <summary>从草稿版本解绑属性</summary>
    [HttpDelete("{id:long}/model/versions/{versionId:long}/attributes/{bindId:long}")]
    public async Task<IActionResult> UnbindAttributeFromVersion(long id, long versionId, long bindId)
    {
        var err = await _service.UnbindAttributeFromVersionAsync(id, versionId, bindId);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "解绑成功" });
    }

    /// <summary>类目当前生效模型详情</summary>
    [HttpGet("{id:long}/model")]
    public async Task<IActionResult> GetModel(long id)
    {
        var model = await _service.GetModelAsync(id);
        if (model == null) return Ok(new { code = 404, msg = "类目不存在" });
        return Ok(new { code = 200, msg = "ok", data = model });
    }
}

// ── AttributeController ───────────────────────────────────────────────────────

/// <summary>属性库管理</summary>
[ApiController]
[Route("api/attribute")]
[Authorize]
public class AttributeController : ControllerBase
{
    private readonly CategoryService _service;

    public AttributeController(CategoryService service) => _service = service;

    /// <summary>所有属性（含枚举值）</summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList()
    {
        var attrs = await _service.GetAllAttributesAsync();
        return Ok(new { code = 200, msg = "ok", data = attrs });
    }

    /// <summary>创建属性</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAttributeRequest req)
    {
        var (attr, err) = await _service.CreateAttributeAsync(
            req.Name, req.Code, req.AttributeType, req.InputType, req.Unit, req.ValueScope);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "创建成功", data = new { attr!.AttributeId } });
    }

    /// <summary>更新属性</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateAttributeRequest req)
    {
        var err = await _service.UpdateAttributeAsync(id, req.Name, req.Unit);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "更新成功" });
    }

    /// <summary>删除属性（未被类目绑定才允许）</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var err = await _service.DeleteAttributeAsync(id);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "删除成功" });
    }

    /// <summary>添加枚举值</summary>
    [HttpPost("{id:long}/values")]
    public async Task<IActionResult> AddValue(long id, [FromBody] AddAttributeValueRequest req)
    {
        var (val, err) = await _service.AddAttributeValueAsync(id, req.Label, req.Value, req.OrderNum);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "添加成功", data = new { val!.ValueId } });
    }

    /// <summary>删除枚举值</summary>
    [HttpDelete("values/{valueId:long}")]
    public async Task<IActionResult> DeleteValue(long valueId)
    {
        var err = await _service.DeleteAttributeValueAsync(valueId);
        if (err != null) return Ok(new { code = 400, msg = err });
        return Ok(new { code = 200, msg = "删除成功" });
    }
}
