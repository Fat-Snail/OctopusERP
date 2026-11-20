using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;
using OctopusCRM.Api.Services;

namespace OctopusCRM.Api.Controllers;

/// <summary>
/// P16 客户门户专属端点。经网关 /gw/crm/api/portal/** 转发。
/// 所有端点以"仅本人"为原则：通过 PortalIdentityService 解析 sub→客户，未绑定身份一律返回空/拒绝。
/// </summary>
[ApiController]
[Route("api/portal")]
[Authorize]
public class PortalController(
    CrmDbContext db,
    PortalIdentityService portalIdentity) : ControllerBase
{
    private long CurrentUserId =>
        long.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    /// <summary>当前门户身份：绑定客户 id/名称；未绑定返回 bound=false。</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var customerId = await portalIdentity.GetCustomerIdAsync(CurrentUserId);
        if (!customerId.HasValue)
            return Ok(new { code = 200, msg = "ok", data = new { bound = false, customerId = (long?)null, customerName = (string?)null } });

        var name = await db.Customers.Where(c => c.CustomerId == customerId.Value)
            .Select(c => c.CustomerName).FirstOrDefaultAsync();
        return Ok(new { code = 200, msg = "ok", data = new { bound = true, customerId, customerName = name } });
    }
}
