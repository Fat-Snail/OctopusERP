using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusRetail.Api.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusRetail.Api.Tests;

/// <summary>
/// UMC → 零售：成员资料快照回推（ADR-010）。
///
/// UMC 是用户主数据源；零售库里的 userName/displayName/phone 只是成员列表用的快照。
/// 管理员在 5173 改手机号后必须回推，否则成员列表会长期显示旧值或空值
/// （实测：feiwoniu001 的手机号一度两边对不上）。
/// </summary>
public class InternalMemberProfileTests(RetailTestFactory factory) : IClassFixture<RetailTestFactory>
{
    private const string Secret = "retail-internal-secret-dev";

    private readonly HttpClient _client = factory.CreateClient();

    private async Task<T> WithDbAsync<T>(Func<RetailDbContext, Task<T>> f)
    {
        using var scope = factory.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<RetailDbContext>());
    }

    private async Task<JsonElement> PostAsync(object body, string? secret = Secret)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/retail/internal/members/sync-profile")
        {
            Content = JsonContent.Create(body),
        };
        if (secret is not null) req.Headers.Add("X-Internal-Secret", secret);

        var resp = await _client.SendAsync(req);
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private Task<RetailMembership> MemberAsync(string kcSub, string tenant) => WithDbAsync(db =>
        db.Memberships.IgnoreQueryFilters()
            .SingleAsync(m => m.KcSub == kcSub && m.TenantId == tenant));

    [Fact]
    public async Task WithWrongSecret_IsRejected()
    {
        var before = await MemberAsync("kc-lisi", "t_shanghai_001");

        var r = await PostAsync(new { kcSub = "kc-lisi", phone = "13000000001" }, secret: "not-the-right-secret");

        r.GetProperty("code").GetInt32().Should().Be(403);
        (await MemberAsync("kc-lisi", "t_shanghai_001")).Phone.Should().Be(before.Phone);
    }

    [Fact]
    public async Task WithoutSecret_IsRejected()
    {
        var before = await MemberAsync("kc-lisi", "t_shanghai_001");

        var r = await PostAsync(new { kcSub = "kc-lisi", phone = "13000000000" }, secret: null);

        r.GetProperty("code").GetInt32().Should().Be(403);
        (await MemberAsync("kc-lisi", "t_shanghai_001")).Phone.Should().Be(before.Phone);
    }

    [Fact]
    public async Task WithSecret_UpdatesSnapshotFields()
    {
        var r = await PostAsync(new
        {
            kcSub = "kc-lisi",
            displayName = "李四(改)",
            phone = "13111112222",
        });

        r.GetProperty("code").GetInt32().Should().Be(200, r.GetProperty("msg").GetString());

        var m = await MemberAsync("kc-lisi", "t_shanghai_001");
        m.DisplayName.Should().Be("李四(改)");
        m.Phone.Should().Be("13111112222");
    }

    [Fact]
    public async Task EmptyFields_DoNotOverwriteExistingValues()
    {
        await PostAsync(new { kcSub = "kc-lisi", phone = "13333334444", displayName = "李四" });

        // 只改手机号，不传姓名：姓名必须保留
        await PostAsync(new { kcSub = "kc-lisi", phone = "13555556666" });

        var m = await MemberAsync("kc-lisi", "t_shanghai_001");
        m.Phone.Should().Be("13555556666");
        m.DisplayName.Should().Be("李四", "未传的字段不能被清空");
    }

    [Fact]
    public async Task UnknownSub_ReturnsZeroUpdated_WithoutError()
    {
        var r = await PostAsync(new { kcSub = "kc-does-not-exist", phone = "13000000001" });

        r.GetProperty("code").GetInt32().Should().Be(200);
        r.GetProperty("data").GetProperty("updated").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task UpdatesAllOrganizationsOfTheSameUser()
    {
        // zhangsan 同时在上海店与子公司
        await PostAsync(new { kcSub = "kc-zhangsan", phone = "13777778888" });

        var rows = await WithDbAsync(db => db.Memberships.IgnoreQueryFilters()
            .Where(m => m.KcSub == "kc-zhangsan").ToListAsync());

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(m => m.Phone == "13777778888",
            "同一个人可能在多个组织里，资料快照要一起更新");
    }
}
