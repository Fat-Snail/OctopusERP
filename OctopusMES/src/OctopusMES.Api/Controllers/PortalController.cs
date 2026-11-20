using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services;

namespace OctopusMES.Api.Controllers;

/// <summary>
/// P16 供应商门户专属端点。经网关 /gw/mes/api/portal/** 转发。
/// 所有端点以"仅本人"为原则：通过 PortalIdentityService 解析 sub→供应商。
/// </summary>
[ApiController]
[Route("api/portal")]
[Authorize]
public class PortalController(
    MesDbContext db,
    PortalIdentityService portalIdentity) : ControllerBase
{
    private long CurrentUserId =>
        long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 1;

    /// <summary>当前门户身份：绑定供应商 id/名称；未绑定返回 bound=false。</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var supplierId = await portalIdentity.GetSupplierIdAsync(CurrentUserId);
        if (!supplierId.HasValue)
            return Ok(new { code = 200, msg = "ok", data = new { bound = false, supplierId = (long?)null, supplierName = (string?)null } });

        var name = await db.Suppliers.Where(s => s.SupplierId == supplierId.Value)
            .Select(s => s.SupplierName).FirstOrDefaultAsync();
        return Ok(new { code = 200, msg = "ok", data = new { bound = true, supplierId, supplierName = name } });
    }
}
