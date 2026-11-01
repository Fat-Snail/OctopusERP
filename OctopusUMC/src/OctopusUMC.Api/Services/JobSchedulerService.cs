using Cronos;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Services;

/// <summary>定时任务调度器（BackgroundService + Cronos）</summary>
public class JobSchedulerService(IServiceProvider serviceProvider, ILogger<JobSchedulerService> logger)
    : BackgroundService
{
    // 记录每个 Job 最近一次触发时间（避免同一分钟重复执行）
    private readonly Dictionary<long, DateTime> _lastRun = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("JobScheduler 启动");

        // 每 30 秒检查一次，精度足够分钟级 cron
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckAndRunJobsAsync(stoppingToken);
        }
    }

    private async Task CheckAndRunJobsAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var jobs = db.Jobs.Where(j => j.Status == 1).ToList();
        var now = DateTime.UtcNow;

        foreach (var job in jobs)
        {
            if (!ShouldRun(job, now)) continue;
            _lastRun[job.JobId] = now;
            // 单个任务失败绝不能拖垮整个调度循环
            try
            {
                await ExecuteJobAsync(db, job, now, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job [{Name}] 调度异常，已跳过", job.JobName);
            }
        }
    }

    private bool ShouldRun(Job job, DateTime now)
    {
        try
        {
            var cron = CronExpression.Parse(job.CronExpression, CronFormat.IncludeSeconds);
            var window = now.AddSeconds(-31);
            var next = cron.GetNextOccurrence(window, TimeZoneInfo.Utc);
            if (next == null || next > now) return false;

            // 同一次触发点不重复执行
            if (_lastRun.TryGetValue(job.JobId, out var last) && (now - last).TotalSeconds < 55)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Job {Name} cron 解析失败: {Msg}", job.JobName, ex.Message);
            return false;
        }
    }

    internal async Task ExecuteJobAsync(ApplicationDbContext db, Job job, DateTime startTime, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var log = new JobLog
        {
            JobId = job.JobId,
            JobName = job.JobName,
            JobGroup = job.JobGroup,
            InvokeTarget = job.InvokeTarget,
            StartTime = startTime,
        };

        try
        {
            await RunTargetAsync(job.InvokeTarget, db, ct);
            sw.Stop();
            log.ElapsedMs = sw.ElapsedMilliseconds;
            log.Status = 0;
            logger.LogInformation("Job [{Name}] 执行成功，耗时 {Ms}ms", job.JobName, log.ElapsedMs);
        }
        catch (Exception ex)
        {
            sw.Stop();
            log.ElapsedMs = sw.ElapsedMilliseconds;
            log.Status = 1;
            log.ErrorMsg = ex.Message;
            logger.LogError("Job [{Name}] 执行失败: {Msg}", job.JobName, ex.Message);
        }

        // ⚠️ 写执行日志也必须兜异常。
        // 历史事故：sys_job_log 表缺失时这里抛 no such table，异常一路冒到 BackgroundService，
        // 触发 HostOptions.BackgroundServiceExceptionBehavior = StopHost，整个 UMC 停服。
        try
        {
            db.JobLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Job [{Name}] 执行日志写入失败（不影响服务运行）: {Msg}",
                job.JobName, ex.Message);
        }
    }

    private static Task RunTargetAsync(string invokeTarget, ApplicationDbContext db, CancellationToken ct)
    {
        return invokeTarget switch
        {
            "CleanExpiredTokensJob" => CleanOldLogsAsync(db, ct),
            "SyncUserCacheJob"      => Task.CompletedTask,
            "GenerateMonthlyReport" => Task.CompletedTask,
            _ => Task.CompletedTask,
        };
    }

    private static async Task CleanOldLogsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var old = db.OperLogs.Where(l => l.OperTime < cutoff).ToList();
        if (old.Count > 0)
        {
            db.OperLogs.RemoveRange(old);
            await db.SaveChangesAsync(ct);
        }
    }
}
