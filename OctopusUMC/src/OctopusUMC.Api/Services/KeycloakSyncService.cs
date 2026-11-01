using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Services;

/// <summary>
/// P9-补 M2：UMC → Keycloak 单向用户同步
///
/// 用 Service Account（umc-sync-bot client）的 client_credentials 拿 admin token，
/// 调 Keycloak Admin REST API 把 UMC 用户增删改/状态/密码变更推到 Keycloak。
///
/// 设计要点：
/// - admin token 缓存（默认 60s 过期前刷新），避免每次同步都打 token 端点
/// - 调用失败只记日志不抛异常（最终一致性，不阻塞 UMC 主流程）
/// - 同步字段：基础信息 + 自定义 attributes（octopus_user_id / primary_dept_* / company_name / miniapp_scope）
/// - 部门/公司/scope 通过 ApplicationDbContext 内查询出快照
///
/// 配置项（Aspire 注入或 appsettings.json）：
///   services:keycloak:http:0       Aspire 注入的 Keycloak 基址，如 http://localhost:8088
///   Keycloak:Realm                 octopus
///   Keycloak:SyncBotClientId       umc-sync-bot
///   Keycloak:SyncBotClientSecret   umc-sync-bot-secret-dev
/// </summary>
public class KeycloakSyncService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<KeycloakSyncService> _logger;
    private readonly ApplicationDbContext _db;

    private static string? _cachedToken;
    private static DateTime _cachedTokenExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    public KeycloakSyncService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<KeycloakSyncService> logger,
        ApplicationDbContext db)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
        _db = db;
    }

    private string BaseUrl =>
        // ⚠️ 优先 https：Aspire 的 AddKeycloak(port: 8088) 把 8088 映射到容器 8443(HTTPS)，
        // 用 http://localhost:8088 会得到 "The response ended prematurely"（明文 HTTP
        // 打到 HTTPS 端口，服务端直接断连），且极难从堆栈看出根因。
        _config["services:keycloak:https:0"]
        ?? _config["services:keycloak:http:0"]   // Aspire 注入（旧版本只有 http）
        ?? _config["Keycloak:BaseUrl"]           // 生产 fallback
        ?? "https://localhost:8088";

    private string Realm => _config["Keycloak:Realm"] ?? "octopus";
    private string SyncBotClientId => _config["Keycloak:SyncBotClientId"] ?? "umc-sync-bot";
    private string SyncBotClientSecret => _config["Keycloak:SyncBotClientSecret"] ?? "umc-sync-bot-secret-dev";

    /// <summary>
    /// 是否启用同步（通过配置控制；测试环境/CI 默认关闭，生产/开发环境开启）
    /// </summary>
    public bool Enabled => bool.TryParse(_config["Keycloak:SyncEnabled"], out var v) && v;

    /// <summary>同步单个用户到 Keycloak（upsert + attributes）— 失败只记日志不抛</summary>
    public async Task SyncUserAsync(User user, CancellationToken ct = default)
    {
        if (!Enabled)
        {
            _logger.LogDebug("Keycloak sync disabled, skip UserId={UserId}", user.UserId);
            return;
        }

        try
        {
            var token = await GetAdminTokenAsync(ct);
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var attrs = BuildUserAttributes(user);
            var payload = new
            {
                username = user.UserName,
                email = user.Email,
                // ⚠️ Keycloak 对 firstName 有 person-name 校验（只接受字母/空格/连字符/撇号），
                // 而 UMC 昵称可以带括号等任意字符。实测 "赵六(海星技术)" 会得到
                // error-person-name-invalid-character / HTTP 400。
                firstName = SanitizePersonName(user.NickName, user.UserName),
                enabled = user.Status == 1,
                emailVerified = !string.IsNullOrEmpty(user.Email),
                attributes = attrs,
            };

            // 1) 查询是否已存在
            var existing = await FindByUsernameAsync(http, user.UserName, ct);
            if (existing is null)
            {
                // 新建
                var createResp = await http.PostAsJsonAsync(
                    $"{BaseUrl}/admin/realms/{Realm}/users",
                    payload, ct);
                if (!createResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Keycloak 用户创建失败 UserId={UserId} Status={Status} Body={Body}",
                        user.UserId, createResp.StatusCode, await createResp.Content.ReadAsStringAsync(ct));
                    return;
                }
                _logger.LogInformation("Keycloak 用户已创建 UserId={UserId} UserName={UserName}", user.UserId, user.UserName);

                // 回写 Keycloak sub：零售域与审计需要 UMC 用户 ↔ Keycloak 用户的对应关系
                var createdId = createResp.Headers.Location?.ToString()
                    .Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (!string.IsNullOrEmpty(createdId))
                    await SaveKcSubAsync(user, createdId, ct);
            }
            else
            {
                // 更新
                var kcId = existing;
                if (user.KcSub != kcId) await SaveKcSubAsync(user, kcId, ct);
                var updateResp = await http.PutAsJsonAsync(
                    $"{BaseUrl}/admin/realms/{Realm}/users/{kcId}",
                    payload, ct);
                if (!updateResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Keycloak 用户更新失败 UserId={UserId} KcId={KcId} Status={Status}",
                        user.UserId, kcId, updateResp.StatusCode);
                    return;
                }
                _logger.LogInformation("Keycloak 用户已更新 UserId={UserId} KcId={KcId}", user.UserId, kcId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak 同步异常 UserId={UserId}", user.UserId);
        }
    }

    /// <summary>重置 Keycloak 用户密码（不暂存为临时密码）</summary>
    public async Task ResetPasswordAsync(long umcUserId, string userName, string newPassword, CancellationToken ct = default)
    {
        if (!Enabled) return;
        try
        {
            var token = await GetAdminTokenAsync(ct);
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var kcId = await FindByUsernameAsync(http, userName, ct);
            if (kcId is null)
            {
                _logger.LogWarning("Keycloak 重置密码失败：找不到用户 UserName={UserName}", userName);
                return;
            }

            var resp = await http.PutAsJsonAsync(
                $"{BaseUrl}/admin/realms/{Realm}/users/{kcId}/reset-password",
                new { type = "password", value = newPassword, temporary = false }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Keycloak 密码重置失败 UserId={UserId} Status={Status}",
                    umcUserId, resp.StatusCode);
            }
            else
            {
                _logger.LogInformation("Keycloak 密码已重置 UserId={UserId}", umcUserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak 重置密码异常 UserId={UserId}", umcUserId);
        }
    }

    /// <summary>删除 Keycloak 用户（推荐用 SyncUserAsync + Status=0 软删，硬删仅在 UMC 物理删除时调用）</summary>
    public async Task DeleteUserAsync(string userName, CancellationToken ct = default)
    {
        if (!Enabled) return;
        try
        {
            var token = await GetAdminTokenAsync(ct);
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var kcId = await FindByUsernameAsync(http, userName, ct);
            if (kcId is null)
            {
                _logger.LogDebug("Keycloak 删除跳过：用户不存在 UserName={UserName}", userName);
                return;
            }

            var resp = await http.DeleteAsync(
                $"{BaseUrl}/admin/realms/{Realm}/users/{kcId}", ct);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Keycloak 删除失败 UserName={UserName} Status={Status}", userName, resp.StatusCode);
            else
                _logger.LogInformation("Keycloak 用户已删除 UserName={UserName}", userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak 删除异常 UserName={UserName}", userName);
        }
    }

    // ─── 私有辅助 ───────────────────────────────────────────

    /// <summary>查找用户 Keycloak ID（按 username 精确匹配）</summary>
    /// <summary>
    /// 回写 Keycloak sub。
    /// 用 ExecuteUpdate 而不是 SaveChanges：回填 worker 传进来的是 AsNoTracking 实体，
    /// 直接改属性不会被持久化。
    /// </summary>
    private async Task SaveKcSubAsync(User user, string kcSub, CancellationToken ct)
    {
        user.KcSub = kcSub;
        try
        {
            await _db.Users.Where(u => u.UserId == user.UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.KcSub, kcSub), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "回写 Keycloak sub 失败 UserId={UserId}", user.UserId);
        }
    }

    private async Task<string?> FindByUsernameAsync(HttpClient http, string userName, CancellationToken ct)
    {
        var resp = await http.GetAsync(
            $"{BaseUrl}/admin/realms/{Realm}/users?username={Uri.EscapeDataString(userName)}&exact=true",
            ct);
        if (!resp.IsSuccessStatusCode) return null;
        var arr = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;
        return arr[0].GetProperty("id").GetString();
    }

    /// <summary>客户端凭据流拿 admin token，60s 内有效则复用</summary>
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

    /// <summary>从 DB 关联表查询用户的主部门 + 公司 + 推断 miniapp_scope</summary>
    private Dictionary<string, string[]> BuildUserAttributes(User user)
    {
        var attrs = new Dictionary<string, string[]>
        {
            ["octopus_user_id"] = new[] { user.UserId.ToString() },
            // 原文昵称（可能含括号等字符，不能放进 firstName）单独保留
            ["nickname"] = new[] { user.NickName },
        };

        // M0：多值 dept_ids（全部部门）。
        // 此前只同步 IsPrimary 一条，导致「一人多部门 / 跨公司兼职」在 token 层被静默丢弃
        // —— UserDept 本身是多对多，zhangsan 这类主/兼职用户在种子里就已存在。
        var allDeptIds = _db.UserDepts
            .Where(ud => ud.UserId == user.UserId)
            .Select(ud => ud.DeptId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        if (allDeptIds.Count > 0)
            attrs["dept_ids"] = allDeptIds.Select(id => id.ToString()).ToArray();

        var primaryUd = _db.UserDepts
            .Where(ud => ud.UserId == user.UserId && ud.IsPrimary)
            .FirstOrDefault();

        if (primaryUd is not null)
        {
            attrs["primary_dept_id"] = new[] { primaryUd.DeptId.ToString() };
            var dept = _db.Depts.FirstOrDefault(d => d.DeptId == primaryUd.DeptId);
            if (dept is not null)
            {
                attrs["primary_dept_name"] = new[] { dept.DeptName };
                // 沿父链找根节点（公司）
                var company = FindRootDept(dept);
                if (company is not null)
                    attrs["company_name"] = new[] { company.DeptName };
            }
        }

        // miniapp_scope 默认 personal；管理员（用户 ID=1 或 admin）= all
        attrs["miniapp_scope"] = new[]
        {
            user.UserId == 1 || user.UserName == "admin" ? "all" : "personal",
        };

        return attrs;
    }

    /// <summary>
    /// 清洗为 Keycloak 能接受的 person name。
    /// 只保留字母（含中文）、空格、连字符、撇号；括号、数字等一律剔除。
    /// 结果为空时回退到用户名，避免 firstName 为空。
    /// </summary>
    private static string SanitizePersonName(string? name, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback ?? string.Empty;

        var kept = new string(name
            .Where(c => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'')
            .ToArray())
            .Trim();

        return string.IsNullOrEmpty(kept) ? (fallback ?? string.Empty) : kept;
    }

    private Dept? FindRootDept(Dept dept)
    {
        var current = dept;
        var depth = 0;
        while (current is not null && current.ParentId != 0 && depth < 20)
        {
            current = _db.Depts.FirstOrDefault(d => d.DeptId == current.ParentId);
            depth++;
        }
        return current;
    }
}
