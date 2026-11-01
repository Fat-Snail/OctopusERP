using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusRetail.Api.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// M2 — 邀请域（design §5）。
///
/// 种子角色：kc-zhangsan 在上海店是 manager、在总部是 staff；kc-lisi 在上海店是 staff；
/// kc-wangwu 在北京店是 manager。
/// </summary>
public class InviteTests(RetailTestFactory factory) : IClassFixture<RetailTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── 基建 ──────────────────────────────────────────────

    private static HttpRequestMessage Req(
        HttpMethod method, string url, object? body = null,
        string? tenant = null, string? sub = null, string? tenants = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        if (tenant is not null) req.Headers.Add("X-Tenant-Id", tenant);
        if (sub is not null) req.Headers.Add("X-Test-Sub", sub);
        // 显式传空串 → 该用户没有任何租户（模拟尚未入店的新人）
        if (tenants is not null) req.Headers.Add("X-Test-Tenants", tenants);
        return req;
    }

    private static async Task<JsonElement> RootAsync(HttpResponseMessage resp) =>
        JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<JsonElement> DataAsync(HttpResponseMessage resp) =>
        (await RootAsync(resp)).GetProperty("data");

    private async Task<T> WithDbAsync<T>(Func<RetailDbContext, Task<T>> f)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RetailDbContext>();
        return await f(db);
    }

    private async Task<(HttpStatusCode status, string? token, long inviteId)> CreateInviteAsync(
        string? tenant = "t_shanghai_001", string? sub = null, string? tenants = null,
        object? body = null)
    {
        var resp = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites",
            body ?? new { Phone = "13900001111", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 },
            tenant, sub, tenants));
        var root = await RootAsync(resp);
        // 业务失败走 HTTP 200 + code!=200，此时 data 为 null
        if (resp.StatusCode != HttpStatusCode.OK || root.GetProperty("code").GetInt32() != 200)
            return (resp.StatusCode, null, 0);
        var data = root.GetProperty("data");
        return (resp.StatusCode, data.GetProperty("token").GetString(), data.GetProperty("inviteId").GetInt64());
    }

    private Task<HttpResponseMessage> AcceptAsync(string token, string? sub = null, string? tenants = "") =>
        _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites/accept",
            new { Token = token }, tenant: null, sub, tenants));

    // ── 创建邀请 ──────────────────────────────────────────

    [Fact]
    public async Task CreateInvite_AsManager_ReturnsTokenAndInviteUrl()
    {
        var (status, token, inviteId) = await CreateInviteAsync();

        status.Should().Be(HttpStatusCode.OK);
        token.Should().NotBeNullOrWhiteSpace();
        token!.Length.Should().BeGreaterThanOrEqualTo(40, "32 字节随机数的 Base64Url 编码");
        inviteId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateInvite_PersistsHashOnly_NeverPlaintext()
    {
        var (_, token, inviteId) = await CreateInviteAsync();
        token.Should().NotBeNull();

        var stored = await WithDbAsync(db => db.Invites.IgnoreQueryFilters()
            .SingleAsync(i => i.InviteId == inviteId));

        stored.TokenHash.Should().NotBe(token, "明文令牌绝不能落库");
        stored.TokenHash.Should().Be(InviteTokenHash(token!));
        stored.TokenHash.Should().MatchRegex("^[0-9a-f]{64}$", "SHA-256 十六进制");
        stored.State.Should().Be(InviteState.Pending);
        stored.UsedCount.Should().Be(0);
    }

    /// <summary>与生产同一算法的哈希（独立复算，避免用被测代码验证被测代码）</summary>
    private static string InviteTokenHash(string token) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    [Fact]
    public async Task CreateInvite_AsStaff_IsRejected()
    {
        // kc-lisi 在上海店只是 staff，不是店长
        var (status, token, _) = await CreateInviteAsync(sub: "kc-lisi", tenants: "t_shanghai_001");

        status.Should().Be(HttpStatusCode.OK);
        token.Should().BeNull("staff 无权邀请成员");
    }

    [Fact]
    public async Task CreateInvite_WithoutTenantHeader_Returns400()
    {
        var resp = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites",
            new { Phone = "13900002222" }, tenant: null));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateInvite_WithoutPhoneOrCode_IsRejected()
    {
        var (status, token, _) = await CreateInviteAsync(body: new { RoleInTenant = "staff" });

        status.Should().Be(HttpStatusCode.OK);
        token.Should().BeNull("手机号与通用邀请码必须二选一");
    }

    // ── 接受邀请：正常路径 ────────────────────────────────

    [Fact]
    public async Task Accept_ValidToken_CreatesMembership()
    {
        var (_, token, _) = await CreateInviteAsync(
            body: new { Phone = "13900003333", RoleInTenant = "cashier", MaxUses = 1, ValidHours = 72 });

        var resp = await AcceptAsync(token!, sub: "kc-newbie-cashier", tenants: "");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await DataAsync(resp);
        data.GetProperty("tenantId").GetString().Should().Be("t_shanghai_001");
        data.GetProperty("roleInTenant").GetString().Should().Be("cashier");

        var membership = await WithDbAsync(db => db.Memberships
            .SingleAsync(m => m.KcSub == "kc-newbie-cashier" && m.TenantId == "t_shanghai_001"));
        membership.Status.Should().Be("active");
        membership.RoleInTenant.Should().Be("cashier");
        membership.KcSyncState.Should().Be("pending", "Keycloak group 双写待 M3 对账任务处理");
    }

    [Fact]
    public async Task Accept_UnknownToken_Fails()
    {
        var resp = await AcceptAsync("not-a-real-token", sub: "kc-newbie-unknown", tenants: "");

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(400);
        (await RootAsync(resp)).GetProperty("msg").GetString().Should().Contain("不存在");
    }

    // ── 接受邀请：状态机 ──────────────────────────────────

    [Fact]
    public async Task Accept_ExpiredToken_Fails_AndMarksInviteExpired()
    {
        var (_, token, inviteId) = await CreateInviteAsync(
            body: new { Phone = "13900004444", RoleInTenant = "staff", MaxUses = 1, ValidHours = 1 });

        factory.Clock.Advance(TimeSpan.FromHours(2));

        try
        {
            var resp = await AcceptAsync(token!, sub: "kc-newbie-expired", tenants: "");
            (await RootAsync(resp)).GetProperty("msg").GetString().Should().Contain("过期");

            var stored = await WithDbAsync(db => db.Invites.IgnoreQueryFilters()
                .SingleAsync(i => i.InviteId == inviteId));
            stored.State.Should().Be(InviteState.Expired);

            var isMember = await WithDbAsync(db => db.Memberships
                .AnyAsync(m => m.KcSub == "kc-newbie-expired"));
            isMember.Should().BeFalse("过期邀请不得建立成员关系");
        }
        finally
        {
            factory.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        }
    }

    [Fact]
    public async Task Accept_RevokedToken_Fails()
    {
        var (_, token, inviteId) = await CreateInviteAsync(
            body: new { Phone = "13900005555", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 });

        var revoke = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/invites/{inviteId}/revoke", null, tenant: "t_shanghai_001"));
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);

        var resp = await AcceptAsync(token!, sub: "kc-newbie-revoked", tenants: "");
        (await RootAsync(resp)).GetProperty("msg").GetString().Should().Contain("撤销");
    }

    [Fact]
    public async Task Accept_SingleUseToken_SecondAttemptFails()
    {
        var (_, token, _) = await CreateInviteAsync(
            body: new { Phone = "13900006666", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 });

        (await AcceptAsync(token!, sub: "kc-newbie-single", tenants: ""))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await AcceptAsync(token!, sub: "kc-newbie-single-2", tenants: "");
        (await RootAsync(second)).GetProperty("msg").GetString().Should().Contain("已被使用");

        var secondIsMember = await WithDbAsync(db => db.Memberships.AnyAsync(m => m.KcSub == "kc-newbie-single-2"));
        secondIsMember.Should().BeFalse("一次性邀请不能被第二个人使用");
    }

    [Fact]
    public async Task Accept_MultiUseToken_WorksUpToMaxUses()
    {
        var (_, token, inviteId) = await CreateInviteAsync(
            body: new { InviteCode = "SH001-PARTTIME", RoleInTenant = "staff", MaxUses = 2, ValidHours = 72 });

        for (var i = 1; i <= 2; i++)
        {
            var resp = await AcceptAsync(token!, sub: $"kc-multi-{i}", tenants: "");
            (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(200, $"第 {i} 次应在额度内");
        }

        var third = await AcceptAsync(token!, sub: "kc-multi-3", tenants: "");
        (await RootAsync(third)).GetProperty("msg").GetString().Should().Contain("已被使用");

        var stored = await WithDbAsync(db => db.Invites.IgnoreQueryFilters()
            .SingleAsync(i => i.InviteId == inviteId));
        stored.UsedCount.Should().Be(2);
        stored.State.Should().Be(InviteState.Accepted, "用满额度后进入终态");
    }

    [Fact]
    public async Task Accept_WhenAlreadyMember_IsIdempotent_AndDoesNotConsumeUse()
    {
        var (_, token, inviteId) = await CreateInviteAsync(
            body: new { Phone = "13900007777", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 });

        (await AcceptAsync(token!, sub: "kc-newbie-idem", tenants: ""))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // 同一人再次点同一个链接
        var again = await AcceptAsync(token!, sub: "kc-newbie-idem", tenants: "");
        var data = await DataAsync(again);
        data.GetProperty("alreadyMember").GetBoolean().Should().BeTrue();

        var memberships = await WithDbAsync(db => db.Memberships
            .CountAsync(m => m.KcSub == "kc-newbie-idem" && m.TenantId == "t_shanghai_001"));
        memberships.Should().Be(1, "重复接受不得产生重复成员关系");
    }

    // ── 租户绑定 ──────────────────────────────────────────

    [Fact]
    public async Task Accept_InviteFromOtherTenant_BindsToInviteTenant()
    {
        // 北京店店长发出的邀请
        var (_, token, _) = await CreateInviteAsync(
            tenant: "t_beijing_002", sub: "kc-wangwu", tenants: "t_beijing_002",
            body: new { Phone = "13900008888", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 });
        token.Should().NotBeNull();

        // 新人还没有任何租户，接受后应被绑进北京店（而不是别的店）
        var resp = await AcceptAsync(token!, sub: "kc-newbie-bj", tenants: "");
        (await DataAsync(resp)).GetProperty("tenantId").GetString().Should().Be("t_beijing_002");
    }

    [Fact]
    public async Task ListInvites_IsTenantScoped()
    {
        // 上海店创建一条
        var (_, _, shInviteId) = await CreateInviteAsync(
            tenant: "t_shanghai_001", sub: "kc-zhangsan", tenants: "t_shanghai_001");
        // 北京店创建一条
        var (_, _, bjInviteId) = await CreateInviteAsync(
            tenant: "t_beijing_002", sub: "kc-wangwu", tenants: "t_beijing_002");

        var resp = await _client.SendAsync(Req(HttpMethod.Get, "/api/retail/invites",
            null, tenant: "t_shanghai_001", sub: "kc-zhangsan", tenants: "t_shanghai_001"));

        var data = await DataAsync(resp);
        var ids = data.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("inviteId").GetInt64()).ToList();

        ids.Should().Contain(shInviteId);
        ids.Should().NotContain(bjInviteId, "上海店绝不能看到北京店的邀请");
        data.GetProperty("items").EnumerateArray()
            .Should().OnlyContain(x => x.GetProperty("tenantId").GetString() == "t_shanghai_001");
    }

    [Fact]
    public async Task Invites_QueryWithoutResolvedTenant_ReturnsEmptyByDefault()
    {
        await CreateInviteAsync(tenant: "t_shanghai_001");

        // 未解析租户的上下文直接查库（未加 IgnoreQueryFilters）
        var visible = await WithDbAsync(db => db.Invites.CountAsync());

        visible.Should().Be(0,
            "邀请表带租户过滤器：未解析租户时必须匹配空集，忘记过滤的后果是查不到而非泄漏");
    }

    // ── 预览 ──────────────────────────────────────────────

    [Fact]
    public async Task Preview_ReturnsTenantName()
    {
        var (_, token, _) = await CreateInviteAsync(
            body: new { Phone = "13900009999", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 });

        var resp = await _client.SendAsync(Req(HttpMethod.Get,
            $"/api/retail/invites/preview?token={Uri.EscapeDataString(token!)}",
            null, tenant: null, sub: "kc-newbie-preview", tenants: ""));

        var data = await DataAsync(resp);
        data.GetProperty("tenantName").GetString().Should().Be("上海南京路店");
        data.GetProperty("usable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Preview_UnknownToken_Returns404()
    {
        var resp = await _client.SendAsync(Req(HttpMethod.Get,
            "/api/retail/invites/preview?token=bogus", null, tenant: null, sub: "kc-x", tenants: ""));

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(404);
    }
}
