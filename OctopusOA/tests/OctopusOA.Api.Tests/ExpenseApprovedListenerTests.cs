using FluentAssertions;
using OctopusOA.Api.Services;
using OctopusOA.Api.Workflow;

namespace OctopusOA.Api.Tests;

/// <summary>
/// Q3-② 报销审批通过 → FIN 费用凭证：事件路由与表单解析的单元测试。
/// HTTP 传输层由 FIN 侧 OaExpenseWebhook_GeneratesExpenseVoucher 集成测试兜底，
/// 事件分发链路由 FlowExtensionTests.ApprovedEvent_Fired_OnCompletion 证明。
/// </summary>
public class ExpenseApprovedListenerTests
{
    private static ProcessEvent Ev(string code, string type, string formData, string title = "测试报销") => new(
        type,
        new FlowInstance { Id = 42, FormData = formData, Title = title },
        new FlowDefinition { Id = 7, Code = code, Name = code, Version = 1, Status = 1, GraphJson = "{}" });

    [Fact]
    public void ExpenseApproved_WithValidAmount_BuildsPayload()
    {
        var payload = ExpenseApprovedListener.TryBuildPayload(Ev(
            "expense", ProcessEventTypes.Approved,
            """{"expenseType":"travel","amount":3200,"description":"北京出差差旅费"}""",
            "李四的报销申请"));

        payload.Should().NotBeNull();
        payload!.OaApprovalId.Should().Be(42);
        payload.Amount.Should().Be(3200m);
        payload.Summary.Should().Be("报销支出：北京出差差旅费");
    }

    [Fact]
    public void ExpenseApproved_WithoutDescription_FallsBackToTitle()
    {
        var payload = ExpenseApprovedListener.TryBuildPayload(Ev(
            "expense", ProcessEventTypes.Approved, """{"expenseType":"office","amount":150}""",
            "张三的办公用品报销"));

        payload.Should().NotBeNull();
        payload!.Summary.Should().Be("报销支出：张三的办公用品报销");
    }

    [Fact]
    public void NonExpenseFlow_IsIgnored()
    {
        ExpenseApprovedListener.TryBuildPayload(Ev(
            "purchase_order", ProcessEventTypes.Approved, """{"amount":50000}""")).Should().BeNull();
    }

    [Fact]
    public void RejectedExpense_IsIgnored()
    {
        ExpenseApprovedListener.TryBuildPayload(Ev(
            "expense", ProcessEventTypes.Rejected, """{"amount":3200}""")).Should().BeNull();
    }

    [Fact]
    public void MissingOrInvalidAmount_IsIgnored()
    {
        ExpenseApprovedListener.TryBuildPayload(Ev("expense", ProcessEventTypes.Approved, "{}")).Should().BeNull();
        ExpenseApprovedListener.TryBuildPayload(Ev("expense", ProcessEventTypes.Approved, """{"amount":0}""")).Should().BeNull();
        ExpenseApprovedListener.TryBuildPayload(Ev("expense", ProcessEventTypes.Approved, """{"amount":-5}""")).Should().BeNull();
        ExpenseApprovedListener.TryBuildPayload(Ev("expense", ProcessEventTypes.Approved, """{"amount":"3200"}""")).Should().BeNull();
    }

    [Fact]
    public void CorruptedFormData_IsIgnored()
    {
        ExpenseApprovedListener.TryBuildPayload(Ev(
            "expense", ProcessEventTypes.Approved, "{not-json")).Should().BeNull();
    }
}
