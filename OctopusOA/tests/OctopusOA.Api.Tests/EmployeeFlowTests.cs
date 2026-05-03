using FluentAssertions;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 职员档案集成测试
///
/// 种子数据：
///   ① 王小明 — temp（HR 刚建档）
///   ② 李小红 — pending（已填 H5，待审核）
///   ③ 赵小强 — rejected（已拒绝）
/// </summary>
public class EmployeeFlowTests : IClassFixture<OATestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _out;

    public EmployeeFlowTests(OATestFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _out = output;
    }

    private void Log(string msg) => _out.WriteLine(msg);

    // ══════════════════════════════════════════
    // Flow 1：创建 → H5 填写 → 确认入职
    // ══════════════════════════════════════════
    [Fact]
    public async Task Flow1_Create_H5Fill_Confirm()
    {
        Log("═══ Flow 1：创建 → H5 填写 → 确认入职 ═══\n");

        // Step 1：HR 创建临时档案
        var createResp = await PostJson("/api/employee", new
        {
            Name = "测试员工_Flow1", Gender = "male", Phone = "13700000001",
            ApplyPosition = "后端工程师", ApplyDeptId = 3, ApplyDeptName = "技术部",
            Education = "本科", GraduateSchool = "清华大学", Major = "计算机",
        });
        createResp.GetProperty("code").GetInt32().Should().Be(200);
        var data = createResp.GetProperty("data");
        var empId = data.GetProperty("employeeId").GetInt64();
        var h5Token = data.GetProperty("h5Token").GetString();
        data.GetProperty("status").GetString().Should().Be("temp");
        Log($"  ✅ 创建成功：EmployeeId={empId}，H5Token={h5Token}");

        // Step 2：H5 获取预填信息
        var h5Get = await GetJson($"/api/h5/onboard/{h5Token}");
        h5Get.GetProperty("code").GetInt32().Should().Be(200);
        h5Get.GetProperty("data").GetProperty("name").GetString().Should().Be("测试员工_Flow1");
        h5Get.GetProperty("data").GetProperty("alreadyFilled").GetBoolean().Should().BeFalse();
        Log("  ✅ H5 获取预填信息成功");

        // Step 3：H5 提交入职信息
        var h5Submit = await PostJson($"/api/h5/onboard/{h5Token}", new
        {
            IdCardNo = "440101199901010011", CurrentAddress = "广州市天河区",
            PoliticalStatus = "群众", MaritalStatus = "未婚", BankName = "工商银行", BankAccount = "6222001234567890",
            Educations = new[] { new { School = "清华大学", Education = "本科", Major = "计算机", StartDate = "2017-09", EndDate = "2021-06" } },
            EmergencyContacts = new[] { new { Name = "张某某", Relation = "父亲", Phone = "13800001111" } },
        });
        h5Submit.GetProperty("code").GetInt32().Should().Be(200);
        Log("  ✅ H5 提交成功");

        // Step 4：验证状态变为 pending
        var detail = await GetJson($"/api/employee/{empId}");
        detail.GetProperty("data").GetProperty("status").GetString().Should().Be("pending");
        detail.GetProperty("data").GetProperty("idCardNo").GetString().Should().Be("440101199901010011");
        detail.GetProperty("data").GetProperty("educations").GetArrayLength().Should().Be(1);
        Log("  ✅ 状态变为 pending，H5 数据已保存");

        // Step 5：HR 确认入职
        var confirm = await PutJson($"/api/employee/{empId}/confirm");
        confirm.GetProperty("code").GetInt32().Should().Be(200);

        var final = await GetJson($"/api/employee/{empId}");
        final.GetProperty("data").GetProperty("status").GetString().Should().Be("active");
        Log("  ✅ 确认入职成功，状态变为 active");

        Log("\n✅✅✅ Flow 1 全部通过！\n");
    }

    // ══════════════════════════════════════════
    // Flow 2：创建 → H5 填写 → 拒绝
    // ══════════════════════════════════════════
    [Fact]
    public async Task Flow2_Create_H5Fill_Reject()
    {
        Log("═══ Flow 2：创建 → H5 填写 → 拒绝 ═══\n");

        var createResp = await PostJson("/api/employee", new
        {
            Name = "拒绝测试_Flow2", Gender = "female", Phone = "13700000002",
            ApplyPosition = "测试工程师",
        });
        var empId = createResp.GetProperty("data").GetProperty("employeeId").GetInt64();
        var h5Token = createResp.GetProperty("data").GetProperty("h5Token").GetString();

        // H5 提交
        await PostJson($"/api/h5/onboard/{h5Token}", new { CurrentAddress = "深圳市南山区" });

        // HR 拒绝
        var reject = await PutJson($"/api/employee/{empId}/reject");
        reject.GetProperty("code").GetInt32().Should().Be(200);

        var final = await GetJson($"/api/employee/{empId}");
        final.GetProperty("data").GetProperty("status").GetString().Should().Be("rejected");
        Log("  ✅ 状态变为 rejected");

        Log("\n✅✅✅ Flow 2 全部通过！\n");
    }

    // ══════════════════════════════════════════
    // 删除：仅 temp/rejected 可删
    // ══════════════════════════════════════════
    [Fact]
    public async Task Delete_TempEmployee_Success()
    {
        // 种子数据 EmployeeId=1 是 temp
        var resp = await _client.DeleteAsync("/api/employee/1");
        var json = await ReadJson(resp);
        json.GetProperty("code").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task Delete_PendingEmployee_Fails()
    {
        // 种子数据 EmployeeId=2 是 pending
        var resp = await _client.DeleteAsync("/api/employee/2");
        var json = await ReadJson(resp);
        json.GetProperty("code").GetInt32().Should().Be(500);
    }

    // ══════════════════════════════════════════
    // H5 token 验证
    // ══════════════════════════════════════════
    [Fact]
    public async Task H5_InvalidToken_Returns404()
    {
        var resp = await GetJson("/api/h5/onboard/invalid_token_xxx");
        resp.GetProperty("code").GetInt32().Should().Be(404);
    }

    [Fact]
    public async Task H5_AlreadyFilledToken_CannotResubmit()
    {
        // 种子 EmployeeId=2 的 token 已填写过，状态是 pending
        var resp = await PostJson("/api/h5/onboard/h5_token_002", new { CurrentAddress = "二次提交" });
        resp.GetProperty("code").GetInt32().Should().Be(500);
    }

    // ══════════════════════════════════════════
    // 种子数据验证
    // ══════════════════════════════════════════
    [Fact]
    public async Task SeedData_ListReturnsAll()
    {
        var resp = await GetJson("/api/employee/list");
        resp.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task SeedData_FilterByStatus()
    {
        var resp = await GetJson("/api/employee/list?status=pending");
        var rows = resp.GetProperty("data").GetProperty("rows");
        rows.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SeedData_PendingEmployee_HasEducations()
    {
        var resp = await GetJson("/api/employee/2");
        var data = resp.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("pending");
        data.GetProperty("educations").GetArrayLength().Should().Be(2);
        data.GetProperty("workHistories").GetArrayLength().Should().Be(2);
        data.GetProperty("families").GetArrayLength().Should().Be(2);
        data.GetProperty("emergencyContacts").GetArrayLength().Should().Be(1);
    }

    // ── 辅助方法 ────────────────────────────

    private async Task<JsonElement> ReadJson(HttpResponseMessage resp) =>
        JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());

    private async Task<JsonElement> GetJson(string url) =>
        await ReadJson(await _client.GetAsync(url));

    private async Task<JsonElement> PostJson(string url, object body) =>
        await ReadJson(await _client.PostAsJsonAsync(url, body));

    private async Task<JsonElement> PutJson(string url) =>
        await ReadJson(await _client.PutAsJsonAsync(url, new { }));
}
