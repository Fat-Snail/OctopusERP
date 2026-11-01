using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusUMC.Api.Services;

/// <summary>用户同步服务：UMC 用户变更时推送到 OA</summary>
public class UserSyncService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<UserSyncService> _logger;

    public UserSyncService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<UserSyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyUserChangedAsync(UserSyncPayload payload)
    {
        // ADR-010：只有平台员工才下发到 OA 等下游系统。
        // 零售域邀请进来的客户/店员虽然也在 sys_user（统一用户中心），
        // 但绝不能出现在 OA 通讯录里——这正是当初否掉「零售用户进 UMC」的理由，
        // 用一次类型过滤就能解决，不需要把用户挡在 UMC 之外。
        if (!string.Equals(payload.UserType, "employee", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("跳过非员工用户的同步：UserId={UserId} Type={Type}",
                payload.UserId, payload.UserType);
            return;
        }

        var oaBaseUrl = _config["UserSync:OaBaseUrl"];
        var secret = _config["UserSync:SharedSecret"];

        if (string.IsNullOrEmpty(oaBaseUrl) || string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("UserSync 未配置 OaBaseUrl 或 SharedSecret，跳过同步");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var signature = ComputeHmacSha256(json, secret);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{oaBaseUrl}/api/users/sync");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Sync-Signature", $"sha256={signature}");

            var response = await client.SendAsync(request);
            _logger.LogInformation("用户同步推送到 OA：{StatusCode}，UserId={UserId}", response.StatusCode, payload.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "用户同步推送失败，UserId={UserId}", payload.UserId);
        }
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public class UserSyncPayload
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int Status { get; set; }

    /// <summary>employee | customer | partner；只有 employee 会被下发</summary>
    public string UserType { get; set; } = "employee";

    public DateTime SyncAt { get; set; } = DateTime.UtcNow;
}
