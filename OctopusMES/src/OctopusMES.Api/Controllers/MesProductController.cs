using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/mes-products")]
public sealed class MesProductController(PlmProductSyncService sync) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog([FromQuery] string? keyword, CancellationToken ct)
    {
        var result = await sync.GetCatalogAsync(keyword, ct);
        return Ok(new { code = result.Available ? 200 : 503, msg = result.Message, data = new { result.SourceSystem, result.Available, products = result.Products } });
    }

    [HttpGet("catalog/search")]
    public async Task<IActionResult> Search([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await sync.SearchCatalogAsync(keyword, page, pageSize, ct);
        return Ok(new { code = result.Available ? 200 : 503, msg = result.Message, data = result });
    }

    [HttpPost("{id:long}/sync")]
    [Authorize(Policy = "PlmSyncForce")]
    public async Task<IActionResult> Sync(long id, [FromQuery] long? manufacturingVersionId, CancellationToken ct)
    {
        try
        {
            if (manufacturingVersionId.HasValue) return Ok(new { code = 200, msg = "PLM 制造版本已同步到 MES", data = await sync.SyncManufacturingVersionAsync(id, manufacturingVersionId.Value, ct) });
            return Ok(new { code = 200, msg = "PLM 产品已同步到 MES", data = await sync.SyncAsync(id, ct) });
        }
        catch (InvalidOperationException ex) { return Ok(new { code = 400, msg = ex.Message, data = (object?)null }); }
    }

    [HttpGet("sync-records")]
    public async Task<IActionResult> SyncRecords([FromQuery] string? status, [FromQuery] string? keyword,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(new { code = 200, msg = "ok", data = await sync.ListSyncRecordsAsync(status, keyword, page, pageSize, ct) });

    [HttpGet("sync-records/{id:long}")]
    public async Task<IActionResult> SyncRecordDetail(long id, CancellationToken ct)
    {
        try { return Ok(new { code = 200, msg = "ok", data = await sync.GetSyncRecordDetailAsync(id, ct) }); }
        catch (InvalidOperationException ex) { return Ok(new { code = 404, msg = ex.Message, data = (object?)null }); }
    }

    [HttpGet("reconcile")]
    public async Task<IActionResult> Reconcile(CancellationToken ct)
    {
        var result = await sync.ReconcileAsync(false, ct);
        return Ok(new { code = result.Available ? 200 : 503, msg = result.Message, data = result });
    }

    [HttpPost("initialize")]
    [Authorize(Policy = "PlmSyncRetry")]
    public async Task<IActionResult> Initialize(CancellationToken ct)
    {
        var result = await sync.ReconcileAsync(true, ct);
        return Ok(new { code = result.Available ? 200 : 503, msg = result.Message, data = result });
    }

    [HttpPost("sync-records/{plmProductId:long}/retry")]
    [Authorize(Policy = "PlmSyncRetry")]
    public async Task<IActionResult> Retry(long plmProductId, CancellationToken ct)
    {
        try { return Ok(new { code = 200, msg = "重试同步成功", data = await sync.RetryRecordAsync(plmProductId, ct) }); }
        catch (InvalidOperationException ex) { return Ok(new { code = 400, msg = ex.Message, data = (object?)null }); }
    }
}
