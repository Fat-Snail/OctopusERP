using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusRetail.Api.Persistence;
using OctopusRetail.Api.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// 短邀请链接（/i/{code}）。
///
/// 背景：原来的邀请链接把 43 字符的 base64url token 放在 query 里，
/// 又长又难口头传达。现在额外生成 8 位短码（去掉易混字符 0/O/1/l/I/5/S），
/// 并**仍然只存哈希**。
/// </summary>
public class ShortCodeTests(RetailTestFactory factory) : IClassFixture<RetailTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static HttpRequestMessage Req(
        HttpMethod method, string url, object? body = null,
        string? tenant = null, string? sub = null, string? tenants = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        if (tenant is not null) req.Headers.Add("X-Tenant-Id", tenant);
        if (sub is not null) req.Headers.Add("X-Test-Sub", sub);
        if (tenants is not null) req.Headers.Add("X-Test-Tenants", tenants);
        return req;
    }

    private static async Task<JsonElement> RootAsync(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<T> WithDbAsync<T>(Func<RetailDbContext, Task<T>> f)
    {
        using var scope = factory.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<RetailDbContext>());
    }

    /// <summary>店长（kc-zhangsan）在上海店建一条邀请，返回短码与长 token</summary>
    private async Task<(string shortCode, string token, long inviteId)> CreateAsync()
    {
        var resp = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites",
            new { InviteCode = "SHORT-TEST", RoleInTenant = "staff", MaxUses = 5, ValidHours = 72 },
            tenant: "t_shanghai_001", sub: "kc-zhangsan", tenants: "t_shanghai_001"));

        var root = await RootAsync(resp);
        root.GetProperty("code").GetInt32().Should().Be(200, root.GetProperty("msg").GetString());

        var data = root.GetProperty("data");
        return (
            data.GetProperty("shortCode").GetString()!,
            data.GetProperty("token").GetString()!,
            data.GetProperty("inviteId").GetInt64());
    }

    // ── 短码形态 ──────────────────────────────────────────

    [Fact]
    public async Task Create_ReturnsEightCharShortCodeAndShortUrl()
    {
        var (code, _, _) = await CreateAsync();

        code.Should().HaveLength(InviteToken.ShortCodeLength);
        // 从字符表推导，不手写正则——手写副本会与实现悄悄漂移（这里就踩过）
        code.All(c => InviteToken.ShortCodeAlphabet.Contains(c)).Should().BeTrue(
            "短码的每个字符都必须来自实现所用的字符表");

        // 短链接可直接打开落地页
        var resp = await _client.SendAsync(Req(HttpMethod.Get,
            $"/api/retail/invites/preview?code={code}", tenant: null, sub: "kc-brand-new", tenants: ""));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_StoresOnlyHashOfShortCode()
    {
        var (code, _, inviteId) = await CreateAsync();

        var stored = await WithDbAsync(db => db.Invites.IgnoreQueryFilters()
            .SingleAsync(i => i.InviteId == inviteId));

        stored.ShortCodeHash.Should().NotBeNull();
        stored.ShortCodeHash.Should().NotBe(code, "短码明文绝不能落库");
        stored.ShortCodeHash.Should().MatchRegex("^[0-9a-f]{64}$", "SHA-256 hex");
        stored.ShortCodeHash.Should().Be(InviteToken.Hash(code));
    }

    [Fact]
    public async Task Create_ShortCodesAreUniqueAcrossInvites()
    {
        var codes = new List<string>();
        for (var i = 0; i < 5; i++)
            codes.Add((await CreateAsync()).shortCode);

        codes.Distinct().Should().HaveCount(5, "短码应互不相同");
    }

    // ── 短码 / 长 token 双通道 ───────────────────────────

    [Fact]
    public async Task Preview_ByShortCode_AndByToken_ResolveSameInvite()
    {
        var (code, token, _) = await CreateAsync();

        var byCode = await RootAsync(await _client.SendAsync(Req(HttpMethod.Get,
            $"/api/retail/invites/preview?code={code}", tenant: null, sub: "kc-p1", tenants: "")));
        var byToken = await RootAsync(await _client.SendAsync(Req(HttpMethod.Get,
            $"/api/retail/invites/preview?token={token}", tenant: null, sub: "kc-p2", tenants: "")));

        var a = byCode.GetProperty("data");
        var b = byToken.GetProperty("data");
        a.GetProperty("tenantId").GetString().Should().Be(b.GetProperty("tenantId").GetString());
        a.GetProperty("roleInTenant").GetString().Should().Be(b.GetProperty("roleInTenant").GetString());
        a.GetProperty("usable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Accept_ByShortCode_CreatesMembership()
    {
        var (code, _, _) = await CreateAsync();

        var resp = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites/accept",
            new { Code = code }, tenant: null, sub: "kc-short-accept", tenants: ""));

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(200);
        (await RootAsync(resp)).GetProperty("data").GetProperty("tenantId").GetString()
            .Should().Be("t_shanghai_001");

        var m = await WithDbAsync(db => db.Memberships.IgnoreQueryFilters()
            .SingleAsync(x => x.KcSub == "kc-short-accept"));
        m.TenantId.Should().Be("t_shanghai_001");
    }

    [Fact]
    public async Task Accept_ByShortCode_RespectsSingleUse()
    {
        // 单独建一条 maxUses=1 的邀请
        var resp = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites",
            new { Phone = "13700009999", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 },
            tenant: "t_shanghai_001", sub: "kc-zhangsan", tenants: "t_shanghai_001"));
        var code = (await RootAsync(resp)).GetProperty("data").GetProperty("shortCode").GetString()!;

        var first = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites/accept",
            new { Code = code }, tenant: null, sub: "kc-once-1", tenants: ""));
        (await RootAsync(first)).GetProperty("code").GetInt32().Should().Be(200);

        var second = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites/accept",
            new { Code = code }, tenant: null, sub: "kc-once-2", tenants: ""));
        (await RootAsync(second)).GetProperty("msg").GetString().Should().Contain("已被使用");
    }

    [Fact]
    public async Task Preview_UnknownShortCode_Returns404()
    {
        var resp = await _client.SendAsync(Req(HttpMethod.Get,
            "/api/retail/invites/preview?code=ZZZZZZZZ", tenant: null, sub: "kc-x", tenants: ""));

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(404);
    }

    [Fact]
    public async Task Preview_WithoutCredential_Returns400()
    {
        var resp = await _client.SendAsync(Req(HttpMethod.Get,
            "/api/retail/invites/preview", tenant: null, sub: "kc-x", tenants: ""));

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public void ShortCodeAlphabet_ExcludesAmbiguousCharacters()
    {
        // 0/O、1/l/I、5/S 形近，口头或短信传达时极易出错，必须从字符表里剔除
        foreach (var bad in "0O1lI5S")
            InviteToken.ShortCodeAlphabet.Should().NotContain(bad.ToString());

        InviteToken.ShortCodeAlphabet.Length.Should().BeGreaterThanOrEqualTo(50,
            "字符表太小会显著降低短码熵值");
    }

    [Fact]
    public void GenerateShortCode_ProducesHighEntropyCodes()
    {
        var generated = Enumerable.Range(0, 500).Select(_ => InviteToken.GenerateShortCode()).ToList();

        generated.Should().OnlyContain(c => c.Length == InviteToken.ShortCodeLength);
        generated.Should().OnlyContain(
            c => c.All(ch => InviteToken.ShortCodeAlphabet.Contains(ch)));
        generated.Distinct().Should().HaveCountGreaterThan(490, "短码必须高熵、不可预测");
    }

    // ── 已是成员时不应显示"邀请已失效" ───────────────────

    [Fact]
    public async Task Preview_WhenCallerIsAlreadyMember_ReportsAlreadyMember()
    {
        var (code, _, _) = await CreateAsync();

        // 以 kc-already 身份接受
        var accepted = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites/accept",
            new { Code = code }, tenant: null, sub: "kc-already", tenants: ""));
        (await RootAsync(accepted)).GetProperty("code").GetInt32().Should().Be(200);

        // 再次预览（模拟注册后登录回到邀请页）
        var preview = await RootAsync(await _client.SendAsync(Req(HttpMethod.Get,
            $"/api/retail/invites/preview?code={code}", tenant: null, sub: "kc-already", tenants: "")));
        var d = preview.GetProperty("data");

        d.GetProperty("alreadyMember").GetBoolean().Should().BeTrue(
            "已经是成员的人再打开邀请页，应显示「你已加入」而不是「邀请已失效」");
        d.GetProperty("myRole").GetString().Should().Be("staff");
    }

    [Fact]
    public async Task Preview_WhenCallerIsNotMember_ReportsNotAlreadyMember()
    {
        var (code, _, _) = await CreateAsync();

        var preview = await RootAsync(await _client.SendAsync(Req(HttpMethod.Get,
            $"/api/retail/invites/preview?code={code}", tenant: null, sub: "kc-outsider", tenants: "")));

        preview.GetProperty("data").GetProperty("alreadyMember").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Preview_UsedUpSingleUseInvite_StillReportsAlreadyMember()
    {
        // 单次邀请：这正是"注册后登录回到邀请页"时最常见的情形
        var created = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites",
            new { Phone = "13700008888", RoleInTenant = "staff", MaxUses = 1, ValidHours = 72 },
            tenant: "t_shanghai_001", sub: "kc-zhangsan", tenants: "t_shanghai_001"));
        var code = (await RootAsync(created)).GetProperty("data").GetProperty("shortCode").GetString()!;

        var accepted = await _client.SendAsync(Req(HttpMethod.Post, "/api/retail/invites/accept",
            new { Code = code }, tenant: null, sub: "kc-joined-one", tenants: ""));
        (await RootAsync(accepted)).GetProperty("code").GetInt32().Should().Be(200);

        // 同一个人再看：邀请已用尽（usable=false），但仍应表明"你是成员"
        var mine = (await RootAsync(await _client.SendAsync(Req(HttpMethod.Get,
            $"/api/retail/invites/preview?code={code}", tenant: null, sub: "kc-joined-one", tenants: ""))))
            .GetProperty("data");
        mine.GetProperty("usable").GetBoolean().Should().BeFalse("一次性邀请已被用掉");
        mine.GetProperty("alreadyMember").GetBoolean().Should().BeTrue(
            "对已加入的人来说，不该只看到「邀请已失效」");

        // 陌生人再看：既不可用、也不是成员
        var other = (await RootAsync(await _client.SendAsync(Req(HttpMethod.Get,
            $"/api/retail/invites/preview?code={code}", tenant: null, sub: "kc-someone-else", tenants: ""))))
            .GetProperty("data");
        other.GetProperty("usable").GetBoolean().Should().BeFalse();
        other.GetProperty("alreadyMember").GetBoolean().Should().BeFalse();
    }
}
