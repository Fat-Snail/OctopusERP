using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusUMC.Api.DTOs;
using OctopusUMC.Infrastructure.Persistence;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// ADR-010：零售域建号改走 UMC 内部接口。
///
/// 这是「所有用户都在 UMC 管理」的落地点——原来零售直连 Keycloak 建号，
/// UMC 完全不知道这个人存在，运营在 5173 查不到、也停用不了。
/// </summary>
/// <summary>
/// 本组用例不连真实 Keycloak（appsettings.json 默认 SyncEnabled=true）。
/// 此处关掉同步，专注于"UMC 是否登记了用户"这件事；
/// Keycloak 建号细节由 KeycloakSyncServiceTests 单独覆盖。
/// </summary>
public class InternalUserApiFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Keycloak:SyncEnabled", "false");
    }
}

public class InternalUserApiTests : IClassFixture<InternalUserApiFactory>
{
    private const string Secret = "retail-internal-secret-dev";

    private readonly HttpClient _client;
    private readonly InternalUserApiFactory _factory;

    public InternalUserApiTests(InternalUserApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> f)
    {
        using var scope = _factory.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<ApiResponse<object?>> PostRetailAsync(object body, string? secret = Secret)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/users/retail")
        {
            Content = JsonContent.Create(body),
        };
        if (secret is not null) req.Headers.Add("X-Internal-Secret", secret);

        var resp = await _client.SendAsync(req);
        return (await resp.Content.ReadFromJsonAsync<ApiResponse<object?>>())!;
    }

    private static object NewUser(string name) => new
    {
        userName = name,
        nickName = "门店客户",
        phoneNumber = "13900002222",
        email = $"{name}@retail.placeholder",
        password = "Passw0rd!",
        source = "retail",
    };

    [Fact]
    public async Task WithoutSecret_IsRejected()
    {
        var r = await PostRetailAsync(NewUser("nosecret01"), secret: null);

        r.Code.Should().Be(403);
        await WithDbAsync(async db =>
            (await db.Users.AnyAsync(u => u.UserName == "nosecret01")).Should().BeFalse());
        return;
    }

    [Fact]
    public async Task WithSecret_CreatesCustomerUser()
    {
        var r = await PostRetailAsync(NewUser("retailcust01"));

        r.Code.Should().Be(200, r.Msg);

        var u = await WithDbAsync(db => db.Users.SingleAsync(x => x.UserName == "retailcust01"));
        u.UserType.Should().Be("customer", "零售邀请进来的是客户/店员，不是平台员工");
        u.Source.Should().Be("retail");
        u.Status.Should().Be(1);
        u.NickName.Should().Be("门店客户");
        u.PhoneNumber.Should().Be("13900002222");

        // 密码必须是可用的 BCrypt 哈希，且不是明文
        u.PasswordHash.Should().NotBe("Passw0rd!");
        BCrypt.Net.BCrypt.Verify("Passw0rd!", u.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task DuplicateUserName_IsRejected()
    {
        (await PostRetailAsync(NewUser("dupcust01"))).Code.Should().Be(200);
        var second = await PostRetailAsync(NewUser("dupcust01"));

        second.Code.Should().Be(409);
        second.Msg.Should().Contain("占用");
    }

    [Fact]
    public async Task ShortPassword_IsRejected()
    {
        var r = await PostRetailAsync(new
        {
            userName = "shortpwd01",
            password = "123",
            source = "retail",
        });

        r.Code.Should().Be(400);
        r.Msg.Should().Contain("8");
    }

    [Fact]
    public async Task Adopt_RegistersExistingKeycloakUser_Idempotently()
    {
        var body = new
        {
            userName = "adopted01",
            kcSub = "kc-adopted-0001",
            nickName = "存量客户",
            userType = "customer",
            source = "retail",
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/users/adopt")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add("X-Internal-Secret", Secret);
        var first = await (await _client.SendAsync(req)).Content.ReadFromJsonAsync<ApiResponse<object?>>();
        first!.Code.Should().Be(200, first.Msg);

        var u = await WithDbAsync(db => db.Users.SingleAsync(x => x.UserName == "adopted01"));
        u.KcSub.Should().Be("kc-adopted-0001");
        u.UserType.Should().Be("customer");

        // 二次接管不得重复建号
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/internal/users/adopt")
        {
            Content = JsonContent.Create(body),
        };
        req2.Headers.Add("X-Internal-Secret", Secret);
        var second = await (await _client.SendAsync(req2)).Content
            .ReadFromJsonAsync<ApiResponse<object?>>();
        second!.Code.Should().Be(200);

        var count = await WithDbAsync(db =>
            db.Users.CountAsync(x => x.UserName == "adopted01"));
        count.Should().Be(1, "接管必须幂等");
    }
}
