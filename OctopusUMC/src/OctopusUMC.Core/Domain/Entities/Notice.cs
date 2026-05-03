namespace OctopusUMC.Core.Domain.Entities;

public class Notice
{
    public long NoticeId { get; set; }
    public string NoticeTitle { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "1"; // 1=通知 2=公告
    public string NoticeContent { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string CreateBy { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }
}
