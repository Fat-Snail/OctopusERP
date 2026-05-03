using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

public class AccountControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AccountControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndContainsUserInfo()
    {
        var response = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "admin", Password = "Admin@123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        result.Should().NotBeNull();
        result!.Code.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.UserName.Should().Be("admin");
        result.Data.Roles.Should().Contain("admin");
        result.Data.Permissions.Should().Contain("*:*:*");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "admin", Password = "WrongPassword" });

        response.StatusCode.Should().Be(HttpStatusCode.OK); // HTTP 200, code=401 in body
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        result!.Code.Should().Be(401);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "nobody", Password = "Pass@123" });

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        result!.Code.Should().Be(401);
    }

    [Fact]
    public async Task Login_WithDisabledUser_Returns403()
    {
        // wangwu (UserId=4) has Status=0
        var response = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "wangwu", Password = "User@123" });

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        result!.Code.Should().Be(403);
        result.Msg.Should().Contain("禁用");
    }

    [Fact]
    public async Task Logout_Returns200()
    {
        var response = await _client.PostAsync("/api/account/logout", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task Login_WithEditorUser_ReturnsRoleInfo()
    {
        var response = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "editor", Password = "Editor@123" });

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.Roles.Should().Contain("editor");
        // editor 角色仅绑定目录/菜单型菜单，没有按钮型权限节点，Permissions 可为空
        result.Data.Permissions.Should().NotContain("*:*:*", "非超级管理员不应有通配权限");
    }
}
