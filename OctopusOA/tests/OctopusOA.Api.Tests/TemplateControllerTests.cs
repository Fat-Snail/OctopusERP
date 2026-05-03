using FluentAssertions;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>审批模板 CRUD 测试</summary>
public class TemplateControllerTests : IClassFixture<OATestFactory>
{
    private readonly HttpClient _client;

    public TemplateControllerTests(OATestFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetList_ReturnsSeedTemplates()
    {
        var resp = await _client.GetAsync("/api/approval/template/list");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetById_LeaveTemplate_HasNodes()
    {
        var json = await GetJson("/api/approval/template/1");
        json.GetProperty("code").GetInt32().Should().Be(200);
        var data = json.GetProperty("data");
        data.GetProperty("templateCode").GetString().Should().Be("leave");
        data.GetProperty("nodes").GetArrayLength().Should().Be(2, "请假模板有 2 个节点");
    }

    [Fact]
    public async Task GetById_ExpenseTemplate_Has3Nodes()
    {
        var json = await GetJson("/api/approval/template/2");
        var data = json.GetProperty("data");
        data.GetProperty("templateCode").GetString().Should().Be("expense");
        data.GetProperty("nodes").GetArrayLength().Should().Be(3, "报销模板有 3 个节点");
    }

    [Fact]
    public async Task Create_NewTemplate_Success()
    {
        var resp = await _client.PostAsJsonAsync("/api/approval/template", new
        {
            TemplateName = "加班审批",
            TemplateCode = "overtime_test",
            Description = "测试模板",
            FormSchema = """{"fields":[{"key":"hours","label":"加班时长","type":"number","required":true}]}""",
            Status = 1
        });
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        json.GetProperty("code").GetInt32().Should().Be(200);
        json.GetProperty("data").GetProperty("templateCode").GetString().Should().Be("overtime_test");
    }

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        var resp = await _client.PostAsJsonAsync("/api/approval/template", new
        {
            TemplateName = "重复模板", TemplateCode = "leave", Status = 1
        });
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        json.GetProperty("code").GetInt32().Should().Be(500);
    }

    [Fact]
    public async Task SetNodes_ReplacesExistingNodes()
    {
        // 先创建模板
        var createResp = await _client.PostAsJsonAsync("/api/approval/template", new
        {
            TemplateName = "节点替换测试", TemplateCode = $"node_test_{Guid.NewGuid():N}",
            FormSchema = "{}", Status = 1
        });
        var created = JsonSerializer.Deserialize<JsonElement>(await createResp.Content.ReadAsStringAsync());
        var templateId = created.GetProperty("data").GetProperty("templateId").GetInt64();

        // 设置节点
        var setResp = await _client.PostAsJsonAsync($"/api/approval/template/{templateId}/nodes", new
        {
            Nodes = new[]
            {
                new { NodeName = "一审", NodeOrder = 1, ApproverType = "role", ApproverValue = "oa_admin" },
                new { NodeName = "二审", NodeOrder = 2, ApproverType = "user", ApproverValue = "1" },
            }
        });
        var setResult = JsonSerializer.Deserialize<JsonElement>(await setResp.Content.ReadAsStringAsync());
        setResult.GetProperty("code").GetInt32().Should().Be(200);

        // 验证节点
        var detail = await GetJson($"/api/approval/template/{templateId}");
        detail.GetProperty("data").GetProperty("nodes").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Delete_TemplateInUse_Fails()
    {
        // 请假模板(id=1) 有审批数据，不可删除
        var resp = await _client.DeleteAsync("/api/approval/template/1");
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        json.GetProperty("code").GetInt32().Should().Be(500);
        json.GetProperty("msg").GetString().Should().Contain("已被使用");
    }

    private async Task<JsonElement> GetJson(string url)
    {
        var resp = await _client.GetAsync(url);
        return JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
    }
}
