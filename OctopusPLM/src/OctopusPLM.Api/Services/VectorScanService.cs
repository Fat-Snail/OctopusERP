using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Services;

public enum ScanState { Idle, Scanning, Done, Error }

public record ScanStatus(
    ScanState State,
    int Total,
    int Processed,
    int Success,
    int Fail,
    string? Error);

public class VectorScanService
{
    private volatile ScanState _state = ScanState.Idle;
    private int _total;
    private int _processed;
    private int _success;
    private int _fail;
    private string? _error;

    public ScanStatus GetStatus() =>
        new(_state, _total, _processed, _success, _fail, _error);

    public bool TryStart(IServiceProvider sp)
    {
        if (_state == ScanState.Scanning) return false;
        _state = ScanState.Scanning;
        _total = _processed = _success = _fail = 0;
        _error = null;
        
        // 修复：创建scope而不是直接使用sp
        _ = Task.Run(async () =>
        {
            await using var scope = sp.CreateAsyncScope();
            await RunAsync(scope.ServiceProvider);
        });
        
        return true;
    }

    private async Task RunAsync(IServiceProvider sp)
    {
        try
        {
            var db = sp.GetRequiredService<PlmDbContext>();
            var vector = sp.GetRequiredService<VectorService>();

            var products = db.Products
                .Where(p => !string.IsNullOrEmpty(p.MainImage))
                .Select(p => new { p.ProductId, p.MainImage, p.ProductName })
                .ToList();

            _total = products.Count;

            foreach (var p in products)
            {
                var (ok, _) = await vector.IndexProductAsync(p.ProductId, p.MainImage!, p.ProductName);
                if (ok) Interlocked.Increment(ref _success);
                else Interlocked.Increment(ref _fail);
                Interlocked.Increment(ref _processed);
            }

            _state = ScanState.Done;
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            _state = ScanState.Error;
        }
    }
}
