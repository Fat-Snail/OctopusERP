using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OctopusOA.Api.Persistence;
using OctopusOA.Api.Workflow;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>高级操作 HTTP 端到端：可驳回节点查询 + 委托/驳回接口通路</summary>
public class FlowAdvancedControllerTests : IClassFixture<OATestFactory>
{
    private readonly OATestFactory _factory;
    private readonly HttpClient _client;
    public FlowAdvancedControllerTests(OATestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    private async Task<JsonElement> Send(HttpMethod method, string url, object? body, string userId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-UserId", userId);
        if (body != null) req.Content = JsonContent.Create(body);
        var resp = await _client.SendAsync(req);
        return JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RejectableNodes_And_RejectTo_WorkEndToEnd()
    {
        // 种子：start → n1(approver=1) → n2(approver=1) → end
        long defId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
            var graph = new FlowGraph
            {
                Nodes = {
                    new FlowNode { Id = "start", Type = FlowNodeTypes.Start, Name = "开始" },
                    new FlowNode { Id = "n1", Type = FlowNodeTypes.Approval, Name = "主管审批", Config = new FlowNodeConfig { ApproverType = "user", ApproverValue = "1" } },
                    new FlowNode { Id = "n2", Type = FlowNodeTypes.Approval, Name = "HR审批", Config = new FlowNodeConfig { ApproverType = "user", ApproverValue = "1" } },
                    new FlowNode { Id = "end", Type = FlowNodeTypes.End, Name = "结束" },
                },
                Edges = {
                    new FlowEdge { From = "start", To = "n1" },
                    new FlowEdge { From = "n1", To = "n2" },
                    new FlowEdge { From = "n2", To = "end" },
                },
            };
            var def = new FlowDefinition { Code = "http_advanced_" + Guid.NewGuid().ToString("N")[..8], Name = "高级操作HTTP", GraphJson = graph.ToJson() };
            db.FlowDefinitions.Add(def);
            db.SaveChanges();
            defId = def.Id;
        }

        // 发起（张三=2）
        var submit = await Send(HttpMethod.Post, "/api/flow/submit",
            new { definitionId = defId, title = "HTTP驳回测试", formData = "{}" }, "2");
        submit.GetProperty("code").GetInt32().Should().Be(200);
        var instId = submit.GetProperty("data").GetProperty("instanceId").GetInt64();

        // admin=1 通过 n1 → 到 n2
        var pending1 = await Send(HttpMethod.Get, "/api/flow/pending", null, "1");
        var t1 = pending1.GetProperty("data").GetProperty("rows").EnumerateArray()
            .First(x => x.GetProperty("instanceId").GetInt64() == instId);
        var t1Id = t1.GetProperty("taskId").GetInt64();
        await Send(HttpMethod.Put, $"/api/flow/task/{t1Id}/approve", new { comment = "同意" }, "1");

        // n2 待办
        var pending2 = await Send(HttpMethod.Get, "/api/flow/pending", null, "1");
        var t2 = pending2.GetProperty("data").GetProperty("rows").EnumerateArray()
            .First(x => x.GetProperty("instanceId").GetInt64() == instId);
        var t2Id = t2.GetProperty("taskId").GetInt64();

        // 查可驳回节点：应含 n1（不含 start/end）
        var nodes = await Send(HttpMethod.Get, $"/api/flow/task/{t2Id}/rejectable-nodes", null, "1");
        nodes.GetProperty("code").GetInt32().Should().Be(200);
        var arr = nodes.GetProperty("data").EnumerateArray().ToList();
        arr.Should().ContainSingle(x => x.GetProperty("nodeId").GetString() == "n1");

        // 驳回到 n1
        var reject = await Send(HttpMethod.Put, $"/api/flow/task/{t2Id}/reject-to",
            new { targetNodeId = "n1", comment = "退回重审" }, "1");
        reject.GetProperty("code").GetInt32().Should().Be(200);

        // n1 重新有待办
        var pending3 = await Send(HttpMethod.Get, "/api/flow/pending", null, "1");
        pending3.GetProperty("data").GetProperty("rows").EnumerateArray()
            .Any(x => x.GetProperty("instanceId").GetInt64() == instId && x.GetProperty("nodeId").GetString() == "n1")
            .Should().BeTrue();
    }
}
