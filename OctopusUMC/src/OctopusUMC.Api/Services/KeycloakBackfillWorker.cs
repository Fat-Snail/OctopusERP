using Microsoft.EntityFrameworkCore;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Services;

/// <summary>
/// Keycloak 存量用户回填。
///
/// 背景：<see cref="KeycloakSyncService"/> 只在 UMC 用户被增删改时触发同步，
/// 所以「打开开关」并不会把**已经存在**的用户补到 Keycloak 里。
/// 实测：开关打开后 Keycloak 里仍只有 realm JSON 种进去的 admin/zhangsan，
/// lisi/wangwu/editor 等 10 个平台员工无法登录任何 OIDC 应用。
///
/// 本 worker 在启动后做一次全量 upsert（幂等，失败只记日志），
/// 让"打开同步开关"这一动作真正生效。
/// </summary>
public class KeycloakBackfillWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<KeycloakBackfillWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // 让 AppHost/DB 先就绪，避免与启动迁移竞争
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            using var scope = scopeFactory.CreateScope();
            var keycloak = scope.ServiceProvider.GetRequiredService<KeycloakSyncService>();

            if (!keycloak.Enabled)
            {
                logger.LogInformation("Keycloak 同步未启用（Keycloak:SyncEnabled=false），跳过存量回填");
                return;
            }

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = await db.Users.AsNoTracking().ToListAsync(stoppingToken);
            if (users.Count == 0) return;

            logger.LogInformation("开始回填 {Count} 个存量用户到 Keycloak", users.Count);

            var ok = 0;
            foreach (var user in users)
            {
                if (stoppingToken.IsCancellationRequested) break;
                await keycloak.SyncUserAsync(user, stoppingToken);
                ok++;
            }

            logger.LogInformation("Keycloak 存量回填完成：已处理 {Count} 个用户", ok);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 正常停机
        }
        catch (Exception ex)
        {
            // 回填失败不能拖垮 UMC 启动
            logger.LogError(ex, "Keycloak 存量回填失败");
        }
    }
}
