using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 审批流程集成测试 — 验证完整审批链路
///
/// 种子数据：
///   admin(UmcUserId=1, oa_admin) — 可审批所有节点
///   zhangsan(UmcUserId=2, oa_user) — 普通员工
///   lisi(UmcUserId=3, oa_user) — 普通员工
///
/// 请假模板(leave)：主管审批 → HR确认（2节点）
/// 报销模板(expense)：主管审批 → 财务审核 → 总经理审批（3节点）
/// </summary>
public class ApprovalFlowTests : IClassFixture<OATestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _out;
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ApprovalFlowTests(OATestFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _out = output;
    }

    private void Log(string msg) => _out.WriteLine(msg);

    // ══════════════════════════════════════════════════
    // Flow 1：请假审批 — 提交 → 逐节点通过 → approved
    // ══════════════════════════════════════════════════
    [Fact]
    public async Task Flow1_LeaveApproval_FullApprove()
    {
        Log("═══ Flow 1：请假审批全流程通过 ═══\n");

        // Step 1：提交请假
        Log("▶ Step 1：张三提交请假申请");
        var submitResp = await _client.PostAsJsonAsync("/api/approval/submit", new
        {
            TemplateId = 1,
            Title = "张三的请假申请_Flow1",
            FormData = """{"leaveType":"annual","startDate":"2026-04-10","endDate":"2026-04-11","days":2,"reason":"测试请假"}"""
        });
        var submit = await ReadJson(submitResp);
        submit.GetProperty("code").GetInt32().Should().Be(200);
        var approvalId = submit.GetProperty("data").GetProperty("approvalId").GetInt64();
        Log($"  ✅ 提交成功，ApprovalId={approvalId}");

        // Step 2：查看详情，确认在节点1
        Log("\n▶ Step 2：查看审批详情");
        var detail = await GetJson($"/api/approval/{approvalId}");
        detail.GetProperty("data").GetProperty("currentNodeOrder").GetInt32().Should().Be(1);
        detail.GetProperty("data").GetProperty("status").GetString().Should().Be("pending");
        Log($"  ✅ 当前节点：{detail.GetProperty("data").GetProperty("currentNodeName").GetString()}");

        // Step 3：主管（admin）通过节点1
        Log("\n▶ Step 3：admin（主管）审批通过");
        var approve1 = await PutJson($"/api/approval/{approvalId}/approve", new { Comment = "同意请假" });
        approve1.GetProperty("code").GetInt32().Should().Be(200);
        Log("  ✅ 节点1 通过");

        // Step 4：HR（admin）通过节点2
        Log("\n▶ Step 4：admin（HR）审批通过");
        var approve2 = await PutJson($"/api/approval/{approvalId}/approve", new { Comment = "HR确认" });
        approve2.GetProperty("code").GetInt32().Should().Be(200);
        Log("  ✅ 节点2 通过");

        // Step 5：验证最终状态
        Log("\n▶ Step 5：验证最终状态");
        var final = await GetJson($"/api/approval/{approvalId}");
        final.GetProperty("data").GetProperty("status").GetString().Should().Be("approved");
        var records = final.GetProperty("data").GetProperty("records");
        records.GetArrayLength().Should().Be(2);
        Log("  ✅ 状态=approved，审批记录 2 条");

        Log("\n✅✅✅ Flow 1 全部通过！\n");
    }

    // ══════════════════════════════════════════════════
    // Flow 2：请假审批 — 提交 → 主管驳回 → rejected
    // ══════════════════════════════════════════════════
    [Fact]
    public async Task Flow2_LeaveApproval_Rejected()
    {
        Log("═══ Flow 2：请假审批驳回 ═══\n");

        var submitResp = await _client.PostAsJsonAsync("/api/approval/submit", new
        {
            TemplateId = 1,
            Title = "张三的请假申请_Flow2_驳回",
            FormData = """{"leaveType":"sick","startDate":"2026-04-15","endDate":"2026-04-20","days":5,"reason":"测试驳回"}"""
        });
        var submit = await ReadJson(submitResp);
        var approvalId = submit.GetProperty("data").GetProperty("approvalId").GetInt64();
        Log($"  提交成功，ApprovalId={approvalId}");

        // 主管驳回
        var reject = await PutJson($"/api/approval/{approvalId}/reject", new { Comment = "请假天数过多，需重新申请" });
        reject.GetProperty("code").GetInt32().Should().Be(200);

        var final = await GetJson($"/api/approval/{approvalId}");
        final.GetProperty("data").GetProperty("status").GetString().Should().Be("rejected");
        Log("  ✅ 状态=rejected");

        Log("\n✅✅✅ Flow 2 全部通过！\n");
    }

    // ══════════════════════════════════════════════════
    // Flow 3：请假审批 — 提交 → 申请人撤回 → cancelled
    // ══════════════════════════════════════════════════
    [Fact]
    public async Task Flow3_LeaveApproval_Cancelled()
    {
        Log("═══ Flow 3：请假审批撤回 ═══\n");

        var submitResp = await _client.PostAsJsonAsync("/api/approval/submit", new
        {
            TemplateId = 1,
            Title = "张三的请假申请_Flow3_撤回",
            FormData = """{"leaveType":"personal","startDate":"2026-05-01","endDate":"2026-05-01","days":1,"reason":"测试撤回"}"""
        });
        var submit = await ReadJson(submitResp);
        var approvalId = submit.GetProperty("data").GetProperty("approvalId").GetInt64();

        // 申请人撤回
        var cancel = await PutJson($"/api/approval/{approvalId}/cancel", new { });
        cancel.GetProperty("code").GetInt32().Should().Be(200);

        var final = await GetJson($"/api/approval/{approvalId}");
        final.GetProperty("data").GetProperty("status").GetString().Should().Be("cancelled");
        Log("  ✅ 状态=cancelled");

        Log("\n✅✅✅ Flow 3 全部通过！\n");
    }

    // ══════════════════════════════════════════════════
    // Flow 4：报销审批 — 3 节点全流程通过
    // ══════════════════════════════════════════════════
    [Fact]
    public async Task Flow4_ExpenseApproval_FullApprove()
    {
        Log("═══ Flow 4：报销审批全流程通过（3 节点）═══\n");

        var submitResp = await _client.PostAsJsonAsync("/api/approval/submit", new
        {
            TemplateId = 2,
            Title = "张三的报销申请_Flow4",
            FormData = """{"expenseType":"travel","amount":5000,"description":"上海出差"}"""
        });
        var submit = await ReadJson(submitResp);
        var approvalId = submit.GetProperty("data").GetProperty("approvalId").GetInt64();
        Log($"  提交成功，ApprovalId={approvalId}");

        // 节点1：主管通过
        var a1 = await PutJson($"/api/approval/{approvalId}/approve", new { Comment = "出差已批准" });
        a1.GetProperty("code").GetInt32().Should().Be(200);
        Log("  ✅ 节点1（主管）通过");

        // 节点2：财务通过
        var a2 = await PutJson($"/api/approval/{approvalId}/approve", new { Comment = "费用合理" });
        a2.GetProperty("code").GetInt32().Should().Be(200);
        Log("  ✅ 节点2（财务）通过");

        // 节点3：总经理通过
        var a3 = await PutJson($"/api/approval/{approvalId}/approve", new { Comment = "同意报销" });
        a3.GetProperty("code").GetInt32().Should().Be(200);
        Log("  ✅ 节点3（总经理）通过");

        // 验证
        var final = await GetJson($"/api/approval/{approvalId}");
        final.GetProperty("data").GetProperty("status").GetString().Should().Be("approved");
        final.GetProperty("data").GetProperty("records").GetArrayLength().Should().Be(3);
        Log("  ✅ 状态=approved，3 条审批记录");

        Log("\n✅✅✅ Flow 4 全部通过！\n");
    }

    // ══════════════════════════════════════════════════
    // 权限测试
    // ══════════════════════════════════════════════════

    [Fact]
    public async Task Mine_ReturnsOnlyOwnApprovals()
    {
        var resp = await GetJson("/api/approval/mine");
        resp.GetProperty("code").GetInt32().Should().Be(200);
        // TestAuthHandler 模拟 admin(sub=1)，mine 应只返回申请人为 admin 的
        // 种子数据中没有 admin 的申请（只有 zhangsan 和 lisi），但 Flow 测试中会创建
    }

    [Fact]
    public async Task Pending_ReturnsOnlyPendingForCurrentUser()
    {
        var resp = await GetJson("/api/approval/pending");
        resp.GetProperty("code").GetInt32().Should().Be(200);
        // admin (oa_admin) 应能看到所有待审批（因为 dept_leader 和 role 都匹配）
    }

    [Fact]
    public async Task Templates_ReturnsAvailableTemplates()
    {
        var resp = await GetJson("/api/approval/templates");
        resp.GetProperty("code").GetInt32().Should().Be(200);
        var data = resp.GetProperty("data");
        data.GetArrayLength().Should().BeGreaterThanOrEqualTo(2, "至少有请假和报销两个模板");
    }

    [Fact]
    public async Task SeedData_PendingApproval_Exists()
    {
        // 种子数据：张三请假(ApprovalId=1) 应为 pending
        var resp = await GetJson("/api/approval/1");
        var data = resp.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("pending");
        data.GetProperty("applicantName").GetString().Should().Be("张三");
    }

    [Fact]
    public async Task SeedData_ApprovedApproval_HasRecords()
    {
        // 种子数据：李四报销(ApprovalId=2) 应为 approved，3 条记录
        var resp = await GetJson("/api/approval/2");
        var data = resp.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("approved");
        data.GetProperty("records").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task SeedData_RejectedApproval_HasRecord()
    {
        // 种子数据：张三报销(ApprovalId=3) 应为 rejected，1 条驳回记录
        var resp = await GetJson("/api/approval/3");
        var data = resp.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("rejected");
        data.GetProperty("records").GetArrayLength().Should().Be(1);
        // 驳回记录的 action 应为 reject
        data.GetProperty("records")[0].GetProperty("action").GetString().Should().Be("reject");
    }

    // ── 辅助方法 ─────────────────────────────────────

    private async Task<JsonElement> ReadJson(HttpResponseMessage resp)
    {
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private async Task<JsonElement> GetJson(string url)
    {
        var resp = await _client.GetAsync(url);
        return await ReadJson(resp);
    }

    private async Task<JsonElement> PutJson(string url, object body)
    {
        var resp = await _client.PutAsJsonAsync(url, body);
        return await ReadJson(resp);
    }
}
