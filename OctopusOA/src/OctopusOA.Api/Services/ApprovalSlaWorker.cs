using Microsoft.EntityFrameworkCore;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Services;

/// <summary>
/// 审批 SLA 后台扫描器：每 30 分钟扫描 pending 审批
/// - 截止前 4h 之内 → 标记 IsSlaWarned + 推送预警通知给申请人
/// - 已超截止时间   → 标记 IsOverdue + 推送催办通知给申请人
/// </summary>
public class ApprovalSlaWorker(IServiceProvider services, ILogger<ApprovalSlaWorker> logger)
    : BackgroundService
{
    /// <summary>预警提前时长（在截止前多久推送即将超时预警）</summary>
    public static TimeSpan WarnLeadTime { get; set; } = TimeSpan.FromHours(4);

    /// <summary>轮询间隔</summary>
    public static TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;
            try { await ScanAsync(stoppingToken); }
            catch (Exception ex) { logger.LogWarning(ex, "SLA scan failed"); }
        }
    }

    /// <summary>暴露给测试调用的扫描方法</summary>
    public async Task ScanAsync(CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var notif = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var webhook = scope.ServiceProvider.GetRequiredService<WebhookSender>();

        var now = DateTime.UtcNow;
        var warnBefore = now.Add(WarnLeadTime);

        // 1) 已超期但未标记
        var overdue = await db.Approvals
            .Where(a => a.Status == "pending"
                     && !a.IsOverdue
                     && a.CurrentNodeDeadline.HasValue
                     && a.CurrentNodeDeadline < now)
            .ToListAsync(ct);

        // 2) 即将超时（截止前 4h 内）且尚未发预警，且未已超期
        var warning = await db.Approvals
            .Where(a => a.Status == "pending"
                     && !a.IsOverdue
                     && !a.IsSlaWarned
                     && a.CurrentNodeDeadline.HasValue
                     && a.CurrentNodeDeadline >= now
                     && a.CurrentNodeDeadline < warnBefore)
            .ToListAsync(ct);

        foreach (var a in overdue)
        {
            a.IsOverdue = true;
            a.IsSlaWarned = true; // 防止后续再触发 warning
            a.UpdateTime = now;
            await notif.NotifyUserAsync(a.ApplicantId, "approval",
                $"您的申请「{a.Title}」已超过审批截止时间，请联系审批人");
            // P9：同时推送到 UMC 通知中心
            webhook.PostToUmcNotify(a.ApplicantId, "oa", "approval_reminder",
                $"审批超期：{a.Title}",
                message: "该申请已超过审批截止时间，请联系审批人",
                linkUrl: $"/approval/detail/{a.ApprovalId}");
        }

        foreach (var a in warning)
        {
            a.IsSlaWarned = true;
            a.UpdateTime = now;
            var hoursLeft = (int)Math.Ceiling((a.CurrentNodeDeadline!.Value - now).TotalHours);
            await notif.NotifyUserAsync(a.ApplicantId, "approval",
                $"您的申请「{a.Title}」将在 {hoursLeft}h 内到期，尚未完成审批");
            // P9：UMC 通知中心
            webhook.PostToUmcNotify(a.ApplicantId, "oa", "approval_reminder",
                $"审批即将超时：{a.Title}",
                message: $"将在 {hoursLeft}h 内到期",
                linkUrl: $"/approval/detail/{a.ApprovalId}");
        }

        if (overdue.Count > 0 || warning.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("SLA scan: marked {Overdue} overdue + {Warning} warning",
                overdue.Count, warning.Count);
        }
    }
}
