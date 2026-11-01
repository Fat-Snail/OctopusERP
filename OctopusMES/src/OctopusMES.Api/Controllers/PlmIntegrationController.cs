using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;
using OctopusMES.Api.Services.PlmSync;

namespace OctopusMES.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/integration/plm")]
public sealed class PlmIntegrationController(MesDbContext db, PlmProductSyncService sync, IConfiguration config) : ControllerBase
{
    [HttpPost("manufacturing-released")]
    public async Task<IActionResult> Released([FromBody] PlmReleaseEvent req, CancellationToken ct)
    {
        var auth = PlmEventAuthenticator.Authenticate(req, config["Plm:SharedSecret"] ?? string.Empty,
            Request.Headers["X-PLM-Secret"].ToString(), Request.Headers["X-PLM-Signature"].ToString(), DateTime.UtcNow);
        if (!auth.Success)
            return auth.Unauthorized ? Unauthorized(new { code = 401, msg = auth.Message }) : BadRequest(new { code = 400, msg = auth.Message });
        var existing = await db.PlmSyncEvents.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == req.EventId, ct);
        if (existing is not null) return Ok(new { code = 200, msg = "重复事件已忽略", data = existing });
        var evt = new MesPlmSyncEvent { EventId = req.EventId, EventType = req.EventType, PlmProductId = req.ProductId, PlmManufacturingVersionId = req.ManufacturingVersionId, ReceivedAt = DateTime.UtcNow };
        db.PlmSyncEvents.Add(evt); await db.SaveChangesAsync(ct);
        try { await sync.SyncManufacturingVersionAsync(req.ProductId, req.ManufacturingVersionId, ct); evt.Status = "processed"; evt.ProcessedAt = DateTime.UtcNow; }
        catch (Exception ex) { evt.Status = "failed"; evt.ErrorMessage = ex.Message; }
        await db.SaveChangesAsync(ct);
        return Ok(new { code = evt.Status == "processed" ? 200 : 500, msg = evt.Status == "processed" ? "制造版本同步成功" : evt.ErrorMessage, data = evt });
    }
}
