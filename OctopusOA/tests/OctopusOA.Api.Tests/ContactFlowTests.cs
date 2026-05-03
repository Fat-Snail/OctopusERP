using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 通讯录测试
///
/// 种子数据：
///   部门：章鱼科技(1)/海星科技(9) 两家公司，共 11 个部门
///   用户-部门：
///     admin(1)     → 总裁办(2)
///     zhangsan(2)  → A公司技术部(3) + B公司技术部(10) [兼职]
///     lisi(3)      → A公司技术部(3)
/// </summary>
public class ContactFlowTests : IClassFixture<OATestFactory>
{
    private readonly HttpClient _client;
    private const string SharedSecret = "octopus-sync-secret-key-2026";

    public ContactFlowTests(OATestFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    private static string ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    // ═══ 部门树 ═══

    [Fact]
    public async Task GetDeptTree_ReturnsTwoCompanies()
    {
        var resp = await _client.GetAsync("/api/contact/dept/tree");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());

        json.GetProperty("code").GetInt32().Should().Be(200);
        var data = json.GetProperty("data");
        data.GetArrayLength().Should().Be(2, "章鱼科技 + 海星科技 两家公司");
    }

    [Fact]
    public async Task GetDeptTree_HasUserCounts()
    {
        var resp = await _client.GetAsync("/api/contact/dept/tree");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());

        var data = json.GetProperty("data");
        var a = data.EnumerateArray().First(d => d.GetProperty("deptName").GetString()!.Contains("章鱼"));
        // 章鱼科技（含子部门）应有 admin + zhangsan + lisi = 3 人
        a.GetProperty("userCount").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    // ═══ 员工列表 ═══

    [Fact]
    public async Task GetUsers_NoFilter_ReturnsAllActive()
    {
        var resp = await _client.GetAsync("/api/contact/users");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());

        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetUsers_FilterByDept_ReturnsOnlyDeptMembers()
    {
        // deptId=3（章鱼技术部）应包含 zhangsan + lisi
        var resp = await _client.GetAsync("/api/contact/users?deptId=3");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());

        var rows = json.GetProperty("data").GetProperty("rows");
        rows.EnumerateArray().Should().Contain(u => u.GetProperty("userName").GetString() == "zhangsan");
        rows.EnumerateArray().Should().Contain(u => u.GetProperty("userName").GetString() == "lisi");
        rows.EnumerateArray().Should().NotContain(u => u.GetProperty("userName").GetString() == "admin");
    }

    [Fact]
    public async Task GetUsers_SearchByKeyword_ReturnsMatchingUsers()
    {
        var resp = await _client.GetAsync("/api/contact/users?keyword=张");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());

        var rows = json.GetProperty("data").GetProperty("rows");
        rows.EnumerateArray().Should().Contain(u => u.GetProperty("nickName").GetString() == "张三");
    }

    [Fact]
    public async Task GetUsers_CompanyRootDept_IncludesChildren()
    {
        // 查 deptId=1（章鱼科技根）应包含所有子部门的用户
        var resp = await _client.GetAsync("/api/contact/users?deptId=1");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());

        var rows = json.GetProperty("data").GetProperty("rows");
        var names = rows.EnumerateArray().Select(u => u.GetProperty("userName").GetString()).ToList();
        names.Should().Contain("admin");
        names.Should().Contain("zhangsan");
        names.Should().Contain("lisi");
    }

    [Fact]
    public async Task GetUser_Detail_HasDeptInfo()
    {
        // zhangsan(2) 应有两条 dept 记录（主 + 兼职）
        var resp = await _client.GetAsync("/api/contact/user/2");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());

        json.GetProperty("code").GetInt32().Should().Be(200);
        var data = json.GetProperty("data");
        data.GetProperty("userName").GetString().Should().Be("zhangsan");
        data.GetProperty("depts").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetUser_NotFound_Returns404()
    {
        var resp = await _client.GetAsync("/api/contact/user/9999");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        json.GetProperty("code").GetInt32().Should().Be(404);
    }

    // ═══ UMC 部门同步 ═══

    [Fact]
    public async Task DeptSync_ValidSignature_AddsNewDept()
    {
        var payload = new { Action = "upsert", DeptId = 100, ParentId = 1, DeptName = "新部门_测试", OrderNum = 99, Status = 1 };
        var json = JsonSerializer.Serialize(payload);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/contact/dept/sync");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(json)}");

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 验证部门已出现在树中
        var treeResp = await _client.GetAsync("/api/contact/dept/tree");
        var treeJson = await treeResp.Content.ReadAsStringAsync();
        treeJson.Should().Contain("新部门_测试");
    }

    [Fact]
    public async Task DeptSync_InvalidSignature_Returns401()
    {
        var payload = new { Action = "upsert", DeptId = 200, ParentId = 1, DeptName = "伪造部门", OrderNum = 1, Status = 1 };
        var json = JsonSerializer.Serialize(payload);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/contact/dept/sync");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Headers.Add("X-Sync-Signature", "sha256=invalid_signature");

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeptSync_DeleteAction_RemovesDept()
    {
        // 先新增
        var addPayload = new { Action = "upsert", DeptId = 300, ParentId = 1, DeptName = "待删除部门", OrderNum = 99, Status = 1 };
        var addJson = JsonSerializer.Serialize(addPayload);
        var addReq = new HttpRequestMessage(HttpMethod.Post, "/api/contact/dept/sync");
        addReq.Content = new StringContent(addJson, Encoding.UTF8, "application/json");
        addReq.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(addJson)}");
        await _client.SendAsync(addReq);

        // 再删除
        var delPayload = new { Action = "delete", DeptId = 300, ParentId = 1, DeptName = "", OrderNum = 0, Status = 0 };
        var delJson = JsonSerializer.Serialize(delPayload);
        var delReq = new HttpRequestMessage(HttpMethod.Post, "/api/contact/dept/sync");
        delReq.Content = new StringContent(delJson, Encoding.UTF8, "application/json");
        delReq.Headers.Add("X-Sync-Signature", $"sha256={ComputeHmac(delJson)}");
        var resp = await _client.SendAsync(delReq);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 验证已不在树中
        var treeResp = await _client.GetAsync("/api/contact/dept/tree");
        var treeJson = await treeResp.Content.ReadAsStringAsync();
        treeJson.Should().NotContain("待删除部门");
    }
}
