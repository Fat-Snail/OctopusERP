using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>Phase 3：HasPermission 特性 + 按钮级权限校验测试</summary>
public class Phase3PermissionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public Phase3PermissionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Default client → TestAuthHandler authenticates as admin (UserId=1, *:*:*)
    private HttpClient AdminClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // Impersonate a specific user via X-Test-UserId header
    private HttpClient AsUser(long userId)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return c;
    }

    // Anonymous client: bypass TestAuthHandler, trigger real 401
    private HttpClient AnonClient()
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        return c;
    }

    // ── 用户新增：system:user:add ─────────────────────────────────

    [Fact]
    public async Task CreateUser_AsAdmin_Returns200()
    {
        var resp = await AdminClient().PostAsJsonAsync("/api/system/user", new
        {
            UserName = $"perm_{Guid.NewGuid().ToString("N")[..8]}",
            NickName = "权限测试",
            Email = $"perm_{Guid.NewGuid().ToString("N")[..8]}@test.com",
            Password = "Test@123",
            DeptIds = new[] { 2 },
            PostIds = Array.Empty<int>(),
            RoleIds = Array.Empty<int>(),
            Status = 1
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task CreateUser_AsCommonUser_Returns403()
    {
        // zhangsan (UserId=2): common role → only system:user:list, NOT system:user:add
        var resp = await AsUser(2).PostAsJsonAsync("/api/system/user", new
        {
            UserName = $"noperm_{Guid.NewGuid().ToString("N")[..8]}",
            NickName = "无权限",
            Email = $"noperm_{Guid.NewGuid().ToString("N")[..8]}@test.com",
            Password = "Test@123",
            DeptIds = new[] { 2 },
            PostIds = Array.Empty<int>(),
            RoleIds = Array.Empty<int>(),
            Status = 1
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        result!.Code.Should().Be(403);
        result.Msg.Should().Contain("权限");
    }

    [Fact]
    public async Task CreateUser_Unauthenticated_Returns401()
    {
        var resp = await AnonClient().PostAsJsonAsync("/api/system/user", new
        {
            UserName = "anon_test",
            NickName = "匿名",
            Email = "anon@test.com",
            Password = "Test@123",
            DeptIds = new[] { 2 },
            PostIds = Array.Empty<int>(),
            RoleIds = Array.Empty<int>(),
            Status = 1
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 用户列表（只读）：common 用户有 system:user:list ──────────

    [Fact]
    public async Task GetUserList_AsCommonUser_Returns200()
    {
        var resp = await AsUser(2).GetAsync("/api/system/user/list");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
        result!.Code.Should().Be(200);
    }

    // ── 用户删除：system:user:delete ─────────────────────────────

    [Fact]
    public async Task DeleteUser_AsCommonUser_Returns403()
    {
        var resp = await AsUser(2).DeleteAsync("/api/system/user/999");
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        result!.Code.Should().Be(403);
    }

    // ── 角色新增：system:role:add ─────────────────────────────────

    [Fact]
    public async Task CreateRole_AsCommonUser_Returns403()
    {
        var resp = await AsUser(2).PostAsJsonAsync("/api/system/role", new
        {
            RoleName = "无权限角色",
            RoleKey = "no_perm_role",
            RoleSort = 99,
            DataScope = "1",
            Status = 1,
            MenuIds = Array.Empty<int>()
        });
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        result!.Code.Should().Be(403);
    }

    [Fact]
    public async Task CreateRole_AsAdmin_Returns200()
    {
        var resp = await AdminClient().PostAsJsonAsync("/api/system/role", new
        {
            RoleName = $"权限测试角色_{Guid.NewGuid().ToString("N")[..4]}",
            RoleKey = $"perm_role_{Guid.NewGuid().ToString("N")[..4]}",
            RoleSort = 99,
            DataScope = "1",
            Status = 1,
            MenuIds = Array.Empty<int>()
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        result!.Code.Should().Be(200);
    }

    // ── 部门新增：system:dept:add ─────────────────────────────────

    [Fact]
    public async Task CreateDept_AsCommonUser_Returns403()
    {
        var resp = await AsUser(2).PostAsJsonAsync("/api/system/dept", new
        {
            ParentId = 1,
            DeptName = "无权限测试部",
            OrderNum = 99,
            Status = 1
        });
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        result!.Code.Should().Be(403);
    }
}
