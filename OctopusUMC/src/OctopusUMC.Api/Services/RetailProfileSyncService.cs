using System.Net.Http.Json;
using OctopusCommon.Security;

namespace OctopusUMC.Api.Services;

/// <summary>
/// UMC → 零售域：成员资料快照回推（ADR-010）。
///
/// ADR-010 之后用户主数据在 UMC，零售库的 `UserName` / `DisplayName` / `Phone`
/// 只是成员列表用的快照。管理员在 5173 改了客户手机号，若不回推，
/// 零售成员列表会一直显示旧值甚至空值（实测 feiwoniu001 出现过两边不一致）。
///
/// 只对**已归属零售域**的用户（有 kcSub）有意义；员工没有零售成员关系，推过去也无人接收。
/// 失败只记日志，不阻塞 UMC 的主流程（与 Keycloak 同步同样的最终一致策略）。
/// </summary>
public class RetailProfileSyncService(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<RetailProfileSyncService> logger)
{
    private string? BaseUrl =>
        config["services:retail-api:http:0"]
        ?? config["Retail:BaseUrl"];      // Aspire 用显式环境变量，避免与 retail→umc 形成环

    private string? SharedSecret => InternalApiSecret.Resolve(config, env);

    public async Task NotifyProfileChangedAsync(
        string? kcSub, string? userName, string? displayName, string? phone,
        CancellationToken ct = default)
    {
        var baseUrl = BaseUrl?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl)) return;          // 未配置零售域：跳过
        if (string.IsNullOrEmpty(kcSub)) return;            // 不是零售域用户

        var secret = SharedSecret;
        if (secret is null)
        {
            // fail-closed：宁可不同步，也不能用开发密钥去调生产接口
            logger.LogWarning("未配置 {Key}，跳过零售资料回推", InternalApiSecret.ConfigKey);
            return;
        }

        try
        {
            var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);

            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/retail/internal/members/sync-profile")
            {
                Content = JsonContent.Create(new
                {
                    kcSub,
                    userName,
                    displayName,
                    phone,
                }),
            };
            req.Headers.Add("X-Internal-Secret", secret);

            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("零售资料回推失败 Status={Status} KcSub={KcSub}", resp.StatusCode, kcSub);
            else
                logger.LogDebug("零售资料已回推 KcSub={KcSub}", kcSub);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "零售资料回推异常 KcSub={KcSub}", kcSub);
        }
    }
}
