using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Api.Services;

namespace OctopusPLM.Api.Controllers;

[ApiController, Route("api/bom-templates"), Authorize(Policy = "BomTemplateView")]
public sealed class BomTemplatesController(CategoryBomTemplateService service) : ControllerBase
{
    public sealed record NewVersionRequest(string Version);
    public sealed record TemplateItemRequest(int LineNo, long MaterialId, decimal DefaultQuantity, decimal DefaultLossRate);
    public sealed record UpdateTemplateRequest(string TemplateName, List<TemplateItemRequest> Items);
    [HttpGet]
    public async Task<IActionResult> List(long categoryId, CancellationToken ct) => Ok(new { code = 200, msg = "ok", data = await service.ListAsync(categoryId, ct) });
    [HttpPost("initialize-leaf-categories"), Authorize(Policy = "BomTemplateManage")]
    public async Task<IActionResult> Initialize(CancellationToken ct) => Ok(new { code = 200, msg = "初始化完成", data = await service.InitializeLeafCategoriesAsync(UserId(), UserName(), ct) });
    [HttpPost("{id:long}/new-version"), Authorize(Policy = "BomTemplateManage")]
    public async Task<IActionResult> NewVersion(long id, NewVersionRequest request, CancellationToken ct) => Ok(new { code = 200, msg = "模板草稿已创建", data = await service.CreateNewVersionAsync(id, request.Version, UserId(), UserName(), ct) });
    [HttpPut("{id:long}"), Authorize(Policy = "BomTemplateManage")]
    public async Task<IActionResult> Update(long id, UpdateTemplateRequest request, CancellationToken ct) => Ok(new { code = 200, msg = "模板草稿已保存", data = await service.UpdateDraftAsync(id, request.TemplateName, request.Items.Select(x => new TemplateItemWrite(x.LineNo, x.MaterialId, x.DefaultQuantity, x.DefaultLossRate)).ToList(), UserId(), UserName(), ct) });
    [HttpPost("{id:long}/publish"), Authorize(Policy = "BomTemplateManage")]
    public async Task<IActionResult> Publish(long id, CancellationToken ct) { await service.PublishAsync(id, UserId(), UserName(), ct); return Ok(new { code = 200, msg = "模板已发布为当前版本" }); }
    private long UserId() => long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private string UserName() => User.FindFirst("name")?.Value ?? User.Identity?.Name ?? UserId().ToString();
}
