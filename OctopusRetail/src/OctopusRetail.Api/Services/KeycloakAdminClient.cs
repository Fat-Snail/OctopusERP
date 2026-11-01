using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusRetail.Api.Services;

/// <summary>
/// 零售域的 Keycloak Admin 客户端。
///
/// 为什么零售要用**自己的** service account（retail-invite-bot），而不是复用 UMC 的：
/// 见 design §5.1 —— Keycloak 的 service account 权限是 **realm 级**的，
/// 做不到「上海店的 bot 只能管上海店的 group」。因此凭据绝不能下发给前端或租户：
/// 所有建号 / 加组动作都由本服务在服务端代理，并先在自己的 membership 表校验调用者权限。
///
/// 与 UMC 的 KeycloakSyncService / KeycloakGroupService 的差异：
///   - 用户主键直接用 Keycloak sub（JWT 的 sub 就是 Keycloak user id），
///     无需按用户名回查，少一次往返且更准确
///   - 额外提供受控建号（CreateUserAsync）：用 required action 强制首次改密，
///     而不是打开 realm 的 registrationAllowed（那会让任何人注册进平台）
/// </summary>
public class KeycloakAdminClient(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<KeycloakAdminClient> logger)
{
    private static string? _cachedToken;
    private static DateTime _cachedTokenExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    /// <summary>租户在 Keycloak 中的 group 根路径（与 realm 的 groups 段一致）</summary>
    public const string TenantGroupRoot = "/tenants";

    /// <summary>
    /// Keycloak 基址。
    /// ⚠️ 实测：Aspire 的 AddKeycloak(port: 8088) 把 8088 映射到容器的 **8443(HTTPS)**。
    /// 若这里取到 http://localhost:8088，POST token 端点会得到
    /// "The response ended prematurely (ResponseEnded)"——那是把明文 HTTP 打到
    /// HTTPS 端口、服务端直接断连的表现，极难从栈里看出根因。
    /// 因此优先取 https 端点。
    /// </summary>
    private string BaseUrl =>
        config["services:keycloak:https:0"]
        ?? config["services:keycloak:http:0"]
        ?? config["Keycloak:BaseUrl"]
        ?? "https://localhost:8088";

    private string Realm => config["Keycloak:Realm"] ?? "octopus";
    private string BotClientId => config["Keycloak:BotClientId"] ?? "retail-invite-bot";
    private string BotClientSecret => config["Keycloak:BotClientSecret"] ?? "retail-invite-bot-secret-dev";

    public bool Enabled => bool.TryParse(config["Keycloak:SyncEnabled"], out var v) && v;

    /// <summary>租户对应的 group 路径，如 /tenants/t_shanghai_001</summary>
    public static string TenantGroupPath(string tenantId) => $"{TenantGroupRoot}/{tenantId}";

    // ─── 受控建号 ───────────────────────────────────────────

    /// <summary>
    /// 创建一个待激活的 Keycloak 用户。
    ///
    /// 两种模式：
    ///   password 为 null   → 随机临时密码 + UPDATE_PASSWORD required action，
    ///                        用户首次登录必须改密（适用于不接触明文口令的邀请流程）
    ///   password 有值      → 使用调用方给出的口令（邀请令牌本身已是强凭据，
    ///                        被邀请人在注册页自行设定密码，无需二次下发临时口令）
    ///
    /// 无论哪种模式，都不打开 realm 的 registrationAllowed ——
    /// 建号只能由持有服务账号的本服务在服务端发起。
    /// </summary>
    public async Task<string?> CreateUserAsync(
        string userName,
        string email,
        string displayName,
        string? password = null,
        bool requirePasswordChange = true,
        CancellationToken ct = default)
    {
        if (!Enabled) return null;
        try
        {
            var http = await AuthedClientAsync(ct);
            var effectivePassword = password ?? Convert.ToBase64String(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

            var payload = new
            {
                username = userName,
                email,
                firstName = displayName,
                enabled = true,
                emailVerified = false,
                credentials = new[] { new { type = "password", value = effectivePassword, temporary = requirePasswordChange } },
                requiredActions = requirePasswordChange ? new[] { "UPDATE_PASSWORD" } : Array.Empty<string>(),
            };

            var resp = await http.PostAsJsonAsync($"{BaseUrl}/admin/realms/{Realm}/users", payload, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Keycloak 建号失败 UserName={UserName} Status={Status}", userName, resp.StatusCode);
                return null;
            }

            // 创建返回 201 + Location，需回查拿 id
            var userId = await FindUserIdByNameAsync(http, userName, ct);
            logger.LogInformation("Keycloak 用户已创建 UserName={UserName}", userName);
            return userId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak 建号异常 UserName={UserName}", userName);
            return null;
        }
    }

    // ─── Group 原语（按 sub，无需回查用户名） ───────────────

    /// <summary>确保组路径存在，返回组 ID；失败返回 null</summary>
    public async Task<string?> EnsureGroupAsync(string groupPath, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        try
        {
            var http = await AuthedClientAsync(ct);
            var segments = groupPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            string? parentId = null;
            var prefix = string.Empty;

            foreach (var segment in segments)
            {
                prefix += "/" + segment;
                var tree = await FetchGroupTreeAsync(http, ct);
                if (!tree.TryGetValue(prefix, out var existing))
                {
                    var url = parentId is null
                        ? $"{BaseUrl}/admin/realms/{Realm}/groups"
                        : $"{BaseUrl}/admin/realms/{Realm}/groups/{parentId}/children";
                    var create = await http.PostAsJsonAsync(url, new { name = segment }, ct);
                    if (!create.IsSuccessStatusCode)
                    {
                        logger.LogWarning("Keycloak 组创建失败 Path={Path} Status={Status}", prefix, create.StatusCode);
                        return null;
                    }
                    tree = await FetchGroupTreeAsync(http, ct);
                    if (!tree.TryGetValue(prefix, out existing)) return null;
                }
                parentId = existing;
            }
            return parentId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak 确保组存在异常 Path={Path}", groupPath);
            return null;
        }
    }

    /// <summary>把用户加入组（按 Keycloak sub）</summary>
    public async Task<bool> AddUserToGroupAsync(string kcSub, string groupPath, CancellationToken ct = default)
    {
        if (!Enabled) return false;
        try
        {
            var http = await AuthedClientAsync(ct);
            var groupId = await FindGroupIdAsync(http, groupPath, ct);
            if (groupId is null) return false;

            var resp = await http.PutAsync(
                $"{BaseUrl}/admin/realms/{Realm}/users/{kcSub}/groups/{groupId}", null, ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Keycloak 加入组失败 Sub={Sub} Path={Path} Status={Status}",
                    kcSub, groupPath, resp.StatusCode);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak 加入组异常 Sub={Sub} Path={Path}", kcSub, groupPath);
            return false;
        }
    }

    /// <summary>把用户移出组（按 Keycloak sub）</summary>
    public async Task<bool> RemoveUserFromGroupAsync(string kcSub, string groupPath, CancellationToken ct = default)
    {
        if (!Enabled) return false;
        try
        {
            var http = await AuthedClientAsync(ct);
            var groupId = await FindGroupIdAsync(http, groupPath, ct);
            if (groupId is null) return false;

            var resp = await http.DeleteAsync(
                $"{BaseUrl}/admin/realms/{Realm}/users/{kcSub}/groups/{groupId}", ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Keycloak 移出组失败 Sub={Sub} Path={Path} Status={Status}",
                    kcSub, groupPath, resp.StatusCode);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak 移出组异常 Sub={Sub} Path={Path}", kcSub, groupPath);
            return false;
        }
    }

    /// <summary>
    /// 按 sub 读取用户名/姓名（用于成员列表快照）。
    /// 失败返回 null，不抛——快照缺失不影响业务。
    /// </summary>
    public async Task<(string? UserName, string? DisplayName)?> TryGetUserAsync(
        string kcSub, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        try
        {
            var http = await AuthedClientAsync(ct);
            var resp = await http.GetAsync($"{BaseUrl}/admin/realms/{Realm}/users/{kcSub}", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var u = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var username = u.TryGetProperty("username", out var un) ? un.GetString() : null;
            var firstName = u.TryGetProperty("firstName", out var fn) ? fn.GetString() : null;
            var lastName = u.TryGetProperty("lastName", out var ln) ? ln.GetString() : null;
            var display = string.Join(" ", new[] { firstName, lastName }
                .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            return (username, string.IsNullOrWhiteSpace(display) ? null : display);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "读取 Keycloak 用户失败 Sub={Sub}", kcSub);
            return null;
        }
    }

    /// <summary>启用/禁用 Keycloak 用户（禁用的用户无法再登录，且无法换取新 token）</summary>
    public async Task<bool> SetUserEnabledAsync(string kcSub, bool enabled, CancellationToken ct = default)
    {
        if (!Enabled) return false;
        try
        {
            var http = await AuthedClientAsync(ct);
            var resp = await http.PutAsJsonAsync(
                $"{BaseUrl}/admin/realms/{Realm}/users/{kcSub}",
                new { enabled }, ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Keycloak 设置启用状态失败 Sub={Sub} Enabled={Enabled} Status={Status}",
                    kcSub, enabled, resp.StatusCode);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak 设置启用状态异常 Sub={Sub}", kcSub);
            return false;
        }
    }

    /// <summary>重置 Keycloak 用户密码</summary>
    public async Task<bool> SetPasswordAsync(
        string kcSub, string newPassword, bool temporary = false, CancellationToken ct = default)
    {
        if (!Enabled) return false;
        try
        {
            var http = await AuthedClientAsync(ct);
            var resp = await http.PutAsJsonAsync(
                $"{BaseUrl}/admin/realms/{Realm}/users/{kcSub}/reset-password",
                new { type = "password", value = newPassword, temporary }, ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Keycloak 重置密码失败 Sub={Sub} Status={Status}", kcSub, resp.StatusCode);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak 重置密码异常 Sub={Sub}", kcSub);
            return false;
        }
    }

    /// <summary>读取用户所属的全部组路径</summary>
    public async Task<List<string>> GetUserGroupPathsAsync(string kcSub, CancellationToken ct = default)
    {
        if (!Enabled) return new List<string>();
        try
        {
            var http = await AuthedClientAsync(ct);
            var resp = await http.GetAsync(
                $"{BaseUrl}/admin/realms/{Realm}/users/{kcSub}/groups?briefRepresentation=true&max=1000", ct);
            if (!resp.IsSuccessStatusCode) return new List<string>();

            var arr = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (arr.ValueKind != JsonValueKind.Array) return new List<string>();

            return arr.EnumerateArray()
                .Select(g => g.TryGetProperty("path", out var p) ? p.GetString() : null)
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p!)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak 读取用户组异常 Sub={Sub}", kcSub);
            return new List<string>();
        }
    }

    // ─── 内部 ───────────────────────────────────────────────

    private async Task<string?> FindGroupIdAsync(HttpClient http, string groupPath, CancellationToken ct)
    {
        var tree = await FetchGroupTreeAsync(http, ct);
        return tree.TryGetValue("/" + groupPath.Trim('/'), out var id) ? id : null;
    }

    private async Task<Dictionary<string, string>> FetchGroupTreeAsync(HttpClient http, CancellationToken ct)
    {
        var acc = new Dictionary<string, string>(StringComparer.Ordinal);
        var resp = await http.GetAsync(
            $"{BaseUrl}/admin/realms/{Realm}/groups?briefRepresentation=false&max=1000&first=0", ct);
        if (!resp.IsSuccessStatusCode) return acc;
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        await ExpandGroupsAsync(http, json, acc, ct);
        return acc;
    }

    /// <summary>
    /// 递归展开组树。
    ///
    /// ⚠️ Keycloak 26 的 <c>GET /groups</c> **只返回 subGroupCount，不再内联 subGroups**
    /// （实测：<c>{"path":"/tenants","subGroupCount":1,"subGroups":[]}</c>）。
    /// 因此当 subGroups 为空而 subGroupCount &gt; 0 时，必须显式拉
    /// <c>GET /groups/{id}/children</c>，否则多级组路径（如 /tenants/{code}）永远解析不出来
    /// —— 表现为 EnsureGroupAsync 反复尝试创建已存在的组并拿到 409，最终返回 null。
    ///
    /// 两种响应形态都兼容：老版本/其他配置若内联 subGroups，则直接递归，省一次往返。
    /// </summary>
    private async Task ExpandGroupsAsync(
        HttpClient http, JsonElement element, Dictionary<string, string> acc, CancellationToken ct)
    {
        if (element.ValueKind != JsonValueKind.Array) return;

        foreach (var group in element.EnumerateArray())
        {
            var path = group.TryGetProperty("path", out var p) ? p.GetString() : null;
            var id = group.TryGetProperty("id", out var i) ? i.GetString() : null;
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(id)) continue;
            acc[path!] = id!;

            var inlineLen = group.TryGetProperty("subGroups", out var subs) && subs.ValueKind == JsonValueKind.Array
                ? subs.GetArrayLength()
                : 0;
            if (inlineLen > 0)
            {
                await ExpandGroupsAsync(http, group.GetProperty("subGroups"), acc, ct);
                continue;
            }

            var subCount = group.TryGetProperty("subGroupCount", out var sc) && sc.ValueKind == JsonValueKind.Number
                ? sc.GetInt32()
                : 0;
            if (subCount <= 0) continue;

            var childrenResp = await http.GetAsync(
                $"{BaseUrl}/admin/realms/{Realm}/groups/{id}/children?briefRepresentation=false&max=1000&first=0", ct);
            if (!childrenResp.IsSuccessStatusCode) continue;
            var children = await childrenResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            await ExpandGroupsAsync(http, children, acc, ct);
        }
    }

    private async Task<string?> FindUserIdByNameAsync(HttpClient http, string userName, CancellationToken ct)
    {
        var resp = await http.GetAsync(
            $"{BaseUrl}/admin/realms/{Realm}/users?username={Uri.EscapeDataString(userName)}&exact=true", ct);
        if (!resp.IsSuccessStatusCode) return null;
        var arr = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;
        return arr[0].TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    private async Task<HttpClient> AuthedClientAsync(CancellationToken ct)
    {
        var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(ct));
        return http;
    }

    /// <summary>client_credentials 拿 admin token，过期前 30s 复用</summary>
    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _cachedTokenExpiry.AddSeconds(-30))
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _cachedTokenExpiry.AddSeconds(-30))
                return _cachedToken;

            var http = httpFactory.CreateClient();
            var resp = await http.PostAsync(
                $"{BaseUrl}/realms/{Realm}/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = BotClientId,
                    ["client_secret"] = BotClientSecret,
                }), ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            _cachedToken = json.GetProperty("access_token").GetString()!;
            _cachedTokenExpiry = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32());
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
