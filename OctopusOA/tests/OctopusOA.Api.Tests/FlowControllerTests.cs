using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OctopusOA.Api.Persistence;
using OctopusOA.Api.Workflow;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>审批中心 HTTP 端到端：发起 → 待办 → 通过 → 详情</summary>
public class FlowControllerTests : IClassFixture<OATestFactory>
{
    private readonly OATestFactory _factory;
    private readonly HttpClient _client;

    public FlowControllerTests(OATestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string url, object? body, string userId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-UserId", userId);
        if (body != null) req.Content = JsonContent.Create(body);
        var resp = await _client.SendAsync(req);
        return JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
    }

    private long SeedLinearDefinition()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var graph = new FlowGraph
        {
            Nodes = {
                new FlowNode { Id = "start", Type = FlowNodeTypes.Start, Name = "开始" },
                new FlowNode { Id = "n1", Type = FlowNodeTypes.Approval, Name = "主管", Config = new FlowNodeConfig { ApproverType = "user", ApproverValue = "1" } },
                new FlowNode { Id = "end", Type = FlowNodeTypes.End, Name = "结束" },
            },
            Edges = {
                new FlowEdge { From = "start", To = "n1" },
                new FlowEdge { From = "n1", To = "end" },
            },
        };
        var def = new FlowDefinition { Code = "http_test", Name = "HTTP测试流程", Version = 1, Status = 1, GraphJson = graph.ToJson() };
        db.FlowDefinitions.Add(def);
        db.SaveChanges();
        return def.Id;
    }

    [Fact]
    public async Task Submit_Approve_Detail_EndToEnd()
    {
        var defId = SeedLinearDefinition();

        // 张三发起
        var submit = await SendAsync(HttpMethod.Post, "/api/flow/submit", new
        {
            DefinitionId = defId,
            Title = "HTTP 端到端流程",
            FormData = """{"days":1}""",
        }, "2");
        submit.GetProperty("code").GetInt32().Should().Be(200);
        var instanceId = submit.GetProperty("data").GetProperty("instanceId").GetInt64();

        // admin 待办里能看到
        var pending = await SendAsync(HttpMethod.Get, "/api/flow/pending", null, "1");
        pending.GetProperty("code").GetInt32().Should().Be(200);
        var rows = pending.GetProperty("data").GetProperty("rows").EnumerateArray().ToList();
        var row = rows.Single(r => r.GetProperty("instanceId").GetInt64() == instanceId);
        var taskId = row.GetProperty("taskId").GetInt64();
        row.GetProperty("nodeName").GetString().Should().Be("主管");

        // admin 通过
        var approve = await SendAsync(HttpMethod.Put, $"/api/flow/task/{taskId}/approve", new { Comment = "同意" }, "1");
        approve.GetProperty("code").GetInt32().Should().Be(200);

        // 详情状态 = approved
        var detail = await SendAsync(HttpMethod.Get, $"/api/flow/{instanceId}", null, "1");
        detail.GetProperty("code").GetInt32().Should().Be(200);
        detail.GetProperty("data").GetProperty("status").GetString().Should().Be("approved");
        detail.GetProperty("data").GetProperty("history").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Approve_ByNonAssignee_Returns403()
    {
        var defId = SeedLinearDefinition();
        var submit = await SendAsync(HttpMethod.Post, "/api/flow/submit", new
        {
            DefinitionId = defId, Title = "越权测试", FormData = "{}",
        }, "2");
        var instanceId = submit.GetProperty("data").GetProperty("instanceId").GetInt64();

        var pending = await SendAsync(HttpMethod.Get, "/api/flow/pending", null, "1");
        var taskId = pending.GetProperty("data").GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("instanceId").GetInt64() == instanceId)
            .GetProperty("taskId").GetInt64();

        // 李四(3) 不是审批人 → 403
        var reject = await SendAsync(HttpMethod.Put, $"/api/flow/task/{taskId}/approve", new { }, "3");
        reject.GetProperty("code").GetInt32().Should().Be(403);
    }

    // ── 流程定义 CRUD（设计器）────────────────────────────

    [Fact]
    public async Task SaveDefinition_Create_AppearsInDefinitions()
    {
        var graph = new FlowGraph
        {
            Nodes = {
                new FlowNode { Id = "start", Type = FlowNodeTypes.Start, Name = "开始" },
                new FlowNode { Id = "end", Type = FlowNodeTypes.End, Name = "结束" },
            },
            Edges = { new FlowEdge { From = "start", To = "end" } },
        };
        var resp = await SendAsync(HttpMethod.Post, "/api/flow/definitions", new
        {
            Code = "designer_test", Name = "设计器测试", FormSchema = "{}", GraphJson = graph.ToJson(),
        }, "1");
        resp.GetProperty("code").GetInt32().Should().Be(200);
        var id = resp.GetProperty("data").GetProperty("id").GetInt64();

        var list = await SendAsync(HttpMethod.Get, "/api/flow/definitions", null, "1");
        list.GetProperty("data").EnumerateArray()
            .Any(d => d.GetProperty("id").GetInt64() == id).Should().BeTrue();
    }

    [Fact]
    public async Task SaveDefinition_Update_BumpsVersion()
    {
        var defId = SeedLinearDefinition();
        var detail = await SendAsync(HttpMethod.Get, $"/api/flow/definitions/{defId}", null, "1");
        detail.GetProperty("code").GetInt32().Should().Be(200);

        var resp = await SendAsync(HttpMethod.Post, "/api/flow/definitions", new
        {
            Id = defId, Code = "http_test", Name = "改名", FormSchema = "{}",
            GraphJson = detail.GetProperty("data").GetProperty("graphJson").GetString(),
        }, "1");
        resp.GetProperty("code").GetInt32().Should().Be(200);
        resp.GetProperty("data").GetProperty("version").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task SaveDefinition_MissingStart_Returns400()
    {
        var resp = await SendAsync(HttpMethod.Post, "/api/flow/definitions", new
        {
            Code = "bad", Name = "坏流程", FormSchema = "{}", GraphJson = """{"nodes":[],"edges":[]}""",
        }, "1");
        resp.GetProperty("code").GetInt32().Should().Be(400);
    }
}
