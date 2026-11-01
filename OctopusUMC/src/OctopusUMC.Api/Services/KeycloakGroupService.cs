using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusUMC.Api.Services;

/// <summary>组同步结果</summary>
public record GroupSyncResult(bool Success, List<string> Added, List<string> Removed, string? Error = null);

/// <summary>
/// Keycloak Group 同步服务（M0）
///
/// 弥补 MINIAPP-PLATFORM.md M2 未完成的「部门 = Keycloak Groups 同步」欠债，
/// 同时为零售租户（design/retail-tenant-workspace.md §4.1）提供通用组原语：
///   - EnsureGroupAsync       确保组路径存在（按层级创建）
///   - AddUserToGroupAsync    用户加入组（仅解析已有组，不隐式创建）
///   - RemoveUserFromGroupAsync 用户移出组
///   - GetUserGroupPathsAsync 读取用户全部组路径
///   - SyncUserGroupsAsync    reconcile 同步，**只在受管前缀内增删**，保护前缀外的组
///
/// 安全边界：一个用户可能同时属于业务组（/tenants/...）和 ERP 组织组（/depts/...）。
/// reconcile 必须限定前缀，否则一次「调店」同步会把用户的 ERP 组织关系一并清空。
///
/// 与 KeycloakSyncService 的关系：共享同一套 Keycloak:* 配置，但各自持有 token 缓存
/// （互不影响的静态缓存，避免改动 KeycloakSyncService 的既有测试契约）。
/// </summary>
public class KeycloakGroupService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<KeycloakGroupService> _logger;

    private static string? _cachedToken;
    private static DateTime _cachedTokenExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    public KeycloakGroupService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<KeycloakGroupService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    private string BaseUrl =>
        // ⚠️ 优先 https，原因见 KeycloakSyncService.BaseUrl
        _config["services:keycloak:https:0"]
        ?? _config["services:keycloak:http:0"]
        ?? _config["Keycloak:BaseUrl"]
        ?? "https://localhost:8088";

    private string Realm => _config["Keycloak:Realm"] ?? "octopus";
    private string SyncBotClientId => _config["Keycloak:SyncBotClientId"] ?? "umc-sync-bot";
    private string SyncBotClientSecret => _config["Keycloak:SyncBotClientSecret"] ?? "umc-sync-bot-secret-dev";

    /// <summary>是否启用同步（测试/CI 默认关闭）</summary>
    public bool Enabled => bool.TryParse(_config["Keycloak:SyncEnabled"], out var v) && v;

    // ─── 公共 API ───────────────────────────────────────────

    /// <summary>确保组路径存在，返回组 ID；失败返回 null</summary>
    public async Task<string?> EnsureGroupAsync(string groupPath, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        try
        {
            var http = await AuthedClientAsync(ct);
            return await EnsureGroupInternalAsync(http, groupPath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak 确保组存在失败 Path={Path}", groupPath);
            return null;
        }
    }

    /// <summary>把用户加入已有组；组或用户不存在时返回 false（不隐式创建）</summary>
    public async Task<bool> AddUserToGroupAsync(string userName, string groupPath, CancellationToken ct = default)
    {
        if (!Enabled) return false;
        try
        {
            var http = await AuthedClientAsync(ct);
            var userId = await FindUserIdAsync(http, userName, ct);
            if (userId is null)
            {
                _logger.LogWarning("Keycloak 加入组失败：找不到用户 UserName={UserName}", userName);
                return false;
            }
            var groupId = await FindGroupIdAsync(http, groupPath, ct);
            if (groupId is null)
            {
                _logger.LogWarning("Keycloak 加入组失败：找不到组 Path={Path}", groupPath);
                return false;
            }
            return await JoinAsync(http, userId, groupId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak 加入组异常 UserName={UserName} Path={Path}", userName, groupPath);
            return false;
        }
    }

    /// <summary>把用户移出组</summary>
    public async Task<bool> RemoveUserFromGroupAsync(string userName, string groupPath, CancellationToken ct = default)
    {
        if (!Enabled) return false;
        try
        {
            var http = await AuthedClientAsync(ct);
            var userId = await FindUserIdAsync(http, userName, ct);
            if (userId is null) return false;
            var groupId = await FindGroupIdAsync(http, groupPath, ct);
            if (groupId is null) return false;
            return await LeaveAsync(http, userId, groupId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak 移出组异常 UserName={UserName} Path={Path}", userName, groupPath);
            return false;
        }
    }

    /// <summary>读取用户所属的全部组路径（如 /tenants/t_a、/depts/tech）</summary>
    public async Task<List<string>> GetUserGroupPathsAsync(string userName, CancellationToken ct = default)
    {
        if (!Enabled) return new List<string>();
        try
        {
            var http = await AuthedClientAsync(ct);
            var userId = await FindUserIdAsync(http, userName, ct);
            return userId is null
                ? new List<string>()
                : await FetchUserGroupPathsAsync(http, userId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak 读取用户组异常 UserName={UserName}", userName);
            return new List<string>();
        }
    }

    /// <summary>
    /// reconcile：让用户在 <paramref name="managedPrefix"/> 下的组等于 <paramref name="desiredPaths"/>。
    /// 前缀外的组一律不动。
    /// </summary>
    public async Task<GroupSyncResult> SyncUserGroupsAsync(
        string userName,
        string managedPrefix,
        IEnumerable<string> desiredPaths,
        CancellationToken ct = default)
    {
        if (!Enabled)
            return new GroupSyncResult(true, new List<string>(), new List<string>());
        try
        {
            var http = await AuthedClientAsync(ct);
            var userId = await FindUserIdAsync(http, userName, ct);
            if (userId is null)
                return new GroupSyncResult(false, new List<string>(), new List<string>(), "用户不存在");

            var prefix = NormalizePath(managedPrefix);
            var desired = desiredPaths.Select(NormalizePath).Distinct().ToList();

            var current = await FetchUserGroupPathsAsync(http, userId, ct);
            var currentManaged = current.Where(p => IsManaged(p, prefix)).ToList();

            var toAdd = desired.Where(d => !current.Contains(d)).ToList();
            var toRemove = currentManaged.Where(p => !desired.Contains(p)).ToList();

            var added = new List<string>();
            var removed = new List<string>();

            foreach (var path in toAdd)
            {
                var groupId = await EnsureGroupInternalAsync(http, path, ct);
                if (groupId is null) continue;
                if (await JoinAsync(http, userId, groupId, ct)) added.Add(path);
            }

            foreach (var path in toRemove)
            {
                var groupId = await FindGroupIdAsync(http, path, ct);
                if (groupId is null) continue;
                if (await LeaveAsync(http, userId, groupId, ct)) removed.Add(path);
            }

            _logger.LogInformation(
                "Keycloak 用户组已同步 UserName={UserName} Prefix={Prefix} Added={Added} Removed={Removed}",
                userName, prefix, added.Count, removed.Count);

            return new GroupSyncResult(true, added, removed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak 用户组同步异常 UserName={UserName}", userName);
            return new GroupSyncResult(false, new List<string>(), new List<string>(), ex.Message);
        }
    }

    // ─── 内部实现 ───────────────────────────────────────────

    /// <summary>按层级确保组存在：逐段解析，缺失则创建（顶层 POST /groups，子级 POST /groups/{parent}/children）</summary>
    private async Task<string?> EnsureGroupInternalAsync(HttpClient http, string groupPath, CancellationToken ct)
    {
        var normalized = NormalizePath(groupPath);
        var segments = normalized.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        string? parentId = null;
        var prefix = string.Empty;

        foreach (var segment in segments)
        {
            prefix += "/" + segment;
            var tree = await FetchGroupTreeAsync(http, ct);
            if (tree.TryGetValue(prefix, out var existingId))
            {
                parentId = existingId;
                continue;
            }

            var created = parentId is null
                ? await CreateGroupAsync(http, segment, null, ct)
                : await CreateGroupAsync(http, segment, parentId, ct);
            if (!created) return null;

            // Keycloak 的 POST /groups 返回 201 + Location，不回 body，需重新拉取解析 id
            tree = await FetchGroupTreeAsync(http, ct);
            if (!tree.TryGetValue(prefix, out var newId))
            {
                _logger.LogWarning("Keycloak 组创建后未能解析出 ID Path={Path}", prefix);
                return null;
            }
            parentId = newId;
        }

        return parentId;
    }

    private async Task<bool> CreateGroupAsync(HttpClient http, string name, string? parentId, CancellationToken ct)
    {
        var url = parentId is null
            ? $"{BaseUrl}/admin/realms/{Realm}/groups"
            : $"{BaseUrl}/admin/realms/{Realm}/groups/{parentId}/children";
        var resp = await http.PostAsJsonAsync(url, new { name }, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Keycloak 组创建失败 Name={Name} ParentId={ParentId} Status={Status}",
                name, parentId, resp.StatusCode);
            return false;
        }
        return true;
    }

    /// <summary>拉取组树并展平为 path → id</summary>
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
            var path = group.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() : null;
            var id = group.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String
                ? i.GetString() : null;
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

    private async Task<string?> FindGroupIdAsync(HttpClient http, string groupPath, CancellationToken ct)
    {
        var tree = await FetchGroupTreeAsync(http, ct);
        return tree.TryGetValue(NormalizePath(groupPath), out var id) ? id : null;
    }

    private async Task<List<string>> FetchUserGroupPathsAsync(HttpClient http, string userId, CancellationToken ct)
    {
        var resp = await http.GetAsync(
            $"{BaseUrl}/admin/realms/{Realm}/users/{userId}/groups?briefRepresentation=true&max=1000", ct);
        if (!resp.IsSuccessStatusCode) return new List<string>();
        var arr = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var result = new List<string>();
        if (arr.ValueKind != JsonValueKind.Array) return result;
        foreach (var g in arr.EnumerateArray())
        {
            if (g.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String)
            {
                var path = p.GetString();
                if (!string.IsNullOrEmpty(path)) result.Add(path!);
            }
        }
        return result;
    }

    private async Task<bool> JoinAsync(HttpClient http, string userId, string groupId, CancellationToken ct)
    {
        var resp = await http.PutAsync(
            $"{BaseUrl}/admin/realms/{Realm}/users/{userId}/groups/{groupId}", null, ct);
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("Keycloak 加入组失败 UserId={UserId} GroupId={GroupId} Status={Status}",
                userId, groupId, resp.StatusCode);
        return resp.IsSuccessStatusCode;
    }

    private async Task<bool> LeaveAsync(HttpClient http, string userId, string groupId, CancellationToken ct)
    {
        var resp = await http.DeleteAsync(
            $"{BaseUrl}/admin/realms/{Realm}/users/{userId}/groups/{groupId}", ct);
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("Keycloak 移出组失败 UserId={UserId} GroupId={GroupId} Status={Status}",
                userId, groupId, resp.StatusCode);
        return resp.IsSuccessStatusCode;
    }

    private async Task<string?> FindUserIdAsync(HttpClient http, string userName, CancellationToken ct)
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
        var token = await GetAdminTokenAsync(ct);
        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    /// <summary>客户端凭据流拿 admin token，过期前 30s 复用</summary>
    public async Task<string> GetAdminTokenAsync(CancellationToken ct = default)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _cachedTokenExpiry.AddSeconds(-30))
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _cachedTokenExpiry.AddSeconds(-30))
                return _cachedToken;

            var http = _httpFactory.CreateClient();
            var resp = await http.PostAsync(
                $"{BaseUrl}/realms/{Realm}/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = SyncBotClientId,
                    ["client_secret"] = SyncBotClientSecret,
                }), ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var token = json.GetProperty("access_token").GetString()!;
            var expiresIn = json.GetProperty("expires_in").GetInt32();
            _cachedToken = token;
            _cachedTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    // ─── 路径工具 ───────────────────────────────────────────

    /// <summary>统一为 /a/b 形式（前导斜杠、去尾斜杠）</summary>
    internal static string NormalizePath(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        if (trimmed.Length == 0) return "/";
        if (!trimmed.StartsWith('/')) trimmed = "/" + trimmed;
        var noTrailing = trimmed.TrimEnd('/');
        return noTrailing.Length == 0 ? "/" : noTrailing;
    }

    /// <summary>按路径边界判断是否属于受管前缀：/tenants 匹配 /tenants 与 /tenants/a，但不匹配 /tenantsX</summary>
    internal static bool IsManaged(string path, string normalizedPrefix)
    {
        if (normalizedPrefix == "/") return true;
        return path.Equals(normalizedPrefix, StringComparison.Ordinal)
            || path.StartsWith(normalizedPrefix + "/", StringComparison.Ordinal);
    }
}
