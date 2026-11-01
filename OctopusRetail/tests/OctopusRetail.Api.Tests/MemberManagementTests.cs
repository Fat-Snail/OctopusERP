using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusRetail.Api.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// 组织成员管理。
///
/// 零售域的用户主数据在 Keycloak（不走 UMC），所以禁用/改密/移出必须有这个入口，
/// 否则只能去 Keycloak Admin Console 手工操作。这里重点验证**权限分级**与不变量。
///
/// 用独立的 t_mtest 租户，避免与其它测试的种子数据互相干扰。
/// </summary>
public class MemberManagementTests(RetailTestFactory factory) : IClassFixture<RetailTestFactory>
{
    private const string T = "t_mtest";

    private readonly HttpClient _client = factory.CreateClient();

    private static HttpRequestMessage Req(
        HttpMethod method, string url, object? body = null,
        string? sub = null, bool withTenant = true)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        req.Headers.Add("X-Test-Tenants", T);
        if (withTenant) req.Headers.Add("X-Tenant-Id", T);
        req.Headers.Add("X-Test-Sub", sub ?? "kc-m-manager");
        return req;
    }

    private static async Task<JsonElement> RootAsync(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<T> WithDbAsync<T>(Func<RetailDbContext, Task<T>> f)
    {
        using var scope = factory.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<RetailDbContext>());
    }

    /// <summary>幂等地准备 t_mtest 的组织与成员</summary>
    private async Task EnsureFixtureAsync()
    {
        await WithDbAsync(async db =>
        {
            if (await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.TenantId == T))
                return 0;

            db.Tenants.Add(new RetailTenant
            {
                TenantId = T, TenantCode = "MTEST", TenantName = "成员管理测试组织",
                TenantType = "company", Status = "active", CreatedAt = DateTime.UtcNow,
            });
            db.Memberships.AddRange(
                New("kc-m-owner", "owner"),
                New("kc-m-manager", "manager"),
                New("kc-m-manager2", "manager"),
                New("kc-m-staff", "staff"));
            await db.SaveChangesAsync();
            return 1;

            RetailMembership New(string sub, string role) => new()
            {
                KcSub = sub, TenantId = T, RoleInTenant = role, Status = "active",
                UserName = sub.Replace("kc-", ""), DisplayName = sub,
                JoinedAt = DateTime.UtcNow, KcSyncState = MembershipSyncState.Unmanaged,
            };
        });
    }

    private async Task<long> IdOfAsync(string sub) => await WithDbAsync(async db =>
        (await db.Memberships.IgnoreQueryFilters()
            .SingleAsync(m => m.TenantId == T && m.KcSub == sub)).MembershipId);

    // ── 列表 ──────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsOnlyCurrentTenantMembers_WithSnapshotNames()
    {
        await EnsureFixtureAsync();

        var resp = await _client.SendAsync(Req(HttpMethod.Get, "/api/retail/members"));

        var data = (await RootAsync(resp)).GetProperty("data");
        data.GetProperty("tenantId").GetString().Should().Be(T);
        data.GetProperty("total").GetInt32().Should().Be(4);

        var items = data.GetProperty("items").EnumerateArray().ToList();
        items.Select(x => x.GetProperty("kcSub").GetString())
            .Should().BeEquivalentTo(new[] { "kc-m-owner", "kc-m-manager", "kc-m-manager2", "kc-m-staff" });
        items.Should().OnlyContain(x => x.GetProperty("userName").GetString() != null,
            "成员列表应能展示用户名快照");
    }

    // ── 正常操作 ──────────────────────────────────────────

    [Fact]
    public async Task Manager_CanDisableAndEnableStaff()
    {
        await EnsureFixtureAsync();
        var id = await IdOfAsync("kc-m-staff");

        var disable = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/members/{id}/status", new { Enabled = false }, sub: "kc-m-manager"));
        (await RootAsync(disable)).GetProperty("code").GetInt32().Should().Be(200);

        var afterDisable = await WithDbAsync(db => db.Memberships.IgnoreQueryFilters()
            .SingleAsync(m => m.MembershipId == id));
        afterDisable.Status.Should().Be("suspended");

        var enable = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/members/{id}/status", new { Enabled = true }, sub: "kc-m-manager"));
        (await RootAsync(enable)).GetProperty("code").GetInt32().Should().Be(200);

        var afterEnable = await WithDbAsync(db => db.Memberships.IgnoreQueryFilters()
            .SingleAsync(m => m.MembershipId == id));
        afterEnable.Status.Should().Be("active");
    }

    [Fact]
    public async Task Manager_CanRemoveStaff()
    {
        await EnsureFixtureAsync();
        var id = await IdOfAsync("kc-m-staff");

        var resp = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/members/{id}/remove", null, sub: "kc-m-manager"));
        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(200);

        var m = await WithDbAsync(db => db.Memberships.IgnoreQueryFilters()
            .SingleAsync(x => x.MembershipId == id));
        m.Status.Should().Be("left");
        m.LeftAt.Should().NotBeNull();
        m.KcSyncState.Should().Be(MembershipSyncState.Pending,
            "移出后要重新对账，把用户踢出 Keycloak 组");
    }

    // ── 权限分级（核心） ──────────────────────────────────

    [Fact]
    public async Task Manager_CannotDisableAnotherManager()
    {
        await EnsureFixtureAsync();
        var id = await IdOfAsync("kc-m-manager2");

        var resp = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/members/{id}/status", new { Enabled = false }, sub: "kc-m-manager"));

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(403);
        (await RootAsync(resp)).GetProperty("msg").GetString().Should().Contain("同级");
    }

    [Fact]
    public async Task Manager_CannotRemoveOwner()
    {
        await EnsureFixtureAsync();
        var id = await IdOfAsync("kc-m-owner");

        var resp = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/members/{id}/remove", null, sub: "kc-m-manager"));

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(403);
    }

    [Fact]
    public async Task Staff_CannotManageMembersAtAll()
    {
        await EnsureFixtureAsync();
        var id = await IdOfAsync("kc-m-manager");

        var resp = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/members/{id}/status", new { Enabled = false }, sub: "kc-m-staff"));

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(403);
        (await RootAsync(resp)).GetProperty("msg").GetString().Should().Contain("无权");
    }

    [Fact]
    public async Task Nobody_CanActOnSelf()
    {
        await EnsureFixtureAsync();
        var id = await IdOfAsync("kc-m-manager");

        var resp = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/members/{id}/status", new { Enabled = false }, sub: "kc-m-manager"));

        (await RootAsync(resp)).GetProperty("msg").GetString().Should().Contain("自己");
    }

    // ── 不变量：不能没有所有者 ────────────────────────────

    [Fact]
    public async Task CannotRemoveLastOwner()
    {
        // 独立租户，只留一个 owner
        const string solo = "t_msolo";
        await WithDbAsync(async db =>
        {
            if (!await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.TenantId == solo))
            {
                db.Tenants.Add(new RetailTenant
                {
                    TenantId = solo, TenantCode = "MSOLO", TenantName = "单人组织",
                    TenantType = "company", Status = "active", CreatedAt = DateTime.UtcNow,
                });
                db.Memberships.AddRange(
                    new RetailMembership
                    {
                        KcSub = "kc-solo-owner", TenantId = solo, RoleInTenant = "owner",
                        Status = "active", JoinedAt = DateTime.UtcNow,
                        KcSyncState = MembershipSyncState.Unmanaged,
                    },
                    new RetailMembership
                    {
                        KcSub = "kc-solo-manager", TenantId = solo, RoleInTenant = "manager",
                        Status = "active", JoinedAt = DateTime.UtcNow,
                        KcSyncState = MembershipSyncState.Unmanaged,
                    });
                await db.SaveChangesAsync();
            }
            return 1;
        });

        var ownerId = await WithDbAsync(async db =>
            (await db.Memberships.IgnoreQueryFilters()
                .SingleAsync(m => m.TenantId == solo && m.KcSub == "kc-solo-owner")).MembershipId);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/retail/members/{ownerId}/remove");
        req.Headers.Add("X-Test-Tenants", solo);
        req.Headers.Add("X-Tenant-Id", solo);
        req.Headers.Add("X-Test-Sub", "kc-solo-manager");   // manager 不能动 owner（等级不够）

        var resp = await _client.SendAsync(req);
        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(403);

        // 换个角度：owner 自己也不该被移除（自我保护）
        var self = new HttpRequestMessage(HttpMethod.Post, $"/api/retail/members/{ownerId}/remove");
        self.Headers.Add("X-Test-Tenants", solo);
        self.Headers.Add("X-Tenant-Id", solo);
        self.Headers.Add("X-Test-Sub", "kc-solo-owner");
        (await RootAsync(await _client.SendAsync(self))).GetProperty("msg").GetString()
            .Should().Contain("自己");
    }

    // ── 重置密码 ──────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_TooShort_IsRejected()
    {
        await EnsureFixtureAsync();
        var id = await IdOfAsync("kc-m-staff");

        var resp = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/members/{id}/reset-password", new { NewPassword = "123" },
            sub: "kc-m-manager"));

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(400);
        (await RootAsync(resp)).GetProperty("msg").GetString().Should().Contain("密码");
    }

    // ── 租户隔离 ──────────────────────────────────────────

    [Fact]
    public async Task CannotActOnMemberOfAnotherTenant()
    {
        await EnsureFixtureAsync();
        // 种子里的上海店成员，不属于 t_mtest
        var otherId = await WithDbAsync(async db =>
            (await db.Memberships.IgnoreQueryFilters()
                .FirstAsync(m => m.TenantId == "t_shanghai_001")).MembershipId);

        var resp = await _client.SendAsync(Req(HttpMethod.Post,
            $"/api/retail/members/{otherId}/status", new { Enabled = false }, sub: "kc-m-manager"));

        (await RootAsync(resp)).GetProperty("code").GetInt32().Should().Be(403);
        (await RootAsync(resp)).GetProperty("msg").GetString().Should().Contain("不属于当前组织");
    }
}
