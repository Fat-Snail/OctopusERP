using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OctopusUMC.Api.Services;
using OctopusUMC.Core.Domain.Entities;
using OctopusUMC.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// P9 M2 — KeycloakSyncService 单元测试
/// 用自定义 HttpMessageHandler 截获 Keycloak Admin REST 调用，验证：
/// - SyncEnabled=false 时静默 skip
/// - 用户不存在 → POST /users
/// - 用户存在 → PUT /users/{id}
/// - admin token 缓存（多次调用只打一次 token 端点）
/// - 网络异常时不抛、不阻塞主流程
/// - 自定义 attributes（octopus_user_id / primary_dept / company / scope）正确注入 payload
/// </summary>
public class KeycloakSyncServiceTests
{
    private static IConfiguration BuildConfig(bool syncEnabled = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:BaseUrl"] = "http://fake-kc",
            ["Keycloak:Realm"] = "octopus",
            ["Keycloak:SyncBotClientId"] = "umc-sync-bot",
            ["Keycloak:SyncBotClientSecret"] = "secret",
            ["Keycloak:SyncEnabled"] = syncEnabled ? "true" : "false",
        }!).Build();

    private static ApplicationDbContext NewDb(out SqliteContext kept)
    {
        // 用内存 SQLite，保持连接生命周期跟测试一致
        var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:;Cache=Shared");
        conn.Open();
        var opt = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(conn).Options;
        var db = new ApplicationDbContext(opt);
        db.Database.EnsureCreated();
        kept = new SqliteContext(conn);
        return db;
    }

    /// <summary>持有 SQLite 连接生命周期</summary>
    public class SqliteContext : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _c;
        public SqliteContext(Microsoft.Data.Sqlite.SqliteConnection c) => _c = c;
        public void Dispose() => _c.Dispose();
    }

    /// <summary>截获所有 HTTP 调用记录请求 + 按路径返回预设响应</summary>
    private class FakeHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> RequestBodies { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }
        public int TokenCallCount;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));

            if (request.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token"))
            {
                TokenCallCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { access_token = "fake-jwt", expires_in = 300 }),
                };
            }
            return Responder?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>重置 KeycloakSyncService 内部静态 token 缓存（每个测试独立）</summary>
    private static void ResetTokenCache()
    {
        var t = typeof(KeycloakSyncService);
        t.GetField("_cachedToken", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, null);
        t.GetField("_cachedTokenExpiry", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, DateTime.MinValue);
    }

    [Fact]
    public async Task SyncUserAsync_Disabled_SkipsAllCalls()
    {
        ResetTokenCache();
        var handler = new FakeHandler();
        using var dbCtx = NewDb(out var keep);
        using var _ = keep;
        var svc = new KeycloakSyncService(new FakeFactory(handler), BuildConfig(syncEnabled: false),
            NullLogger<KeycloakSyncService>.Instance, dbCtx);

        await svc.SyncUserAsync(new User { UserId = 1, UserName = "admin" });

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncUserAsync_NewUser_PostsCreatePayload()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                // 查询返回空数组 → 触发创建
                if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/users"))
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") };
                if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users"))
                    return new HttpResponseMessage(HttpStatusCode.Created);
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        using var db = NewDb(out var keep);
        using var _ = keep;
        var svc = new KeycloakSyncService(new FakeFactory(handler), BuildConfig(),
            NullLogger<KeycloakSyncService>.Instance, db);

        await svc.SyncUserAsync(new User
        {
            UserId = 99,
            UserName = "newuser",
            NickName = "新用户",
            Email = "newuser@test.com",
            Status = 1,
        });

        handler.TokenCallCount.Should().Be(1);

        // 应该有：1 个 token + 1 个 GET users?username= + 1 个 POST /users
        var post = handler.Requests.FirstOrDefault(r =>
            r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/users"));
        post.Should().NotBeNull();

        var idx = handler.Requests.IndexOf(post!);
        var body = handler.RequestBodies[idx];
        body.Should().Contain("\"username\":\"newuser\"");
        body.Should().Contain("\"enabled\":true");
        body.Should().Contain("octopus_user_id");
        body.Should().Contain("\"99\"");
        body.Should().Contain("miniapp_scope");
    }

    [Fact]
    public async Task SyncUserAsync_ExistingUser_PutsUpdate()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/users"))
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[{\"id\":\"kc-uuid-100\",\"username\":\"zhangsan\"}]",
                            System.Text.Encoding.UTF8, "application/json")
                    };
                if (req.Method == HttpMethod.Put && req.RequestUri!.AbsolutePath.Contains("/users/kc-uuid-100"))
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        using var db = NewDb(out var keep);
        using var _ = keep;
        var svc = new KeycloakSyncService(new FakeFactory(handler), BuildConfig(),
            NullLogger<KeycloakSyncService>.Instance, db);

        await svc.SyncUserAsync(new User { UserId = 2, UserName = "zhangsan", Status = 0 });

        var put = handler.Requests.FirstOrDefault(r =>
            r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath.Contains("/users/kc-uuid-100"));
        put.Should().NotBeNull("应调 PUT 到具体 kcId");

        var idx = handler.Requests.IndexOf(put!);
        handler.RequestBodies[idx].Should().Contain("\"enabled\":false");  // Status=0 → enabled=false
    }

    [Fact]
    public async Task GetAdminTokenAsync_CachesToken_OnlyHitsTokenEndpointOnce()
    {
        ResetTokenCache();
        var handler = new FakeHandler();
        using var db = NewDb(out var keep);
        using var _ = keep;
        var svc = new KeycloakSyncService(new FakeFactory(handler), BuildConfig(),
            NullLogger<KeycloakSyncService>.Instance, db);

        var t1 = await svc.GetAdminTokenAsync();
        var t2 = await svc.GetAdminTokenAsync();
        var t3 = await svc.GetAdminTokenAsync();

        t1.Should().Be(t2).And.Be(t3);
        handler.TokenCallCount.Should().Be(1, "应缓存 token，多次调用只打一次端点");
    }

    [Fact]
    public async Task SyncUserAsync_OnHttpFailure_DoesNotThrow()
    {
        ResetTokenCache();
        var handler = new FakeHandler
        {
            Responder = _ => throw new HttpRequestException("network down")
        };
        using var db = NewDb(out var keep);
        using var _ = keep;
        var svc = new KeycloakSyncService(new FakeFactory(handler), BuildConfig(),
            NullLogger<KeycloakSyncService>.Instance, db);

        // 应吃掉异常，不传播
        var act = async () => await svc.SyncUserAsync(new User { UserId = 1, UserName = "x" });
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_CallsResetEndpoint()
    {
        ResetTokenCache();
        string? putPath = null;
        string? putBody = null;
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/users"))
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[{\"id\":\"kc-200\",\"username\":\"zhangsan\"}]",
                            System.Text.Encoding.UTF8, "application/json")
                    };
                if (req.Method == HttpMethod.Put && req.RequestUri!.AbsolutePath.Contains("reset-password"))
                {
                    putPath = req.RequestUri.AbsolutePath;
                    putBody = req.Content!.ReadAsStringAsync().Result;
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        using var db = NewDb(out var keep);
        using var _ = keep;
        var svc = new KeycloakSyncService(new FakeFactory(handler), BuildConfig(),
            NullLogger<KeycloakSyncService>.Instance, db);

        await svc.ResetPasswordAsync(2, "zhangsan", "NewPwd@123");

        putPath.Should().NotBeNull().And.EndWith("/users/kc-200/reset-password");
        putBody.Should().Contain("\"value\":\"NewPwd@123\"");
        putBody.Should().Contain("\"temporary\":false");
    }

    // ─── M0：多值 dept_ids（修复「一人多部门被静默丢弃」） ─────────

    /// <summary>把 user + dept + userDept 写进内存库，用于验证多部门场景</summary>
    private static void SeedUserDepts(ApplicationDbContext db, long userId, params long[] deptIds)
    {
        db.Depts.AddRange(deptIds.Select((d, i) => new Dept
        {
            DeptId = d,
            ParentId = 0,
            DeptName = $"部门{d}",
            OrderNum = i,
            Status = 1,
        }));
        db.SaveChanges();

        db.UserDepts.AddRange(deptIds.Select((d, i) => new UserDept
        {
            UserId = userId,
            DeptId = d,
            IsPrimary = i == 0,
        }));
        db.SaveChanges();
    }

    /// <summary>跑一次 SyncUserAsync，返回创建/更新 payload 的 attributes 节点</summary>
    private static async Task<JsonElement> CaptureAttributesAsync(ApplicationDbContext db, User user)
    {
        ResetTokenCache();
        string? body = null;
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/users"))
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") };
                if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users"))
                {
                    body = req.Content!.ReadAsStringAsync().Result;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        var svc = new KeycloakSyncService(new FakeFactory(handler), BuildConfig(),
            NullLogger<KeycloakSyncService>.Instance, db);

        await svc.SyncUserAsync(user);

        body.Should().NotBeNull("应发出用户创建请求");
        var doc = JsonDocument.Parse(body!);
        return doc.RootElement.GetProperty("attributes").Clone();
    }

    [Fact]
    public async Task SyncUserAsync_UserWithTwoDepts_EmitsAllDeptIds()
    {
        using var db = NewDb(out var keep);
        using var keepAlive = keep;
        // zhangsan 式的一人多部门（主部门 3 + 兼职部门 10）
        SeedUserDepts(db, 2, 3, 10);

        var attrs = await CaptureAttributesAsync(db, new User
        {
            UserId = 2, UserName = "zhangsan", Email = "zhangsan@test.com", Status = 1,
        });

        attrs.TryGetProperty("dept_ids", out var deptIds).Should().BeTrue(
            "多部门必须进入 dept_ids（此前只同步 IsPrimary 一条，兼职部门被静默丢弃）");
        var values = deptIds.EnumerateArray().Select(x => x.GetString()).ToList();
        values.Should().BeEquivalentTo(new[] { "3", "10" });

        // 向后兼容：primary_dept_id 仍保留
        attrs.GetProperty("primary_dept_id")[0].GetString().Should().Be("3");
    }

    [Fact]
    public async Task SyncUserAsync_UserWithNoDept_OmitsDeptIds()
    {
        using var db = NewDb(out var keep);
        using var keepAlive = keep;

        var attrs = await CaptureAttributesAsync(db, new User
        {
            UserId = 77, UserName = "nodept", Email = "nodept@test.com", Status = 1,
        });

        attrs.TryGetProperty("dept_ids", out _).Should().BeFalse("无部门时不应写入空属性");
        attrs.TryGetProperty("primary_dept_id", out _).Should().BeFalse();
    }

    // ─── Keycloak person-name 校验（实测踩坑） ───────────────────

    [Fact]
    public async Task SyncUserAsync_NickNameWithParentheses_SanitizesFirstName()
    {
        // Keycloak 的 firstName 只接受字母/空格/连字符/撇号，
        // "赵六(海星技术)" 会被 400 error-person-name-invalid-character 拒绝。
        ResetTokenCache();
        string? body = null;
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/users"))
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") };
                if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users"))
                {
                    body = req.Content!.ReadAsStringAsync().Result;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        using var db = NewDb(out var keep);
        using var _ = keep;
        var svc = new KeycloakSyncService(new FakeFactory(handler), BuildConfig(),
            NullLogger<KeycloakSyncService>.Instance, db);

        await svc.SyncUserAsync(new User
        {
            UserId = 6, UserName = "zhaoliu", NickName = "赵六(海星技术)",
            Email = "zhaoliu@haixing.com", Status = 1,
        });

        body.Should().NotBeNull();
        var root = JsonDocument.Parse(body!).RootElement;

        var firstName = root.GetProperty("firstName").GetString();
        firstName.Should().NotContain("(").And.NotContain(")");
        firstName.Should().Contain("赵六", "清洗只去掉非法字符，不能把名字整体丢掉");

        // 原始昵称要保留在 attributes 里，避免信息丢失
        var attrs = root.GetProperty("attributes");
        attrs.GetProperty("nickname")[0].GetString().Should().Be("赵六(海星技术)");
    }

    [Fact]
    public async Task SyncUserAsync_NickNameAllSpecialChars_FallsBackToUserName()
    {
        ResetTokenCache();
        string? body = null;
        var handler = new FakeHandler
        {
            Responder = req =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.Contains("/users"))
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") };
                if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users"))
                {
                    body = req.Content!.ReadAsStringAsync().Result;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };
        using var db = NewDb(out var keep);
        using var keepAlive = keep;
        var svc = new KeycloakSyncService(new FakeFactory(handler), BuildConfig(),
            NullLogger<KeycloakSyncService>.Instance, db);

        await svc.SyncUserAsync(new User
        {
            UserId = 7, UserName = "weird", NickName = "()[]{}",
            Email = "w@x.com", Status = 1,
        });

        var root = JsonDocument.Parse(body!).RootElement;
        root.GetProperty("firstName").GetString().Should().NotBeNullOrEmpty(
            "清洗后不能留空，否则 Keycloak 侧显示名是空白");
    }
}
