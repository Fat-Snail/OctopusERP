using FluentAssertions;
using OctopusUMC.Api.DTOs;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 业务流程集成测试 — 模拟真实操作链路，每步输出详细日志。
///
/// 运行命令（查看完整流程日志）：
///   dotnet test --filter "BusinessFlow" --logger "console;verbosity=detailed"
/// </summary>
public class BusinessFlowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _out;

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public BusinessFlowTests(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _out = output;
    }

    // ── 工具方法 ──────────────────────────────────────────

    private void Log(string msg) => _out.WriteLine(msg);

    private void LogJson(string label, object? obj) =>
        _out.WriteLine($"{label}:\n{JsonSerializer.Serialize(obj, _json)}\n");

    private async Task<T> Post<T>(string url, object body)
    {
        var resp = await _client.PostAsJsonAsync(url, body);
        return (await resp.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Put<T>(string url, object body)
    {
        var resp = await _client.PutAsJsonAsync(url, body);
        return (await resp.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Get<T>(string url)
    {
        var resp = await _client.GetAsync(url);
        return (await resp.Content.ReadFromJsonAsync<T>())!;
    }

    // ══════════════════════════════════════════════════════
    // Flow 1：管理员登录 → 创建部门/职位/角色 → 新增用户 → 分配权限
    // ══════════════════════════════════════════════════════
    [Fact]
    public async Task Flow1_管理员完整用户管理链路()
    {
        Log("═══════════════════════════════════════════════");
        Log("Flow 1：管理员完整用户管理链路");
        Log("═══════════════════════════════════════════════\n");

        // ── Step 1：管理员登录 ──────────────────────────
        Log("▶ Step 1：管理员登录");
        var loginResult = await Post<ApiResponse<LoginResponse>>(
            "/api/account/login",
            new { UserName = "admin", Password = "Admin@123" });

        loginResult.Code.Should().Be(200);
        LogJson("✅ 管理员已登录", loginResult.Data);

        // ── Step 2：查看已有部门树 ──────────────────────
        Log("▶ Step 2：读取组织架构树");
        var deptTree = await Get<ApiResponse<List<DeptResponse>>>("/api/system/dept/tree");

        deptTree.Code.Should().Be(200);
        var rootDept = deptTree.Data!.First();
        Log($"✅ 组织架构读取成功，根节点：{rootDept.DeptName}，共 {CountDepts(deptTree.Data)} 个部门");
        LogJson("部门树结构", deptTree.Data.Select(d => new
        {
            d.DeptName,
            子部门 = d.Children.Select(c => new { c.DeptName, 孙子部门 = c.Children.Select(g => g.DeptName) })
        }));

        // ── Step 3：新增业务部门 ────────────────────────
        Log("▶ Step 3：新增「产品部」");
        var newDept = await Post<ApiResponse<DeptResponse>>(
            "/api/system/dept",
            new { ParentId = 1, DeptName = "产品部_Flow1", OrderNum = 10, Status = 1 });

        newDept.Code.Should().Be(200);
        var deptId = newDept.Data!.DeptId;
        LogJson($"✅ 部门已创建（DeptId={deptId}）", newDept.Data);

        // ── Step 4：新增职位 ────────────────────────────
        Log("▶ Step 4：新增「产品经理」职位");
        var newPost = await Post<ApiResponse<PostResponse>>(
            "/api/system/post",
            new { PostName = "产品经理_Flow1", PostCode = "pm_flow1", PostSort = 5, Status = 1 });

        newPost.Code.Should().Be(200);
        var postId = newPost.Data!.PostId;
        LogJson($"✅ 职位已创建（PostId={postId}）", newPost.Data);

        // ── Step 5：新增角色 ────────────────────────────
        Log("▶ Step 5：新增「产品角色」并绑定菜单权限");
        var newRole = await Post<ApiResponse<RoleResponse>>(
            "/api/system/role",
            new
            {
                RoleName = "产品角色_Flow1",
                RoleKey = "product_flow1",
                RoleSort = 5,
                DataScope = "3",          // 本部门数据权限
                MenuIds = new[] { 1, 2, 100, 101, 102 },
                Status = 1,
                Remark = "Flow1 测试角色"
            });

        newRole.Code.Should().Be(200);
        var roleId = newRole.Data!.RoleId;
        LogJson($"✅ 角色已创建（RoleId={roleId}）", newRole.Data);

        // ── Step 6：验证角色菜单绑定 ───────────────────
        Log("▶ Step 6：验证角色菜单权限");
        var roleMenus = await Get<ApiResponse<List<long>>>($"/api/system/menu/role/{roleId}");

        roleMenus.Code.Should().Be(200);
        Log($"✅ 角色绑定菜单 ID 列表：{string.Join(", ", roleMenus.Data!)}");

        // ── Step 7：新增用户 ────────────────────────────
        Log("▶ Step 7：新增用户「产品张」");
        var newUser = await Post<ApiResponse<UserResponse>>(
            "/api/system/user",
            new
            {
                UserName = "product_zhang_flow1",
                NickName = "产品张",
                Email = "product.zhang@octopus.com",
                Password = "Prod@123",
                Sex = "1",
                DeptIds = new[] { deptId },
                PostIds = new[] { postId },
                RoleIds = new[] { roleId },
                Status = 1,
                Remark = "Flow1 测试用户"
            });

        newUser.Code.Should().Be(200);
        var userId = newUser.Data!.UserId;
        LogJson($"✅ 用户已创建（UserId={userId}）", newUser.Data);

        // ── Step 8：读取用户详情（验证关联信息完整） ──
        Log("▶ Step 8：读取用户详情，验证部门/职位/角色关联");
        var userDetail = await Get<ApiResponse<UserResponse>>($"/api/system/user/{userId}");

        userDetail.Code.Should().Be(200);
        userDetail.Data!.DeptName.Should().Be("产品部_Flow1");
        userDetail.Data.PostName.Should().Be("产品经理_Flow1");
        userDetail.Data.Roles.Should().Contain("product_flow1");
        LogJson("✅ 用户详情读取成功", new
        {
            userDetail.Data.UserName,
            userDetail.Data.NickName,
            所属部门 = userDetail.Data.DeptName,
            职位 = userDetail.Data.PostName,
            角色 = userDetail.Data.Roles
        });

        // ── Step 9：用新用户登录，验证权限 ─────────────
        Log("▶ Step 9：新用户登录，验证权限返回");
        var newLogin = await Post<ApiResponse<LoginResponse>>(
            "/api/account/login",
            new { UserName = "product_zhang_flow1", Password = "Prod@123" });

        newLogin.Code.Should().Be(200);
        newLogin.Data!.Roles.Should().Contain("product_flow1");
        newLogin.Data.Permissions.Should().NotContain("*:*:*", "非超管不应有通配权限");
        LogJson("✅ 新用户登录成功，权限如下", new
        {
            newLogin.Data.UserName,
            角色列表 = newLogin.Data.Roles,
            权限标识 = newLogin.Data.Permissions.Any()
                ? newLogin.Data.Permissions
                : new List<string> { "（无按钮权限，仅有菜单访问权）" }
        });

        // ── Step 10：在用户列表按部门过滤（数据权限模拟）──
        Log("▶ Step 10：按部门过滤用户列表（模拟「本部门数据权限」）");
        var deptUsers = await Get<ApiResponse<PagedResult<UserResponse>>>(
            $"/api/system/user/list?deptId={deptId}");

        deptUsers.Code.Should().Be(200);
        deptUsers.Data!.Rows.Should().OnlyContain(u => u.DeptId == deptId);
        Log($"✅ 产品部共有 {deptUsers.Data.Total} 名用户：");
        foreach (var u in deptUsers.Data.Rows)
            Log($"   - [{u.UserId}] {u.NickName}（{u.UserName}）角色：{string.Join("/", u.Roles)}");

        Log("\n✅✅✅ Flow 1 全部步骤通过！\n");
    }

    // ══════════════════════════════════════════════════════
    // Flow 2：角色权限管理链路（菜单权限 + 数据权限五种范围）
    // ══════════════════════════════════════════════════════
    [Fact]
    public async Task Flow2_角色权限管理链路()
    {
        Log("═══════════════════════════════════════════════");
        Log("Flow 2：角色权限管理链路");
        Log("═══════════════════════════════════════════════\n");

        // ── Step 1：读取完整菜单树 ──────────────────────
        Log("▶ Step 1：读取完整菜单树");
        var menuTree = await Get<ApiResponse<List<MenuResponse>>>("/api/system/menu/tree");

        menuTree.Code.Should().Be(200);
        var totalMenus = CountMenus(menuTree.Data!);
        Log($"✅ 菜单树读取成功，共 {totalMenus} 个节点");
        Log("   一级菜单：");
        foreach (var m in menuTree.Data!)
            Log($"   ├─ [{m.MenuId}] {m.MenuName}（{m.MenuType}）→ 子菜单 {m.Children.Count} 个");

        // ── Step 2：创建测试角色 ────────────────────────
        Log("\n▶ Step 2：创建「审计员」角色（初始无任何菜单）");
        var role = await Post<ApiResponse<RoleResponse>>(
            "/api/system/role",
            new { RoleName = "审计员_Flow2", RoleKey = "auditor_flow2", RoleSort = 9, DataScope = "1", Status = 1 });

        role.Code.Should().Be(200);
        var roleId = role.Data!.RoleId;
        Log($"✅ 角色已创建：{role.Data.RoleName}（RoleId={roleId}，DataScope={role.Data.DataScope}）");

        // ── Step 3：绑定菜单权限 ────────────────────────
        Log("\n▶ Step 3：为「审计员」绑定菜单权限（用户管理 + 系统监控）");
        var bindMenuIds = new long[] { 1, 2, 3, 100, 101, 110, 111, 112, 113 };
        var bindMenu = await Post<ApiResponse<object?>>(
            "/api/system/role/menu",
            new { RoleId = roleId, MenuIds = bindMenuIds });

        bindMenu.Code.Should().Be(200);
        Log($"✅ 已绑定菜单 ID：{string.Join(", ", bindMenuIds)}");

        // ── Step 4：验证绑定结果 ────────────────────────
        Log("\n▶ Step 4：验证角色菜单绑定结果");
        var verifyMenus = await Get<ApiResponse<List<long>>>($"/api/system/menu/role/{roleId}");

        verifyMenus.Code.Should().Be(200);
        verifyMenus.Data.Should().BeEquivalentTo(bindMenuIds);
        Log($"✅ 验证通过，角色实际持有菜单：{string.Join(", ", verifyMenus.Data!)}");

        // ── Step 5：遍历五种数据权限范围 ───────────────
        Log("\n▶ Step 5：逐一配置五种数据权限范围");
        var scopeMap = new Dictionary<string, string>
        {
            ["1"] = "全部数据（查看所有部门用户）",
            ["2"] = "本部门及子部门数据",
            ["3"] = "本部门数据",
            ["4"] = "仅本人数据",
            ["5"] = "自定义数据（指定部门列表）"
        };

        foreach (var (scope, desc) in scopeMap)
        {
            var deptIds = scope == "5" ? new long[] { 3, 4, 5 } : Array.Empty<long>();
            var bindDept = await Post<ApiResponse<object?>>(
                "/api/system/role/dept",
                new { RoleId = roleId, DataScope = scope, DeptIds = deptIds });

            bindDept.Code.Should().Be(200);

            var verify = await Get<ApiResponse<RoleResponse>>($"/api/system/role/{roleId}");
            verify.Data!.DataScope.Should().Be(scope);

            var deptInfo = scope == "5" ? $"，自定义部门：[{string.Join(",", deptIds)}]" : "";
            Log($"   ✅ DataScope={scope}（{desc}）设置成功{deptInfo}");
        }

        // ── Step 6：清理测试数据 ────────────────────────
        Log("\n▶ Step 6：清理测试角色");
        var del = await _client.DeleteAsync($"/api/system/role/{roleId}");
        var delResult = await del.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        delResult!.Code.Should().Be(200);
        Log($"✅ 角色 {roleId} 已删除");

        Log("\n✅✅✅ Flow 2 全部步骤通过！\n");
    }

    // ══════════════════════════════════════════════════════
    // Flow 3：字典管理链路（类型 → 数据 → 查询）
    // ══════════════════════════════════════════════════════
    [Fact]
    public async Task Flow3_字典管理链路()
    {
        Log("═══════════════════════════════════════════════");
        Log("Flow 3：字典管理链路");
        Log("═══════════════════════════════════════════════\n");

        // ── Step 1：查看现有字典类型 ────────────────────
        Log("▶ Step 1：读取现有字典类型列表");
        var typeList = await Get<ApiResponse<PagedResult<DictTypeResponse>>>("/api/system/dict/type/list");

        typeList.Code.Should().Be(200);
        Log($"✅ 字典类型列表读取成功，共 {typeList.Data!.Total} 个类型：");
        foreach (var t in typeList.Data.Rows)
            Log($"   - [{t.DictId}] {t.DictName}（{t.DictType}）");

        // ── Step 2：查询字典数据（性别）──────────────────
        Log("\n▶ Step 2：查询「用户性别」字典数据");
        var sexDict = await Get<ApiResponse<List<DictDataResponse>>>("/api/system/dict/data/type/sys_user_sex");

        sexDict.Code.Should().Be(200);
        Log($"✅ 性别字典数据（共 {sexDict.Data!.Count} 条）：");
        foreach (var d in sexDict.Data)
            Log($"   - {d.DictLabel} = {d.DictValue}（默认：{(d.IsDefault ? "是" : "否")}）");

        // ── Step 3：新增字典类型 ────────────────────────
        Log("\n▶ Step 3：新增「审批状态」字典类型");
        var newType = await Post<ApiResponse<DictTypeResponse>>(
            "/api/system/dict/type",
            new { DictName = "审批状态_Flow3", DictType = "oa_approve_status_flow3", Status = 1, Remark = "Flow3测试" });

        newType.Code.Should().Be(200);
        var dictTypeCode = newType.Data!.DictType;
        LogJson($"✅ 字典类型已创建", new
        {
            newType.Data.DictId,
            newType.Data.DictName,
            类型编码 = newType.Data.DictType
        });

        // ── Step 4：为新类型添加字典数据 ───────────────
        Log("\n▶ Step 4：添加「审批状态」字典数据");
        var dataItems = new[]
        {
            new { DictType = dictTypeCode, DictLabel = "待审批", DictValue = "0", DictSort = 1, Status = 1, IsDefault = true  },
            new { DictType = dictTypeCode, DictLabel = "审批中", DictValue = "1", DictSort = 2, Status = 1, IsDefault = false },
            new { DictType = dictTypeCode, DictLabel = "已通过", DictValue = "2", DictSort = 3, Status = 1, IsDefault = false },
            new { DictType = dictTypeCode, DictLabel = "已拒绝", DictValue = "3", DictSort = 4, Status = 1, IsDefault = false },
        };

        foreach (var item in dataItems)
        {
            var addData = await Post<ApiResponse<DictDataResponse>>("/api/system/dict/data", item);
            addData.Code.Should().Be(200);
            Log($"   ✅ 已添加：{item.DictLabel} = {item.DictValue}");
        }

        // ── Step 5：按类型查询验证 ──────────────────────
        Log($"\n▶ Step 5：按类型编码「{dictTypeCode}」查询字典数据");
        var queryResult = await Get<ApiResponse<List<DictDataResponse>>>(
            $"/api/system/dict/data/type/{dictTypeCode}");

        queryResult.Code.Should().Be(200);
        queryResult.Data!.Should().HaveCount(4);
        Log($"✅ 字典数据查询成功，共 {queryResult.Data.Count} 条：");
        foreach (var d in queryResult.Data)
            Log($"   [{d.DictSort}] {d.DictLabel} → {d.DictValue}（默认：{(d.IsDefault ? "✓" : "✗")}）");

        Log("\n✅✅✅ Flow 3 全部步骤通过！\n");
    }

    // ══════════════════════════════════════════════════════
    // Flow 4：用户禁用 → 登录拒绝 → 重新启用 → 重置密码
    // ══════════════════════════════════════════════════════
    [Fact]
    public async Task Flow4_用户状态管理链路()
    {
        Log("═══════════════════════════════════════════════");
        Log("Flow 4：用户状态管理（禁用/启用/重置密码）");
        Log("═══════════════════════════════════════════════\n");

        // ── Step 1：创建测试用户 ────────────────────────
        Log("▶ Step 1：创建测试用户「赵六」");
        var newUser = await Post<ApiResponse<UserResponse>>(
            "/api/system/user",
            new
            {
                UserName = "zhao6_flow4",
                NickName = "赵六",
                Email = "zhao6@octopus.com",
                Password = "Zhao@123",
                DeptIds = new[] { 3L },
                Status = 1
            });

        newUser.Code.Should().Be(200);
        var userId = newUser.Data!.UserId;
        LogJson($"✅ 用户已创建", newUser.Data);

        // ── Step 2：正常登录 ────────────────────────────
        Log("\n▶ Step 2：赵六正常登录");
        var loginOk = await Post<ApiResponse<LoginResponse>>(
            "/api/account/login",
            new { UserName = "zhao6_flow4", Password = "Zhao@123" });

        loginOk.Code.Should().Be(200);
        Log($"✅ 登录成功，欢迎 {loginOk.Data!.NickName}！角色：{string.Join(", ", loginOk.Data.Roles)}");

        // ── Step 3：禁用用户 ────────────────────────────
        Log("\n▶ Step 3：管理员禁用「赵六」账号");
        var disable = await Put<ApiResponse<object>>(
            "/api/system/user/status",
            new { UserId = userId, Status = 0 });

        disable.Code.Should().Be(200);
        Log($"✅ 用户状态已设为禁用（Status=0）");

        // ── Step 4：禁用后尝试登录 ──────────────────────
        Log("\n▶ Step 4：赵六尝试登录（应被拒绝）");
        var loginFail = await Post<ApiResponse<LoginResponse>>(
            "/api/account/login",
            new { UserName = "zhao6_flow4", Password = "Zhao@123" });

        loginFail.Code.Should().Be(403);
        Log($"✅ 登录被拒绝！原因：{loginFail.Msg}");

        // ── Step 5：重新启用 ────────────────────────────
        Log("\n▶ Step 5：管理员重新启用「赵六」");
        var enable = await Put<ApiResponse<object>>(
            "/api/system/user/status",
            new { UserId = userId, Status = 1 });

        enable.Code.Should().Be(200);
        Log($"✅ 用户已重新启用（Status=1）");

        // ── Step 6：重置密码 ────────────────────────────
        Log("\n▶ Step 6：管理员重置「赵六」密码");
        var reset = await Put<ApiResponse<object>>(
            "/api/system/user/resetPwd",
            new { UserId = userId, NewPassword = "NewZhao@456" });

        reset.Code.Should().Be(200);
        Log("✅ 密码已重置");

        // ── Step 7：用新密码登录 ────────────────────────
        Log("\n▶ Step 7：赵六用新密码登录");
        var loginNew = await Post<ApiResponse<LoginResponse>>(
            "/api/account/login",
            new { UserName = "zhao6_flow4", Password = "NewZhao@456" });

        loginNew.Code.Should().Be(200);
        Log($"✅ 新密码登录成功！欢迎回来，{loginNew.Data!.NickName}！");

        // ── Step 8：用旧密码登录（应失败） ──────────────
        Log("\n▶ Step 8：赵六尝试用旧密码登录（应失败）");
        var loginOld = await Post<ApiResponse<LoginResponse>>(
            "/api/account/login",
            new { UserName = "zhao6_flow4", Password = "Zhao@123" });

        loginOld.Code.Should().Be(401);
        Log($"✅ 旧密码已失效，登录拒绝。原因：{loginOld.Msg}");

        // ── Step 9：清理 ────────────────────────────────
        Log("\n▶ Step 9：清理测试数据");
        await _client.DeleteAsync($"/api/system/user/{userId}");
        Log($"✅ 测试用户 {userId} 已删除");

        Log("\n✅✅✅ Flow 4 全部步骤通过！\n");
    }

    // ── 辅助：递归统计部门数量 ───────────────────────────
    private static int CountDepts(List<DeptResponse> depts) =>
        depts.Count + depts.Sum(d => CountDepts(d.Children));

    private static int CountMenus(List<MenuResponse> menus) =>
        menus.Count + menus.Sum(m => CountMenus(m.Children));
}
