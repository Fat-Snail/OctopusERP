using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 用户列表数据权限 + 用户菜单权限测试
///
/// 数据权限模拟（Step 2 阶段通过 deptId 参数模拟，Step 4 接 EF 后改为真实过滤）：
///   DeptId=2 总裁办：admin(UserId=1)
///   DeptId=3 技术部：zhangsan(UserId=2), lisi(UserId=3)
///   DeptId=4 市场部：wangwu(UserId=4，已禁用)
///   DeptId=5 行政部：editor(UserId=5)
///
/// 菜单权限（基于角色 MenuIds 中 F 类型节点的 Permission 字段）：
///   admin(RoleId=1)  → *:*:* 通配
///   common(RoleId=2) → MenuIds={1,2,3,100,101,102}，无 F 类型 → Permissions 为空
///   editor(RoleId=3) → MenuIds={1,2,3,100,101,102,103}，无 F 类型 → Permissions 为空
/// </summary>
public class UserDataPermissionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserDataPermissionTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ─── 用户列表数据权限过滤 ────────────────────────────

    [Fact]
    public async Task GetUserList_FilterByDeptId_ReturnsOnlyUsersInThatDept()
    {
        // 技术部(DeptId=3)有 zhangsan 和 lisi
        var response = await _client.GetAsync("/api/system/user/list?deptId=3");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();

        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().OnlyContain(u => u.DeptId == 3,
            "按 deptId=3 过滤应只返回技术部用户");
        result.Data.Rows.Should().Contain(u => u.UserName == "zhangsan");
        result.Data.Rows.Should().Contain(u => u.UserName == "lisi");
        result.Data.Rows.Should().NotContain(u => u.UserName == "admin",
            "admin 属于总裁办(DeptId=2)，不应出现");
    }

    [Fact]
    public async Task GetUserList_FilterByDeptId2_ReturnsOnlyAdminUser()
    {
        // 总裁办(DeptId=2)只有 admin
        var response = await _client.GetAsync("/api/system/user/list?deptId=2");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();

        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().OnlyContain(u => u.DeptId == 2);
        result.Data.Rows.Should().Contain(u => u.UserName == "admin");
        result.Data.Rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUserList_FilterByDeptId_NoMatchingUsers_ReturnsEmpty()
    {
        // 子部门前端组(DeptId=6)无用户
        var response = await _client.GetAsync("/api/system/user/list?deptId=6");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();

        result!.Code.Should().Be(200);
        result.Data!.Total.Should().Be(0);
        result.Data.Rows.Should().BeEmpty("该部门无任何用户");
    }

    [Fact]
    public async Task GetUserList_FilterByDeptIdAndStatus_CompositeFilter()
    {
        // 全部 Status=1(启用) 的技术部用户
        var response = await _client.GetAsync("/api/system/user/list?deptId=3&status=1");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();

        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().OnlyContain(u => u.DeptId == 3 && u.Status == 1);
    }

    [Fact]
    public async Task GetUserList_NoFilter_ReturnsAllUsers()
    {
        // 不带任何过滤条件，返回全部种子用户（模拟"全部数据"权限范围）
        var response = await _client.GetAsync("/api/system/user/list?pageNum=1&pageSize=100");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();

        result!.Code.Should().Be(200);
        result.Data!.Total.Should().BeGreaterThanOrEqualTo(5, "种子数据有5个用户");
    }

    // ─── 用户角色绑定（数据权限的前提） ─────────────────

    [Fact]
    public async Task GetAuthRole_ReturnsUserAssignedRolesAndAllRoles()
    {
        // lisi(UserId=3) 绑定了 RoleId=[2,3]（普通用户+编辑员）
        var response = await _client.GetAsync("/api/system/user/authRole/3");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        result!.Code.Should().Be(200);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("assignedRoles");
        json.Should().Contain("roles");
    }

    [Fact]
    public async Task GetAuthRole_WithNonExistentUser_Returns404()
    {
        var response = await _client.GetAsync("/api/system/user/authRole/9999");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(404);
    }

    [Fact]
    public async Task BindRoles_UpdatesUserRoles_VerifiedByAuthRole()
    {
        // 创建临时用户，避免污染其他测试的种子数据
        var createReq = new
        {
            UserName = "bind_role_test_ut", NickName = "绑定角色测试", Email = "bindrole@test.com",
            Password = "Test@123", DeptIds = new[] { 3 }, Status = 1, RoleIds = new[] { 3 }
        };
        var created = await (await _client.PostAsJsonAsync("/api/system/user", createReq))
            .Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        var uid = created!.Data!.UserId;

        // 绑定超级管理员角色
        var response = await _client.PutAsync($"/api/system/user/authRole?userId={uid}&roleIds=1", null);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(200);

        // 验证 assignedRoles 包含 admin
        var getRaw = await (await _client.GetAsync($"/api/system/user/authRole/{uid}"))
            .Content.ReadAsStringAsync();
        getRaw.Should().Contain("admin", "绑定后应包含 admin 角色");

        // 清理：删除临时用户
        await _client.DeleteAsync($"/api/system/user/{uid}");
    }

    [Fact]
    public async Task BindRoles_MultipleRoles_UserHasAllRoles()
    {
        // 创建临时用户，绑定多角色后验证，避免状态污染
        var createReq = new
        {
            UserName = "multi_role_test_ut", NickName = "多角色测试", Email = "multirole@test.com",
            Password = "Test@123", DeptIds = new[] { 3 }, Status = 1, RoleIds = new[] { 3 }
        };
        var created = await (await _client.PostAsJsonAsync("/api/system/user", createReq))
            .Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        var uid = created!.Data!.UserId;

        var response = await _client.PutAsync($"/api/system/user/authRole?userId={uid}&roleIds=1,2,3", null);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(200);

        var getRaw = await (await _client.GetAsync($"/api/system/user/authRole/{uid}"))
            .Content.ReadAsStringAsync();
        getRaw.Should().Contain("admin");
        getRaw.Should().Contain("common");
        getRaw.Should().Contain("editor");

        // 清理
        await _client.DeleteAsync($"/api/system/user/{uid}");
    }

    // ─── 用户菜单权限（基于角色 MenuIds 推导） ───────────

    [Fact]
    public async Task Login_AdminUser_ReturnsWildcardPermission()
    {
        // 超级管理员 admin(RoleId=1) 登录，Permissions 应包含 "*:*:*"
        var response = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "admin", Password = "Admin@123" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();

        result!.Code.Should().Be(200);
        result.Data!.Permissions.Should().Contain("*:*:*",
            "超级管理员拥有通配权限");
        result.Data.Permissions.Should().HaveCount(1,
            "超级管理员只需一条通配权限");
    }

    [Fact]
    public async Task Login_CommonUser_ReturnsNoButtonPermissions()
    {
        // common 角色(RoleId=2) MenuIds={1,2,3,100,101,102}，均为 M/C 类型，无 F(按钮)
        var response = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "zhangsan", Password = "User@123" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();

        result!.Code.Should().Be(200);
        result.Data!.Permissions.Should().NotContain("*:*:*",
            "非超级管理员不应有通配权限");
        result.Data.Roles.Should().Contain("common");
    }

    [Fact]
    public async Task Login_EditorUser_HasNoWildcardPermission()
    {
        // editor 角色(RoleId=3) MenuIds 不含按钮节点
        var response = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "editor", Password = "Editor@123" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();

        result!.Code.Should().Be(200);
        result.Data!.Permissions.Should().NotContain("*:*:*");
        result.Data.Roles.Should().Contain("editor");
    }

    [Fact]
    public async Task Login_UserWithAdminRole_ReturnsWildcardPermission()
    {
        // 创建临时用户，绑定超级管理员角色，验证登录后获得通配权限
        var createReq = new
        {
            UserName = "admin_perm_test_ut", NickName = "权限测试用户", Email = "adminperm@test.com",
            Password = "Test@123", DeptIds = new[] { 3 }, Status = 1, RoleIds = new[] { 1 }
        };
        var created = await (await _client.PostAsJsonAsync("/api/system/user", createReq))
            .Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        var uid = created!.Data!.UserId;

        var response = await _client.PostAsJsonAsync("/api/account/login",
            new { UserName = "admin_perm_test_ut", Password = "Test@123" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();

        result!.Code.Should().Be(200);
        result.Data!.Permissions.Should().Contain("*:*:*",
            "绑定超级管理员角色后应有通配权限");

        // 清理
        await _client.DeleteAsync($"/api/system/user/{uid}");
    }

    // ─── 用户菜单树（菜单按角色 MenuIds 过滤） ───────────

    [Fact]
    public async Task GetMenuTree_ReturnsFullTreeForAdminQuery()
    {
        // 完整菜单树包含系统管理、系统监控、系统工具等一级目录
        var response = await _client.GetAsync("/api/system/menu/tree");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<MenuResponse>>>();

        result!.Code.Should().Be(200);
        result.Data.Should().NotBeEmpty();
        result.Data.Should().Contain(m => m.MenuName == "系统管理",
            "系统管理应在菜单树中");
        result.Data.Should().Contain(m => m.MenuName == "系统监控",
            "系统监控应在菜单树中");
    }

    [Fact]
    public async Task GetRoleMenuIds_CommonRole_OnlyHasAllowedMenus()
    {
        // common 角色(RoleId=2) 只绑定了部分菜单：{1,2,3,100,101,102}
        var response = await _client.GetAsync("/api/system/menu/role/2");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<long>>>();

        result!.Code.Should().Be(200);
        result.Data.Should().Contain(100L, "common 角色可访问用户管理");
        result.Data.Should().Contain(101L, "common 角色可访问机构管理");
        result.Data.Should().NotContain(104L, "common 角色不能访问角色管理(MenuId=104)");
        result.Data.Should().NotContain(110L, "common 角色不能访问系统监控(MenuId=110)");
    }

    [Fact]
    public async Task GetRoleMenuIds_EditorRole_HasMoreMenusThanCommon()
    {
        // editor 角色(RoleId=3) MenuIds={1,2,3,100,101,102,103}，多了菜单管理(103)
        var editorResp = await _client.GetAsync("/api/system/menu/role/3");
        var editorResult = await editorResp.Content.ReadFromJsonAsync<ApiResponse<List<long>>>();

        var commonResp = await _client.GetAsync("/api/system/menu/role/2");
        var commonResult = await commonResp.Content.ReadFromJsonAsync<ApiResponse<List<long>>>();

        editorResult!.Data.Should().Contain(103L, "editor 角色额外拥有菜单管理(MenuId=103)");
        commonResult!.Data.Should().NotContain(103L, "common 角色不能访问菜单管理");

        editorResult.Data!.Count.Should().BeGreaterThan(commonResult.Data!.Count,
            "editor 角色菜单数量多于 common 角色");
    }
}
