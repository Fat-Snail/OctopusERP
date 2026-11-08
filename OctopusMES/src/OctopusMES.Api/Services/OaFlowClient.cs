using System.Net.Http.Json;
using System.Text.Json;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

/// <summary>向审批中心（OA OctopusFlow）发起采购审批的客户端抽象（便于测试替身）</summary>
public interface IOaFlowClient
{
    /// <summary>提交采购单审批；返回审批中心流程实例 id，未配置则返回 null</summary>
    Task<long?> SubmitPurchaseAsync(MesPurchaseOrder po);
}

/// <summary>
/// HTTP 实现：调用 OA /api/flow/submit（按 definitionCode=purchase_order 发起）。
/// 未配置 Webhooks:OaBaseUrl 时静默跳过，保证测试/本地无 OA 时行为不变。
/// </summary>
public class HttpOaFlowClient(IHttpClientFactory factory, IConfiguration config, ILogger<HttpOaFlowClient> logger) : IOaFlowClient
{
    public async Task<long?> SubmitPurchaseAsync(MesPurchaseOrder po)
    {
        var baseUrl = config["Webhooks:OaBaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return null;

        var payload = new
        {
            definitionCode = "purchase_order",
            title = $"采购单 {po.PurchaseCode}",
            formData = BuildFormData(po),
            businessKey = $"purchase_order:{po.PurchaseId}",
        };

        try
        {
            var resp = await factory.CreateClient().PostAsJsonAsync(baseUrl + "/api/flow/submit", payload);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("OA flow submit returned {Status}", resp.StatusCode);
                return null;
            }
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200) return null;
            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("instanceId", out var id))
                return id.GetInt64();
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OA flow submit failed");
            return null;
        }
    }

    private static string BuildFormData(MesPurchaseOrder po)
    {
        var first = po.Items.FirstOrDefault();
        var data = new Dictionary<string, object?>
        {
            ["supplierName"] = po.Supplier?.SupplierName,
            ["productName"] = first?.ProductName,
            ["plmProductId"] = first?.PlmProductId,
            ["spec"] = first?.Spec,
            ["unit"] = first?.Unit,
            ["quantity"] = first?.Quantity,
            ["amount"] = po.TotalAmount,
            ["remark"] = po.Remark,
        };
        return JsonSerializer.Serialize(data);
    }
}
