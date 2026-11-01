using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 种子账号的密码策略回归。
///
/// 历史问题：UMC 种子里 zhangsan/lisi/wangwu… 是 `User@123`、editor 是 `Editor@123`，
/// 而 Keycloak 的 realm JSON 给 zhangsan 种的是 `Admin@123`。
/// 同一个用户名在两个系统两套密码，登录失败时根本无法判断是"密码错"还是"用错了系统"。
///
/// 现在统一为 `Admin@123`。这个用例会在有人把密码改回多套时立刻失败。
/// </summary>
public class SeedPasswordPolicyTests : IClassFixture<TestWebApplicationFactory>
{
    private const string UnifiedPassword = "Admin@123";

    private readonly HttpClient _client;

    public SeedPasswordPolicyTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationClientOptionsAllowRedirect());
    }

    /// <summary>所有启用状态的种子账号都能用同一个密码登录</summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("zhangsan")]
    [InlineData("lisi")]
    [InlineData("editor")]
    public async Task EveryEnabledSeedUser_CanLoginWithUnifiedPassword(string userName)
    {
        var body = await LoginAsync(userName, UnifiedPassword);

        body.Code.Should().Be(200,
            $"{userName} 应当能用统一的 {UnifiedPassword} 登录；" +
            "若失败说明种子又改回了多套密码");
    }

    [Fact]
    public async Task DisabledSeedUser_IsRejectedByStatus_NotByPassword()
    {
        // wangwu 是种子里的禁用账号：密码对，但状态拦住
        var body = await LoginAsync("wangwu", UnifiedPassword);

        body.Code.Should().Be(403, "禁用账号应返回 403，而不是密码错误 401");
        body.Msg.Should().Contain("禁用");
    }

    private async Task<ApiResponse<LoginResponse>> LoginAsync(string userName, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/account/login", new { userName, password });
        return (await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>())!;
    }

    /// <summary>测试里不关心重定向，但需要与其它用例保持一致的客户端选项</summary>
    private sealed class WebApplicationClientOptionsAllowRedirect : WebApplicationFactoryClientOptions
    {
        public WebApplicationClientOptionsAllowRedirect() => AllowAutoRedirect = false;
    }
}
