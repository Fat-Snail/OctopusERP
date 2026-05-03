using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusUMC.Api.Services;

/// <summary>公告同步服务：UMC 公告变更时推送到 OA</summary>
public class NoticeSyncService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<NoticeSyncService> _logger;

    public NoticeSyncService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<NoticeSyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyNoticeChangedAsync(NoticeSyncPayload payload)
    {
        var oaBaseUrl = _config["UserSync:OaBaseUrl"];
        var secret = _config["UserSync:SharedSecret"];

        if (string.IsNullOrEmpty(oaBaseUrl) || string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("NoticeSync 未配置 OaBaseUrl 或 SharedSecret，跳过同步");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var signature = ComputeHmacSha256(json, secret);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{oaBaseUrl}/api/notice/sync");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Sync-Signature", $"sha256={signature}");

            var response = await client.SendAsync(request);
            _logger.LogInformation("公告同步推送到 OA：{StatusCode}，Action={Action}，NoticeId={NoticeId}",
                response.StatusCode, payload.Action, payload.NoticeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "公告同步推送失败，NoticeId={NoticeId}", payload.NoticeId);
        }
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public class NoticeSyncPayload
{
    public string Action { get; set; } = "upsert";
    public long NoticeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "1";
    public int Priority { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public DateTime PublishTime { get; set; }
    public int Status { get; set; } = 1;
}
