using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OctopusUMC.Api.DTOs;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit.Abstractions;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 多公司场景演示：一名员工管理多家公司的多个部门
///
/// 种子数据组织架构：
///
///   公司A ── 章鱼科技有限公司 (DeptId=1)
///            ├── 总裁办  (DeptId=2)  →  admin
///            ├── 技术部  (DeptId=3)  →  zhangsan(主) + lisi
///            ├── 市场部  (DeptId=4)  →  wangwu(禁用)
///            └── 行政部  (DeptId=5)  →  editor
///
///   公司B ── 海星科技有限公司 (DeptId=9)
///            ├── 技术部  (DeptId=10) →  zhangsan(兼) + zhaoliu(主)
///            └── 市场部  (DeptId=11) →  (暂无人员)
///
///   跨公司兼职：
///     zhangsan (UserId=2)
///       主部门：A公司技术部(DeptId=3)  职位：技术总监   IsPrimary=true
///       兼职：  B公司技术部(DeptId=10) 职位：工程师     IsPrimary=false
///
///   数据权限查询结果预期：
///     ?deptId=3  → A公司技术部 → [zhangsan, lisi]          zhangsan.deptId=3（主部门）
///     ?deptId=10 → B公司技术部 → [zhangsan, zhaoliu]       zhangsan.deptId=3（显示主部门）
///     ?deptId=11 → B公司市场部 → []
/// </summary>
public class MultiCompanyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    // UnsafeRelaxedJsonEscaping 让中文字符直接输出，而非 \uXXXX 转义
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public MultiCompanyTests(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _output = output;
    }

    // ──────────────────────────────────────────────────────────────
    // Flow 5：多公司 + 跨公司数据权限演示
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Flow5_MultiCompany_CrossDeptDataPermission()
    {
        _output.WriteLine("═══════════════════════════════════════════════════════════════");
        _output.WriteLine("  Flow 5：多公司场景 — 一个用户管理多家公司的多个部门");
        _output.WriteLine("═══════════════════════════════════════════════════════════════");

        // ── Step 1：查询部门树，确认两家公司都在 ─────────────────────
        _output.WriteLine("\n【Step 1】查询完整部门树（含两家公司）");

        var treeResp = await _client.GetAsync("/api/system/dept/tree");
        var tree = await treeResp.Content.ReadFromJsonAsync<ApiResponse<List<DeptResponse>>>();
        tree!.Code.Should().Be(200);

        var companyA = tree.Data!.FirstOrDefault(d => d.DeptName.Contains("章鱼"));
        var companyB = tree.Data!.FirstOrDefault(d => d.DeptName.Contains("海星"));
        companyA.Should().NotBeNull("章鱼科技应在部门树根节点");
        companyB.Should().NotBeNull("海星科技应在部门树根节点");

        _output.WriteLine($"  ✓ 公司A：{companyA!.DeptName} (DeptId={companyA.DeptId})");
        foreach (var child in companyA.Children)
            _output.WriteLine($"      ├─ {child.DeptName} (DeptId={child.DeptId})");

        _output.WriteLine($"  ✓ 公司B：{companyB!.DeptName} (DeptId={companyB.DeptId})");
        foreach (var child in companyB.Children)
            _output.WriteLine($"      ├─ {child.DeptName} (DeptId={child.DeptId})");

        // ── Step 2：查询 zhangsan 的所有任职记录 ─────────────────────
        _output.WriteLine("\n【Step 2】查询 zhangsan 的全部任职信息（跨公司兼职）");

        var deptsResp = await _client.GetAsync("/api/system/user/2/depts");
        var deptsResult = await deptsResp.Content.ReadFromJsonAsync<JsonElement>();
        var deptsJson = JsonSerializer.Serialize(deptsResult, _json);
        _output.WriteLine(deptsJson);

        // 验证 zhangsan 同时在两家公司的技术部
        deptsJson.Should().Contain("章鱼科技",  "zhangsan 的主部门在章鱼科技");
        deptsJson.Should().Contain("海星科技",  "zhangsan 兼职部门在海星科技");
        deptsJson.Should().Contain("true",       "有一条 isPrimary=true 的主部门记录");
        deptsJson.Should().Contain("false",      "有一条 isPrimary=false 的兼职记录");

        // ── Step 3：数据权限查询 — A公司A部门（章鱼技术部） ─────────
        _output.WriteLine("\n【Step 3】数据权限查询 — A公司技术部 (?deptId=3)");
        _output.WriteLine("  预期：[zhangsan(主), lisi]，两人均是章鱼技术部成员");

        var respA = await _client.GetAsync("/api/system/user/list?deptId=3");
        var resultA = await respA.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
        resultA!.Code.Should().Be(200);

        _output.WriteLine($"  查询结果（共 {resultA.Data!.Total} 人）：");
        foreach (var u in resultA.Data.Rows)
            _output.WriteLine($"    - {u.UserName} ({u.NickName}) | 主部门={u.DeptName}(DeptId={u.DeptId}) | 状态={u.Status}");

        resultA.Data.Rows.Should().Contain(u => u.UserName == "zhangsan", "zhangsan 属于章鱼技术部（主部门）");
        resultA.Data.Rows.Should().Contain(u => u.UserName == "lisi",     "lisi 属于章鱼技术部");
        resultA.Data.Rows.Should().NotContain(u => u.UserName == "zhaoliu", "zhaoliu 不在章鱼技术部，应被过滤");
        resultA.Data.Rows.Should().NotContain(u => u.UserName == "admin",   "admin 在总裁办，不在技术部");

        // zhangsan 返回的 deptId 是他的主部门（章鱼技术部=3），而不是兼职部门
        var zhangsanA = resultA.Data.Rows.First(u => u.UserName == "zhangsan");
        zhangsanA.DeptId.Should().Be(3, "zhangsan 的主部门是章鱼技术部(DeptId=3)，即使他兼职海星，响应显示主部门");
        _output.WriteLine($"  ✓ zhangsan 显示主部门 DeptId={zhangsanA.DeptId}（{zhangsanA.DeptName}），符合预期");

        // ── Step 4：数据权限查询 — B公司B部门（海星技术部） ─────────
        _output.WriteLine("\n【Step 4】数据权限查询 — B公司技术部 (?deptId=10)");
        _output.WriteLine("  预期：[zhangsan(兼), zhaoliu(主)]");
        _output.WriteLine("  注意：zhangsan 出现在这里，但他的响应主部门仍显示章鱼技术部");

        var respB = await _client.GetAsync("/api/system/user/list?deptId=10");
        var resultB = await respB.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
        resultB!.Code.Should().Be(200);

        _output.WriteLine($"  查询结果（共 {resultB.Data!.Total} 人）：");
        foreach (var u in resultB.Data.Rows)
            _output.WriteLine($"    - {u.UserName} ({u.NickName}) | 主部门={u.DeptName}(DeptId={u.DeptId}) | 来源={"DeptId="+u.DeptId}");

        resultB.Data.Rows.Should().Contain(u => u.UserName == "zhangsan", "zhangsan 兼职海星技术部，出现在 B 公司查询结果");
        resultB.Data.Rows.Should().Contain(u => u.UserName == "zhaoliu",  "zhaoliu 是海星技术部专属员工");
        resultB.Data.Rows.Should().NotContain(u => u.UserName == "lisi",  "lisi 不在海星技术部，应被过滤");
        resultB.Data.Rows.Should().NotContain(u => u.UserName == "admin", "admin 不在海星技术部");

        // 关键验证：zhangsan 在 B 公司查询中仍显示主部门（章鱼技术部=3）
        var zhangsanB = resultB.Data.Rows.First(u => u.UserName == "zhangsan");
        zhangsanB.DeptId.Should().Be(3, "zhangsan 主部门是章鱼技术部(DeptId=3)，即使通过海星技术部(DeptId=10)筛选到他");
        _output.WriteLine($"  ✓ zhangsan 通过 B公司查询找到，但主部门仍显示 DeptId={zhangsanB.DeptId}（{zhangsanB.DeptName}）");
        _output.WriteLine("  ✓ 这是正确行为：数据权限决定「能看到谁」，主部门决定「显示什么归属」");

        // ── Step 5：查询 B公司市场部，应为空 ──────────────────────
        _output.WriteLine("\n【Step 5】数据权限查询 — B公司市场部 (?deptId=11)");
        _output.WriteLine("  预期：空（海星市场部尚无成员）");

        var respEmpty = await _client.GetAsync("/api/system/user/list?deptId=11");
        var resultEmpty = await respEmpty.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
        resultEmpty!.Code.Should().Be(200);
        resultEmpty.Data!.Total.Should().Be(0, "海星市场部暂无成员");
        resultEmpty.Data.Rows.Should().BeEmpty();
        _output.WriteLine("  ✓ 海星市场部：0 人（正确）");

        // ── Step 6：找出同时在两家公司的员工（跨公司兼职名单） ───
        _output.WriteLine("\n【Step 6】统计跨公司兼职名单");
        _output.WriteLine("  方法：A公司查询结果 ∩ B公司查询结果 = 跨公司兼职人员");

        var usersInA = resultA.Data.Rows.Select(u => u.UserName).ToHashSet();
        var usersInB = resultB.Data.Rows.Select(u => u.UserName).ToHashSet();
        var crossCompany = usersInA.Intersect(usersInB).ToList();

        _output.WriteLine($"  A公司技术部人员：[{string.Join(", ", usersInA)}]");
        _output.WriteLine($"  B公司技术部人员：[{string.Join(", ", usersInB)}]");
        _output.WriteLine($"  跨公司兼职人员： [{string.Join(", ", crossCompany)}]");

        crossCompany.Should().Contain("zhangsan", "zhangsan 同时在 A 和 B 两家公司的技术部");
        crossCompany.Should().HaveCount(1, "目前只有 zhangsan 一人跨公司兼职");
        _output.WriteLine("  ✓ 跨公司兼职验证通过");

        // ── Step 7：新增一名员工到 B 公司市场部，验证数据隔离 ─────
        _output.WriteLine("\n【Step 7】在 B公司市场部新增员工，验证公司数据隔离");

        var createReq = new
        {
            UserName = "haixin_sales", NickName = "海星-销售小王", Email = "sales@haixing.com",
            Password = "Test@123", DeptIds = new[] { 11 }, Status = 1, Sex = "1", RoleIds = new[] { 2 }
        };
        var createResp = await (await _client.PostAsJsonAsync("/api/system/user", createReq))
            .Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        createResp!.Code.Should().Be(200);
        var newUid = createResp.Data!.UserId;
        _output.WriteLine($"  ✓ 新增员工成功：{createResp.Data.UserName} | DeptId={createResp.Data.DeptId} | UserId={newUid}");

        // 查询 B 公司市场部现在应该有这个人
        var respBMkt = await _client.GetAsync("/api/system/user/list?deptId=11");
        var resultBMkt = await respBMkt.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
        resultBMkt!.Data!.Total.Should().Be(1, "海星市场部新增了一名员工");
        resultBMkt.Data.Rows.First().UserName.Should().Be("haixin_sales");
        _output.WriteLine($"  ✓ 海星市场部现有员工：{string.Join(", ", resultBMkt.Data.Rows.Select(u => u.UserName))}");

        // 确认 B 市场部员工不出现在 A 技术部
        var respACheck = await _client.GetAsync("/api/system/user/list?deptId=3");
        var resultACheck = await respACheck.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
        resultACheck!.Data!.Rows.Should().NotContain(u => u.UserName == "haixin_sales",
            "haixin_sales 是 B公司市场部员工，不应出现在 A公司技术部的查询结果");
        _output.WriteLine("  ✓ 数据隔离验证：haixin_sales 不在 A 公司技术部查询结果中");

        // 清理
        await _client.DeleteAsync($"/api/system/user/{newUid}");
        _output.WriteLine($"\n  [清理] 删除临时员工 UserId={newUid}");

        // ── 汇总 ─────────────────────────────────────────────────
        _output.WriteLine("\n═══════════════════════════════════════════════════════════════");
        _output.WriteLine("  Flow 5 通过 ✓");
        _output.WriteLine("  验证要点：");
        _output.WriteLine("  1. sys_user_dept 支持一人多部门（跨公司兼职）");
        _output.WriteLine("  2. ?deptId 过滤基于 UserDept 关联表，不依赖 User.DeptId 字段");
        _output.WriteLine("  3. UserResponse.deptId 始终返回主部门(IsPrimary=true)，不随查询条件变化");
        _output.WriteLine("  4. 跨公司员工出现在多家公司的数据权限查询结果中");
        _output.WriteLine("  5. 新员工加入 B 公司后，A 公司查询结果不受影响（数据隔离）");
        _output.WriteLine("═══════════════════════════════════════════════════════════════");
    }

    // ──────────────────────────────────────────────────────────────
    // 独立断言测试（不依赖日志输出，CI 友好）
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeptFilter_CompanyA_TechDept_ReturnsZhangsanAndLisi()
    {
        var resp = await _client.GetAsync("/api/system/user/list?deptId=3");
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();

        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().Contain(u => u.UserName == "zhangsan");
        result.Data.Rows.Should().Contain(u => u.UserName == "lisi");
        result.Data.Rows.Should().NotContain(u => u.UserName == "zhaoliu");
    }

    [Fact]
    public async Task DeptFilter_CompanyB_TechDept_ReturnsZhangsanAndZhaoliu()
    {
        var resp = await _client.GetAsync("/api/system/user/list?deptId=10");
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();

        result!.Code.Should().Be(200);
        result.Data!.Rows.Should().Contain(u => u.UserName == "zhangsan",
            "zhangsan 兼职 B公司技术部，应出现在 deptId=10 的查询结果");
        result.Data.Rows.Should().Contain(u => u.UserName == "zhaoliu",
            "zhaoliu 是 B公司技术部专属员工");
        result.Data.Rows.Should().NotContain(u => u.UserName == "lisi",
            "lisi 仅在 A公司技术部，不在 B公司");
    }

    [Fact]
    public async Task DeptFilter_CompanyB_MarketDept_IsEmpty()
    {
        var resp = await _client.GetAsync("/api/system/user/list?deptId=11");
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();

        result!.Code.Should().Be(200);
        result.Data!.Total.Should().Be(0, "海星市场部无成员");
    }

    [Fact]
    public async Task ZhangsanDisplaysDeptA_WhenQueriedViaCompanyB()
    {
        // 关键行为验证：通过 B公司部门查到 zhangsan，
        // 但响应里 deptId/deptName 仍然显示 A公司（主部门），不会变成 B 公司
        var resp = await _client.GetAsync("/api/system/user/list?deptId=10");
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();

        var zhangsan = result!.Data!.Rows.First(u => u.UserName == "zhangsan");
        zhangsan.DeptId.Should().Be(3,
            "主部门是章鱼技术部(DeptId=3)，即使通过海星技术部(DeptId=10)筛选，显示仍是主部门");
        zhangsan.DeptName.Should().Be("技术部");
    }

    [Fact]
    public async Task GetUserDepts_Zhangsan_ShowsBothCompanies()
    {
        // GET /api/system/user/2/depts 返回 zhangsan 的所有任职记录
        var resp = await _client.GetAsync("/api/system/user/2/depts");
        var result = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>();

        result!.Code.Should().Be(200);
        var json = JsonSerializer.Serialize(result.Data, _json);

        json.Should().Contain("章鱼科技", "zhangsan 主部门属于章鱼科技");
        json.Should().Contain("海星科技", "zhangsan 兼职部门属于海星科技");
        // WriteIndented=true 格式为 "deptId": 3（冒号后有空格）
        json.Should().Contain("\"deptId\": 3",  "章鱼技术部 DeptId=3");
        json.Should().Contain("\"deptId\": 10", "海星技术部 DeptId=10");
        json.Should().Contain("\"isPrimary\": true",  "有主部门记录");
        json.Should().Contain("\"isPrimary\": false", "有兼职记录");
    }
}
