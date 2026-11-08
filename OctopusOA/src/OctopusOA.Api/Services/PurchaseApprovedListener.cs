using OctopusOA.Api.Workflow;

namespace OctopusOA.Api.Services;

/// <summary>
/// 采购审批通过/驳回 → 回推 MES 更新采购单状态（事件监听器扩展点）。
/// 对应设计文档 §5.1「事件监听器」，业务方实现接口即可插拔，引擎零改动。
/// </summary>
public class PurchaseApprovedListener(WebhookSender webhook) : IProcessEventListener
{
    public void OnEvent(ProcessEvent evt)
    {
        var isPurchase = string.Equals(evt.Definition.Code, "purchase_order", StringComparison.OrdinalIgnoreCase)
            || (evt.Instance.BusinessKey?.StartsWith("purchase_order", StringComparison.OrdinalIgnoreCase) ?? false);
        if (!isPurchase) return;

        webhook.PostToMes("/api/webhook/approval-callback", new
        {
            OaApprovalId = evt.Instance.Id,
            Approved = evt.Type == ProcessEventTypes.Approved,
            Comment = (string?)null,
        });
    }
}
