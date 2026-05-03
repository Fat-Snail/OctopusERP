using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusUMC.Api.Services;

/// <summary>部门同步服务：UMC 部门变更时推送到 OA</summary>
public class DeptSyncService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DeptSyncService> _logger;

    public DeptSyncService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<DeptSyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyDeptChangedAsync(DeptSyncPayload payload)
    {
        var oaBaseUrl = _config["UserSync:OaBaseUrl"];
        var secret = _config["UserSync:SharedSecret"];

        if (string.IsNullOrEmpty(oaBaseUrl) || string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("DeptSync 未配置 OaBaseUrl 或 SharedSecret，跳过同步");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var signature = ComputeHmacSha256(json, secret);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{oaBaseUrl}/api/contact/dept/sync");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Sync-Signature", $"sha256={signature}");

            var response = await client.SendAsync(request);
            _logger.LogInformation("部门同步推送到 OA：{StatusCode}，Action={Action}，DeptId={DeptId}",
                response.StatusCode, payload.Action, payload.DeptId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "部门同步推送失败，DeptId={DeptId}", payload.DeptId);
        }
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public class DeptSyncPayload
{
    public string Action { get; set; } = "upsert"; // upsert / delete
    public long DeptId { get; set; }
    public long ParentId { get; set; }
    public string DeptName { get; set; } = string.Empty;
    public int OrderNum { get; set; }
    public int Status { get; set; }
}
