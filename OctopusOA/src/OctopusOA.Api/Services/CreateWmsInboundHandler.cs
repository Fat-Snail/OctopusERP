using System.Text.Json;
using OctopusOA.Api.Workflow;

namespace OctopusOA.Api.Services;

/// <summary>
/// 采购订单审批通过 → 自动创建 WMS 入库单（service_task: create_wms_inbound）。
/// 引擎侧 seam：审批中心不复制子系统数据，只按表单快照通过 webhook 通知 WMS。
/// 注：WMS receive-goods 当前为「生产入库」语义，采购入库专用端点需 WMS 侧补充（后续）。
/// </summary>
public class CreateWmsInboundHandler(WebhookSender webhook) : IServiceTaskHandler
{
    public string Kind => "create_wms_inbound";

    public string? Execute(ServiceTaskContext ctx)
    {
        Dictionary<string, JsonElement> data;
        try { data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ctx.Instance.FormData) ?? new(); }
        catch { data = new(); }

        webhook.PostToWms("/api/webhook/receive-goods", new
        {
            MesWorkOrderId = 0L,
            PlmProductId = GetInt64(data, "plmProductId"),
            ProductName = GetString(data, "productName") ?? "",
            Spec = GetString(data, "spec"),
            Unit = GetString(data, "unit"),
            Quantity = GetDecimal(data, "quantity"),
            InboundType = "purchase",
        });
        return null;
    }

    private static string? GetString(Dictionary<string, JsonElement> d, string k)
        => d.TryGetValue(k, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private static long? GetInt64(Dictionary<string, JsonElement> d, string k)
        => d.TryGetValue(k, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var v) ? v : null;

    private static decimal GetDecimal(Dictionary<string, JsonElement> d, string k)
        => d.TryGetValue(k, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetDecimal() : 0;
}
