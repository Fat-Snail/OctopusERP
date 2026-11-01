using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Api.Services;

namespace OctopusPLM.Api.Controllers;

[ApiController]
[Route("api/materials")]
[Authorize(Policy = "MaterialView")]
public sealed class MaterialsController(MaterialService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(string? keyword, string? type, string? status, bool selectableOnly = false, int page = 1, int pageSize = 20, CancellationToken ct = default)
        => Ok(new { code = 200, msg = "ok", data = await service.SearchAsync(keyword, type, status, selectableOnly, page, pageSize, ct) });

    [HttpPost, Authorize(Policy = "MaterialManage")]
    public async Task<IActionResult> Create(MaterialWriteRequest request, CancellationToken ct) => await Execute(async () => await service.CreateAsync(request, UserId(), UserName(), ct));

    [HttpPut("{id:long}"), Authorize(Policy = "MaterialManage")]
    public async Task<IActionResult> Update(long id, MaterialWriteRequest request, CancellationToken ct) => await Execute(async () => await service.UpdateAsync(id, request, UserId(), UserName(), ct));

    [HttpGet("{id:long}/references")]
    public async Task<IActionResult> References(long id, CancellationToken ct) => Ok(new { code = 200, msg = "ok", data = await service.GetReferencesAsync(id, ct) });

    [HttpDelete("{id:long}"), Authorize(Policy = "MaterialManage")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) => await Execute(async () => { await service.DeleteAsync(id, UserId(), UserName(), ct); return (object?)null; });

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(new { code = 200, msg = "操作成功", data = await action() }); }
        catch (MaterialConflictException ex) { return Ok(new { code = 409, msg = ex.Message, data = (object?)null }); }
        catch (KeyNotFoundException ex) { return Ok(new { code = 404, msg = ex.Message, data = (object?)null }); }
        catch (ArgumentException ex) { return Ok(new { code = 400, msg = ex.Message, data = (object?)null }); }
    }
    private long UserId() => long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private string UserName() => User.FindFirst("name")?.Value ?? User.Identity?.Name ?? UserId().ToString();
}
