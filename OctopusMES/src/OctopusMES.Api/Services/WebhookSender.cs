using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusMES.Api.Services;

public class WebhookSender(IHttpClientFactory factory, IConfiguration config, ILogger<WebhookSender> logger)
{
    public void PostToWms(string path, object payload)
    {
        var baseUrl = config["Webhooks:WmsBaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return;
        var secret = config["Webhooks:WmsSecret"] ?? "";
        FireAndForget(baseUrl + path, payload, "X-Wms-Signature", secret);
    }

    /// <summary>P9 — 推送到 UMC 统一通知中心（工单完工等业务事件）</summary>
    public void PostToUmcNotify(long userId, string source, string type, string title,
        string? message = null, string? linkUrl = null, string? payloadJson = null)
    {
        var baseUrl = config["Webhooks:UmcBaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return;
        var secret = config["Webhooks:UmcNotifySecret"] ?? "";
        FireAndForget(baseUrl + "/api/internal/notify",
            new { userId, source, type, title, message, linkUrl, payloadJson },
            "X-Notify-Signature", secret);
    }

    private void FireAndForget(string url, object payload, string sigHeader, string secret)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var body = JsonSerializer.Serialize(payload);
                var sig = ComputeHmac(body, secret);
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                    { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                req.Headers.TryAddWithoutValidation(sigHeader, sig);
                using var resp = await factory.CreateClient().SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                    logger.LogWarning("Webhook {Url} returned {Status}", url, resp.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Webhook {Url} failed", url);
            }
        });
    }

    private static string ComputeHmac(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLower();
    }
}
