using System.Text.Json;
using OctopusOA.Api.Workflow;

namespace OctopusOA.Api.Services;

/// <summary>
/// Q3-② 报销审批通过 → 推送 FIN 生成费用凭证（事件监听器扩展点，引擎零改动）。
/// FIN 侧按 OaApprovalId 幂等（Source="oa-expense" + SourceRef），重复推送不会重复记账。
/// </summary>
public class ExpenseApprovedListener(WebhookSender webhook) : IProcessEventListener
{
    public void OnEvent(ProcessEvent evt)
    {
        var payload = TryBuildPayload(evt);
        if (payload == null) return;

        webhook.PostToFin("/api/webhook/oa/expense-approved", payload);
    }

    /// <summary>public 供单元测试：仅 expense 流程且终审通过、且表单含有效金额时产出 payload</summary>
    public static OaExpensePayload? TryBuildPayload(ProcessEvent evt)
    {
        if (!string.Equals(evt.Definition.Code, "expense", StringComparison.OrdinalIgnoreCase)) return null;
        if (evt.Type != ProcessEventTypes.Approved) return null;

        decimal? amount = null;
        string? description = null;
        try
        {
            using var doc = JsonDocument.Parse(evt.Instance.FormData);
            if (doc.RootElement.TryGetProperty("amount", out var amtEl)
                && amtEl.ValueKind == JsonValueKind.Number
                && amtEl.TryGetDecimal(out var amt))
            {
                amount = amt;
            }
            if (doc.RootElement.TryGetProperty("description", out var descEl)
                && descEl.ValueKind == JsonValueKind.String)
            {
                description = descEl.GetString();
            }
        }
        catch (JsonException)
        {
            return null; // 表单数据损坏，忽略（审批引擎层面已有校验，此处防御）
        }

        if (amount is null or <= 0) return null;

        var summary = string.IsNullOrWhiteSpace(description)
            ? $"报销支出：{evt.Instance.Title}"
            : $"报销支出：{description}";

        return new OaExpensePayload(evt.Instance.Id, amount.Value, summary);
    }
}

/// <summary>与 FIN 侧 OaExpenseRequest 契约对齐（OaApprovalId / Amount / Summary）</summary>
public record OaExpensePayload(long OaApprovalId, decimal Amount, string? Summary);
