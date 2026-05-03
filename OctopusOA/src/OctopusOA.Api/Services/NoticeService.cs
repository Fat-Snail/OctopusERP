using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Services;

public class NoticeService
{
    private readonly OaDbContext _db;
    public NoticeService(OaDbContext db) => _db = db;

    /// <summary>查询公告列表（带已读状态）</summary>
    public List<NoticeItem> GetList(long userId, string? type, int? status)
    {
        var all = _db.OaNotices.ToList();
        if (!string.IsNullOrEmpty(type)) all = all.Where(n => n.NoticeType == type).ToList();
        if (status.HasValue) all = all.Where(n => n.Status == status.Value).ToList();

        var readIds = _db.OaNoticeReads.Where(r => r.UmcUserId == userId).Select(r => r.NoticeId).ToList().ToHashSet();

        return all
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.PublishTime)
            .Select(n => MapToItem(n, readIds))
            .ToList();
    }

    /// <summary>首页最新公告（仅 status=1 且未读优先）</summary>
    public List<NoticeItem> GetLatest(long userId, int limit = 5)
    {
        var readIds = _db.OaNoticeReads.Where(r => r.UmcUserId == userId).Select(r => r.NoticeId).ToList().ToHashSet();
        return _db.OaNotices
            .Where(n => n.Status == 1)
            .ToList()
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.PublishTime)
            .Take(limit)
            .Select(n => MapToItem(n, readIds))
            .ToList();
    }

    /// <summary>获取未读数量</summary>
    public int GetUnreadCount(long userId)
    {
        var readIds = _db.OaNoticeReads.Where(r => r.UmcUserId == userId).Select(r => r.NoticeId).ToList().ToHashSet();
        return _db.OaNotices.Where(n => n.Status == 1).ToList().Count(n => !readIds.Contains(n.NoticeId));
    }

    /// <summary>获取详情 + 自动标记已读</summary>
    public NoticeItem? GetById(long noticeId, long userId, bool autoMarkRead = true)
    {
        var notice = _db.OaNotices.FirstOrDefault(n => n.NoticeId == noticeId);
        if (notice == null) return null;
        if (autoMarkRead) MarkRead(noticeId, userId);
        var readIds = _db.OaNoticeReads.Where(r => r.UmcUserId == userId).Select(r => r.NoticeId).ToList().ToHashSet();
        return MapToItem(notice, readIds);
    }

    /// <summary>标记已读</summary>
    public void MarkRead(long noticeId, long userId)
    {
        if (_db.OaNoticeReads.Any(r => r.NoticeId == noticeId && r.UmcUserId == userId)) return;
        _db.OaNoticeReads.Add(new OaNoticeRead
        {
            NoticeId = noticeId,
            UmcUserId = userId,
            ReadAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
    }

    /// <summary>处理 UMC 公告同步</summary>
    public void Sync(NoticeSyncPayload payload)
    {
        var existing = _db.OaNotices.FirstOrDefault(n => n.NoticeId == payload.NoticeId);

        if (payload.Action == "delete")
        {
            if (existing != null) _db.OaNotices.Remove(existing);
            var reads = _db.OaNoticeReads.Where(r => r.NoticeId == payload.NoticeId).ToList();
            _db.OaNoticeReads.RemoveRange(reads);
            _db.SaveChanges();
            return;
        }

        if (existing != null)
        {
            existing.Title = payload.Title;
            existing.Content = payload.Content;
            existing.NoticeType = payload.NoticeType;
            existing.Priority = payload.Priority;
            existing.Publisher = payload.Publisher;
            existing.PublishTime = payload.PublishTime;
            existing.Status = payload.Status;
        }
        else
        {
            _db.OaNotices.Add(new OaNotice
            {
                NoticeId = payload.NoticeId,
                Title = payload.Title,
                Content = payload.Content,
                NoticeType = payload.NoticeType,
                Priority = payload.Priority,
                Publisher = payload.Publisher,
                PublishTime = payload.PublishTime,
                Source = "umc",
                Status = payload.Status,
            });
        }
        _db.SaveChanges();
    }

    private static NoticeItem MapToItem(OaNotice n, HashSet<long> readIds) => new()
    {
        NoticeId = n.NoticeId, Title = n.Title, Content = n.Content,
        NoticeType = n.NoticeType, Priority = n.Priority,
        Publisher = n.Publisher, PublishTime = n.PublishTime,
        Source = n.Source, Status = n.Status,
        IsRead = readIds.Contains(n.NoticeId),
    };
}
