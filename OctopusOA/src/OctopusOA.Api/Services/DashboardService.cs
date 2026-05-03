using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Services;

/// <summary>首页工作台聚合服务</summary>
public class DashboardService
{
    private readonly OaDbContext _db;
    private readonly ApprovalService _approval;
    private readonly NoticeService _notice;
    private readonly AttendanceService _attendance;
    private readonly MeetingService _meeting;

    public DashboardService(
        OaDbContext db,
        ApprovalService approval,
        NoticeService notice,
        AttendanceService attendance,
        MeetingService meeting)
    {
        _db = db;
        _approval = approval;
        _notice = notice;
        _attendance = attendance;
        _meeting = meeting;
    }

    /// <summary>首页卡片数据</summary>
    public DashboardSummary GetSummary(long userId)
    {
        var pendingApprovals = _approval.GetPendingForUser(userId).Count;
        var unreadNotices = _notice.GetUnreadCount(userId);
        var todayAttendance = BuildTodaySnapshot(userId);
        var myApprovals = _db.Approvals.Count(a => a.ApplicantId == userId);

        return new DashboardSummary
        {
            PendingApprovals = pendingApprovals,
            UnreadNotices = unreadNotices,
            TodayAttendance = todayAttendance,
            TodayMeetings = _meeting.GetToday(userId).Count,
            MyApprovalsCount = myApprovals,
        };
    }

    /// <summary>聚合待办列表</summary>
    public List<TodoItem> GetTodos(long userId, string? type)
    {
        var all = new List<TodoItem>();

        // 1. 待审批
        if (type == null || type == "approval")
        {
            foreach (var a in _approval.GetPendingForUser(userId))
            {
                var template = _db.Templates.FirstOrDefault(t => t.TemplateId == a.TemplateId);
                all.Add(new TodoItem
                {
                    Type = "approval", Id = a.ApprovalId,
                    Title = a.Title,
                    Subtitle = $"{a.ApplicantName} · {template?.TemplateName ?? "审批"}",
                    Time = a.CreateTime.ToString("o"),
                    Link = "/approval/pending",
                    Tag = "待审批", TagType = "warning",
                });
            }
        }

        // 2. 未读公告
        if (type == null || type == "notice")
        {
            var readIds = _db.OaNoticeReads
                .Where(r => r.UmcUserId == userId).Select(r => r.NoticeId).ToList().ToHashSet();
            var unread = _db.OaNotices
                .Where(n => n.Status == 1)
                .ToList()
                .Where(n => !readIds.Contains(n.NoticeId))
                .OrderByDescending(n => n.Priority).ThenByDescending(n => n.PublishTime)
                .Take(10)
                .ToList();
            foreach (var n in unread)
            {
                var tag = n.NoticeType switch { "3" => "紧急", "2" => "公告", _ => "通知" };
                var tagType = n.NoticeType switch { "3" => "danger", "2" => "warning", _ => "info" };
                all.Add(new TodoItem
                {
                    Type = "notice", Id = n.NoticeId,
                    Title = n.Title,
                    Subtitle = $"{n.Publisher} · 未读公告",
                    Time = n.PublishTime.ToString("o"),
                    Link = $"/notice/{n.NoticeId}",
                    Tag = tag, TagType = tagType,
                });
            }
        }

        // 3. 今日打卡提醒
        if (type == null || type == "attendance")
        {
            var today = _attendance.GetToday(userId);
            if (today.CanCheckIn)
            {
                all.Add(new TodoItem
                {
                    Type = "attendance", Id = 0,
                    Title = "今日未打上班卡",
                    Subtitle = $"工作时间 {today.RuleWorkStart}–{today.RuleWorkEnd}",
                    Time = DateTime.UtcNow.ToString("o"),
                    Link = "/attendance/mine",
                    Tag = "打卡", TagType = "info",
                });
            }
            else if (today.CanCheckOut)
            {
                all.Add(new TodoItem
                {
                    Type = "attendance", Id = 0,
                    Title = "今日未打下班卡",
                    Subtitle = $"工作时间 {today.RuleWorkStart}–{today.RuleWorkEnd}",
                    Time = DateTime.UtcNow.ToString("o"),
                    Link = "/attendance/mine",
                    Tag = "打卡", TagType = "info",
                });
            }
        }

        // 4. 今日会议
        if (type == null || type == "meeting")
        {
            foreach (var m in _meeting.GetToday(userId))
            {
                all.Add(new TodoItem
                {
                    Type = "meeting", Id = m.Id,
                    Title = m.Title,
                    Subtitle = $"{m.RoomName} · {m.StartTime.AddHours(8):HH:mm}-{m.EndTime.AddHours(8):HH:mm}",
                    Time = m.StartTime.ToString("o"),
                    Link = "/meeting/mine",
                    Tag = "会议", TagType = "success",
                });
            }
        }

        // 按时间倒序
        return all.OrderByDescending(t => t.Time).ToList();
    }

    private TodayAttendanceSnapshot BuildTodaySnapshot(long userId)
    {
        var t = _attendance.GetToday(userId);
        return new TodayAttendanceSnapshot
        {
            CheckedIn = !t.CanCheckIn,
            CheckedOut = !t.CanCheckOut && !t.CanCheckIn,
            CheckInStatus = t.CheckInStatus,
            CheckOutStatus = t.CheckOutStatus,
            CheckInTime = t.CheckInTime?.ToString("o"),
            CheckOutTime = t.CheckOutTime?.ToString("o"),
        };
    }
}
