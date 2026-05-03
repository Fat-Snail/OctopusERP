using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 角色管理接口测试
/// 覆盖：CRUD 闭环、角色绑定菜单权限、角色绑定数据权限范围
///
/// 种子角色：
///   RoleId=1 超级管理员(admin)  DataScope=1(全部)
///   RoleId=2 普通用户(common)   DataScope=4(仅本人)
///   RoleId=3 编辑员(editor)     DataScope=3(本部门)
/// </summary>
public class RoleControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RoleControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ─── 查询 ────────────────────────────────────────────

    [Fact]
    public async Task GetRoleList_ReturnsAllSeedRoles()
    {
        var response = await _client.GetAsync("/api/system/role/list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<RoleResponse>>>();
        result!.Code.Should().Be(200);
        result.Data!.Total.Should().BeGreaterThanOrEqualTo(3, "种子数据有3个角色");
        result.Data.Rows.Should().Contain(r => r.RoleKey == "admin");
        result.Data.Rows.Should().Contain(r => r.RoleKey == "common");
        result.Data.Rows.Should().Contain(r => r.RoleKey == "editor");
    }

    [Fact]
    public async Task GetRoleList_FilterByRoleName_ReturnsMatchedOnly()
    {
        var response = await _client.GetAsync("/api/system/role/list?roleName=管理员");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<RoleResponse>>>();
        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().OnlyContain(r => r.RoleName.Contains("管理员"));
    }

    [Fact]
    public async Task GetRoleById_WithValidId_ReturnsRoleWithMenuIds()
    {
        var response = await _client.GetAsync("/api/system/role/1");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.RoleKey.Should().Be("admin");
        result.Data.DataScope.Should().Be("1", "超级管理员应为全部数据权限");
        result.Data.MenuIds.Should().NotBeEmpty("超级管理员绑定了所有菜单");
    }

    [Fact]
    public async Task GetRoleById_WithInvalidId_Returns404()
    {
        var response = await _client.GetAsync("/api/system/role/9999");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        result!.Code.Should().Be(404);
    }

    // ─── 新增 ────────────────────────────────────────────

    [Fact]
    public async Task CreateRole_WithValidData_AppearsInList()
    {
        var req = new
        {
            RoleName = "审计员_UT",
            RoleKey = "auditor_ut",
            RoleSort = 10,
            DataScope = "2",
            MenuIds = new[] { 1, 2, 100 },
            Status = 1,
            Remark = "测试新增角色"
        };

        var createResp = await _client.PostAsJsonAsync("/api/system/role", req);
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        created!.Code.Should().Be(200);
        created.Data!.RoleKey.Should().Be("auditor_ut");
        created.Data.DataScope.Should().Be("2");

        // 内存 CRUD 闭环：新增后可在列表查到
        var listResp = await _client.GetAsync("/api/system/role/list?roleKey=auditor_ut");
        var list = await listResp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<RoleResponse>>>();
        list!.Data!.Rows.Should().Contain(r => r.RoleKey == "auditor_ut");
    }

    [Fact]
    public async Task CreateRole_WithDuplicateRoleKey_ReturnsBadRequest()
    {
        // "admin" 已存在于种子数据
        var req = new { RoleName = "另一个管理员", RoleKey = "admin", RoleSort = 99, Status = 1 };
        var response = await _client.PostAsJsonAsync("/api/system/role", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        result!.Code.Should().Be(500, "角色标识已存在应返回错误");
    }

    // ─── 修改 ────────────────────────────────────────────

    [Fact]
    public async Task UpdateRole_WithValidData_ReturnsUpdatedRole()
    {
        var req = new
        {
            RoleId = 3,
            RoleName = "编辑员（已更新）",
            RoleKey = "editor",
            RoleSort = 3,
            DataScope = "3",
            MenuIds = new[] { 1, 2, 100, 101 },
            Status = 1
        };
        var response = await _client.PutAsJsonAsync("/api/system/role", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.RoleName.Should().Be("编辑员（已更新）");
    }

    [Fact]
    public async Task UpdateRole_WithNonExistentId_Returns404()
    {
        var req = new { RoleId = 9999, RoleName = "不存在角色", RoleKey = "ghost", RoleSort = 1, Status = 1 };
        var response = await _client.PutAsJsonAsync("/api/system/role", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        result!.Code.Should().Be(404);
    }

    // ─── 删除 ────────────────────────────────────────────

    [Fact]
    public async Task DeleteRole_WithValidId_RoleRemovedFromList()
    {
        // 先创建再删除，不影响种子数据
        var req = new { RoleName = "待删除角色_UT", RoleKey = "del_role_ut", RoleSort = 99, Status = 1 };
        var createResp = await _client.PostAsJsonAsync("/api/system/role", req);
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        var newId = created!.Data!.RoleId;

        var deleteResp = await _client.DeleteAsync($"/api/system/role/{newId}");
        var deleteResult = await deleteResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        deleteResult!.Code.Should().Be(200);

        var getResp = await _client.GetAsync($"/api/system/role/{newId}");
        var getResult = await getResp.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        getResult!.Code.Should().Be(404);
    }

    // ─── 角色绑定菜单权限 ────────────────────────────────

    [Fact]
    public async Task BindMenu_UpdatesRoleMenuIds_VerifiedByGetRoleMenuIds()
    {
        // 给 common 角色(RoleId=2)绑定新的菜单集合
        var newMenuIds = new long[] { 1, 2, 3, 100, 101, 102, 103, 104 };
        var req = new { RoleId = 2, MenuIds = newMenuIds };

        var bindResp = await _client.PostAsJsonAsync("/api/system/role/menu", req);
        var bindResult = await bindResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        bindResult!.Code.Should().Be(200);

        // 用 GET /api/system/menu/role/{roleId} 验证绑定已生效
        var getMenuResp = await _client.GetAsync("/api/system/menu/role/2");
        var getMenuResult = await getMenuResp.Content.ReadFromJsonAsync<ApiResponse<List<long>>>();
        getMenuResult!.Code.Should().Be(200);
        getMenuResult.Data.Should().BeEquivalentTo(newMenuIds);
    }

    [Fact]
    public async Task BindMenu_WithNonExistentRole_Returns404()
    {
        var req = new { RoleId = 9999, MenuIds = new[] { 1, 2 } };
        var response = await _client.PostAsJsonAsync("/api/system/role/menu", req);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(404);
    }

    [Fact]
    public async Task GetRoleMenuIds_ReturnsCorrectMenuIdsForRole()
    {
        // 超级管理员(RoleId=1)绑定了大量菜单
        var response = await _client.GetAsync("/api/system/menu/role/1");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<long>>>();
        result!.Code.Should().Be(200);
        result.Data.Should().NotBeEmpty("超级管理员绑定了所有菜单");
        result.Data.Should().Contain(100, "超级管理员可访问用户管理菜单(MenuId=100)");
        result.Data.Should().Contain(104, "超级管理员可访问角色管理菜单(MenuId=104)");
    }

    // ─── 角色绑定数据权限（五种范围） ────────────────────

    [Theory]
    [InlineData("1", "全部数据")]
    [InlineData("2", "本部门及子部门数据")]
    [InlineData("3", "本部门数据")]
    [InlineData("4", "仅本人数据")]
    [InlineData("5", "自定义数据权限")]
    public async Task BindDept_AllDataScopeValues_SaveCorrectly(string dataScope, string description)
    {
        // 创建一个临时角色用于测试
        var createReq = new
        {
            RoleName = $"数据权限测试_{dataScope}_UT",
            RoleKey = $"scope_{dataScope}_ut",
            RoleSort = 99,
            DataScope = "1",
            Status = 1
        };
        var createResp = await _client.PostAsJsonAsync("/api/system/role", createReq);
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        var roleId = created!.Data!.RoleId;

        // 绑定数据权限
        var deptIds = dataScope == "5" ? new long[] { 3, 4 } : Array.Empty<long>();
        var bindReq = new { RoleId = roleId, DataScope = dataScope, DeptIds = deptIds };
        var bindResp = await _client.PostAsJsonAsync("/api/system/role/dept", bindReq);
        var bindResult = await bindResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        bindResult!.Code.Should().Be(200, $"设置 {description} 应成功");

        // 验证 DataScope 已更新
        var getResp = await _client.GetAsync($"/api/system/role/{roleId}");
        var getResult = await getResp.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        getResult!.Data!.DataScope.Should().Be(dataScope, description);

        // 清理
        await _client.DeleteAsync($"/api/system/role/{roleId}");
    }

    [Fact]
    public async Task BindDept_WithCustomScope_SavesDeptIds()
    {
        // DataScope=5 自定义数据权限时，DeptIds 应正确保存
        var createReq = new
        {
            RoleName = "自定义数据权限角色_UT",
            RoleKey = "custom_scope_ut",
            RoleSort = 99,
            DataScope = "1",
            Status = 1
        };
        var created = await (await _client.PostAsJsonAsync("/api/system/role", createReq))
            .Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        var roleId = created!.Data!.RoleId;

        var customDeptIds = new long[] { 3, 4, 5 };
        var bindReq = new { RoleId = roleId, DataScope = "5", DeptIds = customDeptIds };
        await _client.PostAsJsonAsync("/api/system/role/dept", bindReq);

        var getResult = await (await _client.GetAsync($"/api/system/role/{roleId}"))
            .Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
        getResult!.Data!.DataScope.Should().Be("5");
        getResult.Data.DeptIds.Should().BeEquivalentTo(customDeptIds);

        await _client.DeleteAsync($"/api/system/role/{roleId}");
    }
}
