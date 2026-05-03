namespace OctopusUMC.Api.DTOs;

/// <summary>公告响应</summary>
public class NoticeResponse
{
    public long NoticeId { get; set; }
    public string NoticeTitle { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "1";
    public string NoticeContent { get; set; } = string.Empty;
    public int Status { get; set; }
    public string CreateBy { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>创建公告请求</summary>
public class CreateNoticeRequest
{
    public string NoticeTitle { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "1";
    public string NoticeContent { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>修改公告请求</summary>
public class UpdateNoticeRequest
{
    public long NoticeId { get; set; }
    public string NoticeTitle { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "1";
    public string NoticeContent { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}
