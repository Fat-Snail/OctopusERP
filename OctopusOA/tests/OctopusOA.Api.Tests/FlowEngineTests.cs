using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OctopusOA.Api.Persistence;
using OctopusOA.Api.Workflow;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 图流程引擎集成测试（真实 SQLite，直接驱动 IFlowEngine）。
/// 种子用户：admin(1, oa_admin) / zhangsan(2, oa_user) / lisi(3, oa_user)。
/// </summary>
public class FlowEngineTests : IClassFixture<OATestFactory>
{
    private readonly OATestFactory _factory;

    public FlowEngineTests(OATestFactory factory) => _factory = factory;

    // ── 图构造辅助 ─────────────────────────────────────────

    private static FlowNode Node(string id, string type, string name, FlowNodeConfig? cfg = null)
        => new() { Id = id, Type = type, Name = name, Config = cfg };

    private static FlowEdge Edge(string from, string to, string? cond = null)
        => new() { From = from, To = to, Condition = cond };

    private static FlowNodeConfig Approver(string type, string? value) => new() { ApproverType = type, ApproverValue = value };

    private static long Seed(OaDbContext db, string code, FlowGraph graph)
    {
        var def = new FlowDefinition { Code = code, Name = code, Version = 1, Status = 1, GraphJson = graph.ToJson() };
        db.FlowDefinitions.Add(def);
        db.SaveChanges();
        return def.Id;
    }

    private static List<FlowTask> PendingFor(OaDbContext db, long instanceId, long userId)
        => db.FlowTasks.Where(t => t.InstanceId == instanceId && t.AssigneeId == userId && t.Status == "pending").ToList();

    // ── 线性：单人审批链 ───────────────────────────────────

    [Fact]
    public void Linear_TwoApprovals_BecomesApproved()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var graph = new FlowGraph
        {
            Nodes = {
                Node("start", FlowNodeTypes.Start, "开始"),
                Node("n1", FlowNodeTypes.Approval, "直属主管", Approver("dept_leader", null)),
                Node("n2", FlowNodeTypes.Approval, "HR 确认", Approver("role", "oa_admin")),
                Node("end", FlowNodeTypes.End, "结束"),
            },
            Edges = { Edge("start", "n1"), Edge("n1", "n2"), Edge("n2", "end") },
        };
        var defId = Seed(db, "linear_test", graph);

        var (inst, err) = engine.Start(new StartFlowRequest(defId, "请假", 2, "张三", """{"days":2}"""));
        err.Should().BeNull();
        inst!.Status.Should().Be("pending");

        // 节点1 → 主管(admin=1)
        var t1 = PendingFor(db, inst.Id, 1);
        t1.Should().ContainSingle();
        engine.CompleteTask(t1[0].Id, 1, "admin", "approve", "同意").Should().BeNull();

        // 节点2 → HR(admin=1)
        var t2 = PendingFor(db, inst.Id, 1);
        t2.Should().ContainSingle();
        engine.CompleteTask(t2[0].Id, 1, "admin", "approve", "确认").Should().BeNull();

        engine.GetInstance(inst.Id)!.Status.Should().Be("approved");
    }

    // ── 排他网关：条件分支 ─────────────────────────────────

    [Fact]
    public void ExclusiveGateway_RoutesByCondition()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var graph = new FlowGraph
        {
            Nodes = {
                Node("start", FlowNodeTypes.Start, "开始"),
                Node("n1", FlowNodeTypes.Approval, "主管", Approver("user", "1")),
                Node("gw", FlowNodeTypes.ExclusiveGateway, "金额判断"),
                Node("high", FlowNodeTypes.Approval, "总经理", Approver("user", "2")),
                Node("low", FlowNodeTypes.Approval, "财务", Approver("user", "3")),
                Node("end", FlowNodeTypes.End, "结束"),
            },
            Edges = {
                Edge("start", "n1"), Edge("n1", "gw"),
                Edge("gw", "high", "amount >= 5000"),
                Edge("gw", "low"),                       // 默认分支
                Edge("high", "end"), Edge("low", "end"),
            },
        };
        var defId = Seed(db, "exclusive_test", graph);

        // 大额 → 走 high(总经理=2)
        var (big, _) = engine.Start(new StartFlowRequest(defId, "报销", 2, "张三", """{"amount":8000}"""));
        engine.CompleteTask(PendingFor(db, big!.Id, 1)[0].Id, 1, "admin", "approve", null).Should().BeNull();
        PendingFor(db, big.Id, 2).Should().ContainSingle();   // 总经理收到
        PendingFor(db, big.Id, 3).Should().BeEmpty();          // 财务未收到

        // 小额 → 走 low(财务=3)
        var (small, _) = engine.Start(new StartFlowRequest(defId, "报销", 2, "张三", """{"amount":100}"""));
        engine.CompleteTask(PendingFor(db, small!.Id, 1)[0].Id, 1, "admin", "approve", null).Should().BeNull();
        PendingFor(db, small.Id, 3).Should().ContainSingle();  // 财务收到
        PendingFor(db, small.Id, 2).Should().BeEmpty();        // 总经理未收到
    }

    // ── 会签：全部通过 ─────────────────────────────────────

    [Fact]
    public void CounterSign_RequiresAllApprovals()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var graph = new FlowGraph
        {
            Nodes = {
                Node("start", FlowNodeTypes.Start, "开始"),
                Node("n1", FlowNodeTypes.CounterSign, "会签节点", Approver("role", "oa_user")),
                Node("end", FlowNodeTypes.End, "结束"),
            },
            Edges = { Edge("start", "n1"), Edge("n1", "end") },
        };
        var defId = Seed(db, "counter_test", graph);

        var (inst, _) = engine.Start(new StartFlowRequest(defId, "会签", 1, "admin", "{}"));
        inst!.Status.Should().Be("pending");

        // role:oa_user → 张三(2) + 李四(3) 两个任务
        PendingFor(db, inst.Id, 2).Should().ContainSingle();
        PendingFor(db, inst.Id, 3).Should().ContainSingle();

        // 张三先通过 → 仍未结束（李四未通过）
        engine.CompleteTask(PendingFor(db, inst.Id, 2)[0].Id, 2, "张三", "approve", "ok").Should().BeNull();
        engine.GetInstance(inst.Id)!.Status.Should().Be("pending");

        // 李四通过 → 全部通过 → approved
        engine.CompleteTask(PendingFor(db, inst.Id, 3)[0].Id, 3, "李四", "approve", "ok").Should().BeNull();
        engine.GetInstance(inst.Id)!.Status.Should().Be("approved");
    }

    // ── 会签：任一驳回即驳回 ───────────────────────────────

    [Fact]
    public void CounterSign_SingleReject_RejectsInstance()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var graph = new FlowGraph
        {
            Nodes = {
                Node("start", FlowNodeTypes.Start, "开始"),
                Node("n1", FlowNodeTypes.CounterSign, "会签节点", Approver("role", "oa_user")),
                Node("end", FlowNodeTypes.End, "结束"),
            },
            Edges = { Edge("start", "n1"), Edge("n1", "end") },
        };
        var defId = Seed(db, "counter_reject_test", graph);

        var (inst, _) = engine.Start(new StartFlowRequest(defId, "会签驳回", 1, "admin", "{}"));

        engine.CompleteTask(PendingFor(db, inst!.Id, 2)[0].Id, 2, "张三", "reject", "不同意").Should().BeNull();
        engine.GetInstance(inst.Id)!.Status.Should().Be("rejected");
        // 李四的任务应被取消
        db.FlowTasks.Where(t => t.InstanceId == inst.Id && t.AssigneeId == 3).Single().Status.Should().Be("cancelled");
    }

    // ── 或签：任一通过即通过 ───────────────────────────────

    [Fact]
    public void OrSign_AnyApproval_Passes()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var graph = new FlowGraph
        {
            Nodes = {
                Node("start", FlowNodeTypes.Start, "开始"),
                Node("n1", FlowNodeTypes.OrSign, "或签节点", Approver("role", "oa_user")),
                Node("end", FlowNodeTypes.End, "结束"),
            },
            Edges = { Edge("start", "n1"), Edge("n1", "end") },
        };
        var defId = Seed(db, "orsign_test", graph);

        var (inst, _) = engine.Start(new StartFlowRequest(defId, "或签", 1, "admin", "{}"));

        engine.CompleteTask(PendingFor(db, inst!.Id, 2)[0].Id, 2, "张三", "approve", "ok").Should().BeNull();
        engine.GetInstance(inst.Id)!.Status.Should().Be("approved");
        // 李四的待办被取消
        db.FlowTasks.Where(t => t.InstanceId == inst.Id && t.AssigneeId == 3).Single().Status.Should().Be("cancelled");
    }

    // ── 并行网关：分叉 + 汇合 ──────────────────────────────

    [Fact]
    public void ParallelGateway_ForkJoin_ApprovedAfterAllBranches()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var graph = new FlowGraph
        {
            Nodes = {
                Node("start", FlowNodeTypes.Start, "开始"),
                Node("n1", FlowNodeTypes.Approval, "主管", Approver("user", "1")),
                Node("fork", FlowNodeTypes.ParallelGateway, "并行分叉"),
                Node("nA", FlowNodeTypes.Approval, "审批A", Approver("user", "2")),
                Node("nB", FlowNodeTypes.Approval, "审批B", Approver("user", "3")),
                Node("join", FlowNodeTypes.ParallelGateway, "并行汇合"),
                Node("end", FlowNodeTypes.End, "结束"),
            },
            Edges = {
                Edge("start", "n1"), Edge("n1", "fork"),
                Edge("fork", "nA"), Edge("fork", "nB"),
                Edge("nA", "join"), Edge("nB", "join"),
                Edge("join", "end"),
            },
        };
        var defId = Seed(db, "parallel_test", graph);

        var (inst, _) = engine.Start(new StartFlowRequest(defId, "并行", 2, "张三", "{}"));
        inst!.Status.Should().Be("pending");

        // 主管通过 → 分叉出两个并行任务
        engine.CompleteTask(PendingFor(db, inst.Id, 1)[0].Id, 1, "admin", "approve", null).Should().BeNull();
        PendingFor(db, inst.Id, 2).Should().ContainSingle();
        PendingFor(db, inst.Id, 3).Should().ContainSingle();

        // 只完成 A → 仍 pending（等 B 汇合）
        engine.CompleteTask(PendingFor(db, inst.Id, 2)[0].Id, 2, "张三", "approve", null).Should().BeNull();
        engine.GetInstance(inst.Id)!.Status.Should().Be("pending");

        // 完成 B → 汇合 → approved
        engine.CompleteTask(PendingFor(db, inst.Id, 3)[0].Id, 3, "李四", "approve", null).Should().BeNull();
        engine.GetInstance(inst.Id)!.Status.Should().Be("approved");
    }

    // ── 撤回 ──────────────────────────────────────────────

    [Fact]
    public void Cancel_ByApplicant_CancelsPendingTasks()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var graph = new FlowGraph
        {
            Nodes = {
                Node("start", FlowNodeTypes.Start, "开始"),
                Node("n1", FlowNodeTypes.Approval, "主管", Approver("user", "1")),
                Node("end", FlowNodeTypes.End, "结束"),
            },
            Edges = { Edge("start", "n1"), Edge("n1", "end") },
        };
        var defId = Seed(db, "cancel_test", graph);

        var (inst, _) = engine.Start(new StartFlowRequest(defId, "撤回", 2, "张三", "{}"));
        engine.Cancel(inst!.Id, 2).Should().BeNull();
        engine.GetInstance(inst.Id)!.Status.Should().Be("cancelled");
        db.FlowTasks.Where(t => t.InstanceId == inst.Id).All(t => t.Status == "cancelled").Should().BeTrue();
    }
}
