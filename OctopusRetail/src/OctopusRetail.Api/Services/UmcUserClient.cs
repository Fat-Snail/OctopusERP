using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusRetail.Api.Services;

/// <summary>
/// UMC 内部用户接口客户端（ADR-010）。
///
/// **为什么零售不自己建号了**：
/// 原来零售用 `retail-invite-bot` 直接调 Keycloak 建号，结果 UMC 的 `sys_user` 里
/// 根本没有这个人——运营在 5173 的用户管理里查不到他，也不能停用/改密，
/// 只能去 Keycloak 控制台手工操作。「统一用户中心」名不副实。
///
/// 现在改为：零售 → UMC 内部接口建号 → UMC 同步 Keycloak → 零售只写成员关系。
/// 这样用户主数据只有一份，且一定在 UMC 里。
/// </summary>
public class UmcUserClient(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<UmcUserClient> logger)
{
    /// <summary>Aspire 注入的服务发现地址优先</summary>
    private string BaseUrl =>
        (config["services:umc-api:http:0"]
         ?? config["services:umc-api:https:0"]
         ?? config["Umc:BaseUrl"]
         ?? "http://localhost:5001").TrimEnd('/');

    private string SharedSecret =>
        config["InternalApi:SharedSecret"] ?? "retail-internal-secret-dev";

    public record CreateResult(bool Success, string? KcSub, long UserId, string? Error);

    /// <summary>在 UMC 建号并同步到 Keycloak，返回 Keycloak sub</summary>
    public async Task<CreateResult> CreateRetailUserAsync(
        string userName,
        string displayName,
        string? phone,
        string? email,
        string password,
        CancellationToken ct = default)
    {
        try
        {
            var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);

            var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/internal/users/retail")
            {
                Content = JsonContent.Create(new
                {
                    userName,
                    nickName = displayName,
                    phoneNumber = phone,
                    email,
                    password,
                    source = "retail",
                }),
            };
            req.Headers.Add("X-Internal-Secret", SharedSecret);

            var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("UMC 建号失败 Status={Status} Body={Body}", resp.StatusCode, body);
                return new CreateResult(false, null, 0, ExtractMessage(body) ?? "账号服务暂时不可用");
            }

            var root = JsonDocument.Parse(body).RootElement;
            if (root.GetProperty("code").GetInt32() != 200)
                return new CreateResult(false, null, 0,
                    root.TryGetProperty("msg", out var m) ? m.GetString() : "建号失败");

            var data = root.GetProperty("data");
            var kcSub = data.TryGetProperty("kcSub", out var k) ? k.GetString() : null;
            var userId = data.TryGetProperty("userId", out var u) ? u.GetInt64() : 0;

            if (string.IsNullOrEmpty(kcSub))
                return new CreateResult(false, null, userId, "账号已建，但 Keycloak 未同步成功，请稍后重试");

            return new CreateResult(true, kcSub, userId, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "调用 UMC 建号异常，BaseUrl={BaseUrl}", BaseUrl);
            return new CreateResult(false, null, 0, "无法连接账号服务（UMC），请确认后端已启动");
        }
    }

    private static string? ExtractMessage(string body)
    {
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            return root.TryGetProperty("msg", out var m) ? m.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
