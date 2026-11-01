using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctopusOA.Api.Persistence;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusOA.Api.Tests;

/// <summary>P17 BPM 扩展：条件分支 + 转交 + 抄送</summary>
public class BpmExtensionTests : IClassFixture<OATestFactory>
{
    private readonly OATestFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public BpmExtensionTests(OATestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, string userId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-UserId", userId);
        if (body != null) req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage r) =>
        JsonSerializer.Deserialize<JsonElement>(await r.Content.ReadAsStringAsync());

    // ── 条件分支：amount < 1000 跳过总经理审批 ──────────────────────

    [Fact]
    public async Task SkipCondition_SmallAmount_SkipsLastNode()
    {
        // 报销模板（TemplateId=2）三个节点：主管→财务→总经理
        // 给"总经理审批"节点加 SkipCondition: amount < 1000
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
            var managerNode = await db.Nodes
                .Where(n => n.TemplateId == 2 && n.NodeName.Contains("总经理"))
                .FirstAsync();
            managerNode.SkipCondition = "amount < 1000";
            await db.SaveChangesAsync();
        }

        // 张三提交 500 元报销
        var submit = await SendAsync(HttpMethod.Post, "/api/approval/submit", new
        {
            TemplateId = 2,
            Title = "P17 小额报销 — 应跳过总经理",
            FormData = """{"amount":500,"category":"差旅"}""",
        }, "2");
        var submitDoc = await ReadJson(submit);
        submitDoc.GetProperty("code").GetInt32().Should().Be(200);
        var approvalId = submitDoc.GetProperty("data").GetProperty("approvalId").GetInt64();

        // 节点 1 (主管 admin=1) 审批通过
        var n1 = await SendAsync(HttpMethod.Put, $"/api/approval/{approvalId}/approve", new { Comment = "OK" }, "1");
        (await ReadJson(n1)).GetProperty("code").GetInt32().Should().Be(200);

        // 节点 2 (财务 admin) 审批通过 → 应跳过总经理直接 approved
        var n2 = await SendAsync(HttpMethod.Put, $"/api/approval/{approvalId}/approve", new { Comment = "OK" }, "1");
        (await ReadJson(n2)).GetProperty("code").GetInt32().Should().Be(200);

        // 验证最终状态 = approved + 有一条 skip 记录
        using var s2 = _factory.Services.CreateScope();
        var db2 = s2.ServiceProvider.GetRequiredService<OaDbContext>();
        var approval = await db2.Approvals.FirstAsync(a => a.ApprovalId == approvalId);
        approval.Status.Should().Be("approved");

        var skipRecords = await db2.ApprovalRecords
            .Where(r => r.ApprovalId == approvalId && r.Action == "skip")
            .ToListAsync();
        skipRecords.Should().HaveCountGreaterThanOrEqualTo(1);
        skipRecords[0].Comment.Should().Contain("amount < 1000");
    }

    // ── 条件分支：amount >= 1000 不跳过 ────────────────────────────

    [Fact]
    public async Task SkipCondition_LargeAmount_DoesNotSkip()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
            var managerNode = await db.Nodes
                .Where(n => n.TemplateId == 2 && n.NodeName.Contains("总经理"))
                .FirstAsync();
            managerNode.SkipCondition = "amount < 1000";
            await db.SaveChangesAsync();
        }

        var submit = await SendAsync(HttpMethod.Post, "/api/approval/submit", new
        {
            TemplateId = 2,
            Title = "P17 大额报销 — 不应跳过",
            FormData = """{"amount":5000,"category":"差旅"}""",
        }, "2");
        var approvalId = (await ReadJson(submit)).GetProperty("data").GetProperty("approvalId").GetInt64();

        await SendAsync(HttpMethod.Put, $"/api/approval/{approvalId}/approve", new { Comment = "" }, "1");
        await SendAsync(HttpMethod.Put, $"/api/approval/{approvalId}/approve", new { Comment = "" }, "1");

        // 仍处于 pending（在总经理节点）
        using var s = _factory.Services.CreateScope();
        var db2 = s.ServiceProvider.GetRequiredService<OaDbContext>();
        var approval = await db2.Approvals.FirstAsync(a => a.ApprovalId == approvalId);
        approval.Status.Should().Be("pending");
        approval.CurrentNodeOrder.Should().Be(3); // 在总经理节点
    }

    // ── 转交 ────────────────────────────────────────────────────────

    [Fact]
    public async Task Delegate_TransfersAuthorityToAnotherUser()
    {
        // 提交请假（节点 1 主管审批 — admin 默认有权）
        var submit = await SendAsync(HttpMethod.Post, "/api/approval/submit", new
        {
            TemplateId = 1,
            Title = "P17 转交测试",
            FormData = """{"leaveType":"annual","days":1}""",
        }, "2");
        var approvalId = (await ReadJson(submit)).GetProperty("data").GetProperty("approvalId").GetInt64();

        // admin (1) 转交给 lisi (3)
        var dlg = await SendAsync(HttpMethod.Put, $"/api/approval/{approvalId}/delegate", new
        {
            ToUserId = 3L, ToUserName = "lisi", Reason = "我休假",
        }, "1");
        var dlgDoc = await ReadJson(dlg);
        dlgDoc.GetProperty("code").GetInt32().Should().Be(200);

        // 验证 admin 现在不能审批（已转交）
        using var s = _factory.Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<OaDbContext>();
        var deleg = await db.ApprovalDelegates.FirstAsync(d => d.ApprovalId == approvalId);
        deleg.FromUserId.Should().Be(1);
        deleg.ToUserId.Should().Be(3);
        deleg.Reason.Should().Be("我休假");

        // lisi (3) 现在能在 pending 列表中看到这单
        var pending = await SendAsync(HttpMethod.Get, "/api/approval/pending", null, "3");
        var pendingDoc = await ReadJson(pending);
        var rows = pendingDoc.GetProperty("data").GetProperty("rows").EnumerateArray()
            .Select(x => x.GetProperty("approvalId").GetInt64()).ToList();
        rows.Should().Contain(approvalId);
    }

    [Fact]
    public async Task Delegate_ToSelfRejected()
    {
        var submit = await SendAsync(HttpMethod.Post, "/api/approval/submit", new
        {
            TemplateId = 1,
            Title = "自转交测试",
            FormData = """{"days":1}""",
        }, "2");
        var approvalId = (await ReadJson(submit)).GetProperty("data").GetProperty("approvalId").GetInt64();

        var resp = await SendAsync(HttpMethod.Put, $"/api/approval/{approvalId}/delegate",
            new { ToUserId = 1L, ToUserName = "admin", Reason = "" }, "1");
        var doc = await ReadJson(resp);
        doc.GetProperty("code").GetInt32().Should().Be(400);
        doc.GetProperty("msg").GetString().Should().Contain("自己");
    }

    [Fact]
    public async Task Delegate_NoPermission_Rejected()
    {
        var submit = await SendAsync(HttpMethod.Post, "/api/approval/submit", new
        {
            TemplateId = 1, Title = "无权转交", FormData = """{"days":1}""",
        }, "2");
        var approvalId = (await ReadJson(submit)).GetProperty("data").GetProperty("approvalId").GetInt64();

        // lisi (3) 不在审批人列表内，无权转交
        var resp = await SendAsync(HttpMethod.Put, $"/api/approval/{approvalId}/delegate",
            new { ToUserId = 1L, ToUserName = "admin", Reason = "" }, "3");
        var doc = await ReadJson(resp);
        doc.GetProperty("code").GetInt32().Should().Be(400);
    }
}
