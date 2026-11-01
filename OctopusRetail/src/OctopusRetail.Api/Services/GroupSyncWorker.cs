namespace OctopusRetail.Api.Services;

/// <summary>
/// 周期性推进 Keycloak group 对账。
///
/// 之所以是「对账」而不是「同步」：成员关系写入业务库后即生效（用户马上能用），
/// Keycloak group 只是下游投影，晚到几十秒不影响正确性，失败下轮重试即可。
/// 这样邀请流程的可用性不依赖 Keycloak 的实时可用性。
/// </summary>
public class GroupSyncWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<GroupSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = int.TryParse(config["Retail:GroupSyncIntervalSeconds"], out var s) ? s : 60;
        var interval = TimeSpan.FromSeconds(Math.Max(5, seconds));

        logger.LogInformation("Group 对账后台任务启动，间隔 {Interval}s", interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<GroupSyncReconciler>();
                await reconciler.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 对账失败不得拖垮进程
                logger.LogError(ex, "Group 对账轮次异常");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
