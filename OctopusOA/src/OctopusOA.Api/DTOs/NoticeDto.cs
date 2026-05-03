namespace OctopusOA.Api.DTOs;

public class NoticeItem
{
    public long NoticeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "1";
    public int Priority { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public DateTime PublishTime { get; set; }
    public string Source { get; set; } = string.Empty;
    public int Status { get; set; }
    public bool IsRead { get; set; }
}

public class NoticeSyncPayload
{
    public string Action { get; set; } = "upsert"; // upsert / delete
    public long NoticeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "1";
    public int Priority { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public DateTime PublishTime { get; set; }
    public int Status { get; set; } = 1;
}
