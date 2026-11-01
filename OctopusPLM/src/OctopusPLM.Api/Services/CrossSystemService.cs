using System.Text;
using System.Text.Json;

namespace OctopusPLM.Api.Services;

/// <summary>聚合 WMS 库存 + CRM 销量到 PLM 商品视图（读取 Webhooks:WmsBaseUrl / CrmBaseUrl 配置；不可达时静默返回零值）</summary>
public class CrossSystemService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<CrossSystemService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>批量拉取 productIds 的 WMS 库存汇总；返回字典 productId → StockSummary</summary>
    public async Task<Dictionary<long, StockSummary>> GetStockAsync(IEnumerable<long> productIds)
    {
        var ids = productIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0) return new();

        var baseUrl = config["Webhooks:WmsBaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return new();

        try
        {
            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var body = JsonSerializer.Serialize(new { productIds = ids });
            var resp = await client.PostAsync(
                $"{baseUrl}/api/inventory/by-products",
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode) return new();
            var json = await resp.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<ApiWrapper<List<StockSummary>>>(json, JsonOpts);
            return (wrapper?.Data ?? new()).ToDictionary(x => x.PlmProductId);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "WMS by-products 不可达");
            return new();
        }
    }

    /// <summary>批量拉取 productIds 的 CRM 销量汇总；返回字典 productId → SalesSummary</summary>
    public async Task<Dictionary<long, SalesSummary>> GetSalesAsync(IEnumerable<long> productIds, int days = 30)
    {
        var ids = productIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0) return new();

        var baseUrl = config["Webhooks:CrmBaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return new();

        try
        {
            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var body = JsonSerializer.Serialize(new { productIds = ids, days });
            var resp = await client.PostAsync(
                $"{baseUrl}/api/contract/sales-qty",
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode) return new();
            var json = await resp.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<ApiWrapper<List<SalesSummary>>>(json, JsonOpts);
            return (wrapper?.Data ?? new()).ToDictionary(x => x.PlmProductId);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "CRM sales-qty 不可达");
            return new();
        }
    }

    public class ApiWrapper<T>
    {
        public int Code { get; set; }
        public string? Msg { get; set; }
        public T? Data { get; set; }
    }
}

public class StockSummary
{
    public long PlmProductId { get; set; }
    public decimal TotalQty { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal LockedQty { get; set; }
    public int WarehouseCount { get; set; }
}

public class SalesSummary
{
    public long PlmProductId { get; set; }
    public decimal SalesQty { get; set; }
    public decimal SalesAmount { get; set; }
    public int ContractCount { get; set; }
    public int Days { get; set; }
}
