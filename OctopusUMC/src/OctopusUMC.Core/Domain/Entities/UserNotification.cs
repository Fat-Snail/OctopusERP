namespace OctopusUMC.Core.Domain.Entities;

/// <summary>
/// P9 跨系统统一用户通知（与 Notice 公告区分：Notice 是广播给所有人的公告，
/// UserNotification 是定向推送给某个用户的业务消息——审批结果/库存预警/工单完工等）。
/// </summary>
public class UserNotification
{
    public long NotificationId { get; set; }

    /// <summary>目标用户（UMC UserId）</summary>
    public long UserId { get; set; }

    /// <summary>来源系统：umc / oa / crm / wms / mes / fin / hrm / oms</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>通知类型：approval_result / approval_reminder / stock_alert / workorder_done / mention / notice</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>通知标题（< 100 字符，列表展示）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>通知正文（详情展示）</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>点击跳转 URL（可选，例如审批详情页：/approval/123）</summary>
    public string? LinkUrl { get; set; }

    /// <summary>附加 JSON 负载（可选，用于前端富展示）</summary>
    public string? PayloadJson { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
