using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Api.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctopusPLM.Api.Controllers;

[ApiController]
[Route("api/vector/scan")]
[AllowAnonymous]
public class VectorScanController(VectorScanService scan, IServiceProvider sp) : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var json = JsonSerializer.Serialize(scan.GetStatus(), _jsonOpts);
        return Content(json, "application/json");
    }

    [HttpPost("start")]
    public IActionResult Start()
    {
        if (!scan.TryStart(sp))
            return Ok(new { code = 409, msg = "扫描任务正在进行中" });
        return Ok(new { code = 200, msg = "扫描已启动" });
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        while (!ct.IsCancellationRequested)
        {
            var status = scan.GetStatus();
            var json = JsonSerializer.Serialize(status, _jsonOpts);
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);

            if (status.State == ScanState.Done || status.State == ScanState.Error) break;
            await Task.Delay(400, ct);
        }
    }
}
