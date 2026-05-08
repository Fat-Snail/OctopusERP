using Microsoft.AspNetCore.Mvc;
using OctopusPLM.Api.Services;
using System.Text.Json;

namespace OctopusPLM.Api.Controllers;

/// <summary>CLIP 模型管理（状态查询 / 手动触发下载 / SSE 进度）</summary>
[ApiController]
[Route("api/vector/model")]
public class VectorModelController : ControllerBase
{
    private readonly ModelDownloadService _downloader;
    private readonly VectorService _vector;

    public VectorModelController(ModelDownloadService downloader, VectorService vector)
    {
        _downloader = downloader;
        _vector = vector;
    }

    /// <summary>查询模型状态：是否已加载、是否在下载、下载进度</summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var s = _downloader.GetStatus(_vector.IsModelLoaded);
        return Ok(new
        {
            state      = s.State.ToString().ToLower(),   // idle | downloading | done | error
            progressPct = s.ProgressPct,
            downloadedMb = s.DownloadedMb,
            totalMb    = s.TotalMb,
            modelLoaded = s.ModelLoaded,
            error      = s.Error,
        });
    }

    /// <summary>触发下载。mirror=true 使用国内镜像源。</summary>
    [HttpPost("download")]
    public IActionResult StartDownload([FromQuery] bool mirror = false)
    {
        if (_vector.IsModelLoaded)
            return Ok(new { message = "模型已加载，无需重复下载" });

        var started = _downloader.TryStart(mirror, _vector);
        if (!started)
            return Conflict(new { message = "下载已在进行中" });

        return Accepted(new { message = $"开始下载（{(mirror ? "镜像源" : "官方源")}）" });
    }

    /// <summary>SSE 进度流：每 500ms 推送一次，下载完成或出错后自动关闭</summary>
    [HttpGet("download/stream")]
    public async Task StreamProgress(CancellationToken ct)
    {
        Response.Headers.ContentType  = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection   = "keep-alive";

        while (!ct.IsCancellationRequested)
        {
            var s = _downloader.GetStatus(_vector.IsModelLoaded);
            var payload = JsonSerializer.Serialize(new
            {
                state        = s.State.ToString().ToLower(),
                progressPct  = s.ProgressPct,
                downloadedMb = s.DownloadedMb,
                totalMb      = s.TotalMb,
                modelLoaded  = s.ModelLoaded,
                error        = s.Error,
            });

            await Response.WriteAsync($"data: {payload}\n\n", ct);
            await Response.Body.FlushAsync(ct);

            // 下载完成或出错，最后推一帧后关闭流
            if (s.State == DownloadState.Done || s.State == DownloadState.Error)
                break;

            await Task.Delay(500, ct);
        }
    }
}
