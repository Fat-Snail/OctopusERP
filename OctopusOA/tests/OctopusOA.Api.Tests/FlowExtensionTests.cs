using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OctopusOA.Api.Persistence;
using OctopusOA.Api.Workflow;

namespace OctopusOA.Api.Tests;

/// <summary>审批中心扩展点：服务任务 + 事件监听器 + 采购流程种子</summary>
public class FlowExtensionTests : IClassFixture<OATestFactory>
{
    private readonly OATestFactory _factory;

    public FlowExtensionTests(OATestFactory factory) => _factory = factory;

    private sealed class FakeServiceTask(string kind = "fake_task") : IServiceTaskHandler
    {
        public string Kind => kind;
        public int Calls { get; private set; }
        public ServiceTaskContext? Last { get; private set; }
        public string? Execute(ServiceTaskContext ctx) { Calls++; Last = ctx; return null; }
    }

    private sealed class FakeListener : IProcessEventListener
    {
        public List<ProcessEvent> Events { get; } = new();
        public void OnEvent(ProcessEvent evt) => Events.Add(evt);
    }

    private static (FlowEngine engine, FakeServiceTask task, FakeListener listener) CreateEngine(
        OaDbContext db, string handlerKind = "fake_task")
    {
        var task = new FakeServiceTask(handlerKind);
        var tasks = new ServiceTaskHandlerRegistry(new IServiceTaskHandler[] { task });
        var listener = new FakeListener();
        var events = new ProcessEventDispatcher(new IProcessEventListener[] { listener });
        var engine = new FlowEngine(db, new ApproverStrategyRegistry(), tasks, events);
        return (engine, task, listener);
    }

    private static long Seed(OaDbContext db, string code, FlowGraph graph)
    {
        var def = new FlowDefinition { Code = code, Name = code, Version = 1, Status = 1, GraphJson = graph.ToJson() };
        db.FlowDefinitions.Add(def);
        db.SaveChanges();
        return def.Id;
    }

    private static FlowNode N(string id, string type, string name, FlowNodeConfig? cfg = null)
        => new() { Id = id, Type = type, Name = name, Config = cfg };

    private static FlowEdge E(string from, string to, string? cond = null)
        => new() { From = from, To = to, Condition = cond };

    private static FlowNodeConfig Approver(string type, string? value = null)
        => new() { ApproverType = type, ApproverValue = value };

    private static List<FlowTask> Pending(OaDbContext db, long instanceId, long userId)
        => db.FlowTasks.Where(t => t.InstanceId == instanceId && t.AssigneeId == userId && t.Status == "pending").ToList();

    // 单审批 + 服务任务 的通用图
    private static FlowGraph ApprovalThenService(string serviceKind) => new()
    {
        Nodes =
        {
            N("start", FlowNodeTypes.Start, "开始"),
            N("n1", FlowNodeTypes.Approval, "主管", Approver("user", "1")),
            N("svc", FlowNodeTypes.ServiceTask, "服务任务", new FlowNodeConfig { ServiceKind = serviceKind }),
            N("end", FlowNodeTypes.End, "结束"),
        },
        Edges = { E("start", "n1"), E("n1", "svc"), E("svc", "end") },
    };

    [Fact]
    public void ServiceTask_Executes_OnApproval()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var (engine, task, _) = CreateEngine(db);

        var defId = Seed(db, "svc_test", ApprovalThenService("fake_task"));
        var (inst, err) = engine.Start(new StartFlowRequest(defId, "服务任务", 2, "张三", "{}"));
        err.Should().BeNull();

        engine.CompleteTask(Pending(db, inst!.Id, 1)[0].Id, 1, "admin", "approve", null).Should().BeNull();

        task.Calls.Should().Be(1);
        task.Last!.Node.Id.Should().Be("svc");
        engine.GetInstance(inst.Id)!.Status.Should().Be("approved");
        db.FlowHistories.Count(h => h.InstanceId == inst.Id && h.Action == "complete").Should().Be(1);
    }

    [Fact]
    public void ApprovedEvent_Fired_OnCompletion()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var (engine, _, listener) = CreateEngine(db);

        var defId = Seed(db, "event_test", ApprovalThenService("fake_task"));
        var (inst, _) = engine.Start(new StartFlowRequest(defId, "事件", 2, "张三", "{}"));
        engine.CompleteTask(Pending(db, inst!.Id, 1)[0].Id, 1, "admin", "approve", null).Should().BeNull();

        var approved = listener.Events.Single(e => e.Type == ProcessEventTypes.Approved);
        approved.Instance.Id.Should().Be(inst.Id);
        approved.Definition.Id.Should().Be(defId);
    }

    [Fact]
    public void RejectedEvent_Fired_OnReject()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var (engine, _, listener) = CreateEngine(db);

        var defId = Seed(db, "reject_event_test", ApprovalThenService("fake_task"));
        var (inst, _) = engine.Start(new StartFlowRequest(defId, "驳回", 2, "张三", "{}"));
        engine.CompleteTask(Pending(db, inst!.Id, 1)[0].Id, 1, "admin", "reject", "不同意").Should().BeNull();

        listener.Events.Single(e => e.Type == ProcessEventTypes.Rejected).Instance.Id.Should().Be(inst.Id);
        engine.GetInstance(inst.Id)!.Status.Should().Be("rejected");
    }

    [Fact]
    public void UnregisteredServiceTask_ReturnsError()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var (engine, _, _) = CreateEngine(db); // 只注册了 fake_task

        var defId = Seed(db, "missing_svc_test", ApprovalThenService("not_registered"));
        var (inst, _) = engine.Start(new StartFlowRequest(defId, "缺处理器", 2, "张三", "{}"));

        var err = engine.CompleteTask(Pending(db, inst!.Id, 1)[0].Id, 1, "admin", "approve", null);
        err.Should().Contain("未注册的服务任务");
    }

    // ── 采购流程种子 ───────────────────────────────────────

    [Fact]
    public void PurchaseOrderDefinition_IsSeeded_WithServiceTask()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();

        var def = db.FlowDefinitions.FirstOrDefault(d => d.Code == "purchase_order");
        def.Should().NotBeNull();
        def!.BusinessKey.Should().Be("purchase_order");

        var graph = FlowGraph.FromJson(def.GraphJson);
        graph.Nodes.Should().Contain(n => n.Type == FlowNodeTypes.ServiceTask);
        graph.Nodes.Should().Contain(n => n.Type == FlowNodeTypes.ExclusiveGateway);
        graph.Nodes.Should().Contain(n => n.Type == FlowNodeTypes.Approval);
    }

    [Fact]
    public void PurchaseFlow_SmallAmount_SkipsGeneralManager()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var (engine, task, listener) = CreateEngine(db, "create_wms_inbound");

        var def = db.FlowDefinitions.First(d => d.Code == "purchase_order");
        var (inst, err) = engine.Start(new StartFlowRequest(
            def.Id, "采购50000", 2, "张三",
            """{"supplierName":"供应商A","productName":"面料X","quantity":100,"amount":50000}"""));
        err.Should().BeNull();

        // 采购主管（admin=1）→ 财务（admin=1）
        engine.CompleteTask(Pending(db, inst!.Id, 1)[0].Id, 1, "admin", "approve", null).Should().BeNull();
        engine.CompleteTask(Pending(db, inst.Id, 1)[0].Id, 1, "admin", "approve", null).Should().BeNull();

        // 金额 < 10 万 → 跳过总经理 → 服务任务 → approved
        engine.GetInstance(inst.Id)!.Status.Should().Be("approved");
        task.Calls.Should().Be(1);
        listener.Events.Should().Contain(e => e.Type == ProcessEventTypes.Approved);
    }

    [Fact]
    public void AttendanceFixFlow_Approved_UpdatesAttendance()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var def = db.FlowDefinitions.First(d => d.Code == "attendance_fix");
        var (inst, err) = engine.Start(new StartFlowRequest(
            def.Id, "补卡申请", 2, "张三",
            """{"fixDate":"2025-01-15","fixType":"checkIn","fixTime":"09:30","reason":"忘记打卡"}"""));
        err.Should().BeNull();

        engine.CompleteTask(Pending(db, inst!.Id, 1)[0].Id, 1, "admin", "approve", null).Should().BeNull();
        engine.CompleteTask(Pending(db, inst.Id, 1)[0].Id, 1, "admin", "approve", null).Should().BeNull();

        engine.GetInstance(inst.Id)!.Status.Should().Be("approved");
        var record = db.Attendances.FirstOrDefault(a => a.UmcUserId == 2 && a.Date == "2025-01-15");
        record.Should().NotBeNull();
        record!.CheckInStatus.Should().Be("normal");
    }

    [Fact]
    public void FlowDefinitions_CommonFlows_AreSeeded()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        foreach (var code in new[] { "leave", "expense", "attendance_fix", "purchase_order" })
            db.FlowDefinitions.Any(d => d.Code == code).Should().BeTrue($"{code} 应已种子");
    }

    [Fact]
    public void FlowDemoData_IsSeeded()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        db.FlowInstances.Count(i => i.Title.Contains("示例")).Should().Be(6);
        db.FlowTasks.Count(t => t.Status == "pending").Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void DemoPendingTask_CanBeApproved_AndAdvances()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var demo = db.FlowInstances.First(i => i.Title.Contains("张三的请假申请（示例）"));
        var task = db.FlowTasks.First(t => t.InstanceId == demo.Id && t.Status == "pending");
        engine.CompleteTask(task.Id, 1, "admin", "approve", "同意").Should().BeNull();

        // leave：n1 通过 → n2（HR），实例仍 pending
        engine.GetInstance(demo.Id)!.Status.Should().Be("pending");
        db.FlowTasks.Count(t => t.InstanceId == demo.Id && t.Status == "pending").Should().Be(1);
    }
}
