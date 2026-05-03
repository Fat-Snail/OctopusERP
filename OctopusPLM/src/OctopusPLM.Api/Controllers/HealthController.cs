using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OctopusPLM.Api.Controllers;

/// <summary>健康检查</summary>
[ApiController]
public class HealthController : ControllerBase
{
    /// <summary>健康检查端点</summary>
    [HttpGet("/api/health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { code = 200, msg = "ok", data = new { status = "healthy", timestamp = DateTime.UtcNow } });
    }
}
