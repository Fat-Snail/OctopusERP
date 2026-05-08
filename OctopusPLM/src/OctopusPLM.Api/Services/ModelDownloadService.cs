namespace OctopusPLM.Api.Services;

public enum DownloadState { Idle, Downloading, Done, Error }

public record DownloadStatus(
    DownloadState State,
    double ProgressPct,
    double DownloadedMb,
    double TotalMb,
    bool ModelLoaded,
    string? Error);

/// <summary>管理 CLIP 模型的手动下载进度（Singleton）</summary>
public class ModelDownloadService
{
    private const string OriginUrl = "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model.onnx";
    private const string MirrorUrl = "https://hf-mirror.com/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model.onnx";

    private volatile DownloadState _state = DownloadState.Idle;
    private long _downloaded;
    private long _total = -1;
    private string? _error;

    private readonly IHttpClientFactory _http;
    private readonly ILogger<ModelDownloadService> _log;

    public ModelDownloadService(IHttpClientFactory http, ILogger<ModelDownloadService> log)
    {
        _http = http;
        _log = log;
    }

    public DownloadStatus GetStatus(bool modelLoaded) => new(
        State: _state,
        ProgressPct: _total > 0 ? Math.Round(_downloaded * 100.0 / _total, 1) : 0,
        DownloadedMb: Math.Round(_downloaded / 1048576.0, 1),
        TotalMb: Math.Round(_total > 0 ? _total / 1048576.0 : 0, 1),
        ModelLoaded: modelLoaded,
        Error: _error);

    /// <summary>触发下载。已在下载中返回 false。</summary>
    public bool TryStart(bool useMirror, VectorService vector)
    {
        if (_state == DownloadState.Downloading) return false;

        _state = DownloadState.Downloading;
        _downloaded = 0;
        _total = -1;
        _error = null;

        _ = Task.Run(() => RunAsync(useMirror, vector));
        return true;
    }

    private async Task RunAsync(bool useMirror, VectorService vector)
    {
        var url = useMirror ? MirrorUrl : OriginUrl;
        var dest = VectorService.ModelPath;
        var tmp = dest + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            using var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(15);

            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            _total = resp.Content.Headers.ContentLength ?? -1;

            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(tmp);

            var buf = new byte[65536];
            int read;
            while ((read = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read));
                Interlocked.Add(ref _downloaded, read);
            }

            await dst.FlushAsync();
            dst.Close();
            File.Move(tmp, dest, overwrite: true);

            _log.LogInformation("CLIP 模型下载完成（{MB:F1} MB），正在加载...", _downloaded / 1048576.0);
            vector.LoadModel();
            _state = DownloadState.Done;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "CLIP 模型下载失败");
            if (File.Exists(tmp)) File.Delete(tmp);
            _error = ex.Message;
            _state = DownloadState.Error;
        }
    }
}
