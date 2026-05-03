using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Api.Services;

namespace OctopusPLM.Api.Controllers;

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

    /// <summary>类目下绑定的属性（含枚举值，按 GroupName 分组）</summary>
    [HttpGet("{id:long}/attributes")]
    public async Task<IActionResult> GetAttributes(long id)
    {
        var attributes = await _service.GetAttributesAsync(id);
        return Ok(new { code = 200, msg = "ok", data = attributes });
    }

    /// <summary>类目模型版本列表</summary>
    [HttpGet("{id:long}/model/versions")]
    public async Task<IActionResult> GetModelVersions(long id)
    {
        var versions = await _service.GetModelVersionsAsync(id);
        return Ok(new { code = 200, msg = "ok", data = versions });
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
