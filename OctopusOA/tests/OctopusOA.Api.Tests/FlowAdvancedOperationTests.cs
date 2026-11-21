using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OctopusOA.Api.Persistence;
using OctopusOA.Api.Workflow;

namespace OctopusOA.Api.Tests;

/// <summary>
/// 图流程引擎高级操作测试：委托/转交、加签、驳回到指定节点。
/// 种子用户：admin(1) / zhangsan(2) / lisi(3)。
/// </summary>
public class FlowAdvancedOperationTests : IClassFixture<OATestFactory>
{
    private readonly OATestFactory _factory;
    public FlowAdvancedOperationTests(OATestFactory factory) => _factory = factory;

    private static FlowNode Node(string id, string type, string name, FlowNodeConfig? cfg = null)
        => new() { Id = id, Type = type, Name = name, Config = cfg };
    private static FlowEdge Edge(string from, string to, string? cond = null)
        => new() { From = from, To = to, Condition = cond };
    private static FlowNodeConfig User(string id) => new() { ApproverType = "user", ApproverValue = id };

    private static long Seed(OaDbContext db, string code, FlowGraph graph)
    {
        var def = new FlowDefinition { Code = code, Name = code, Version = 1, Status = 1, GraphJson = graph.ToJson() };
        db.FlowDefinitions.Add(def);
        db.SaveChanges();
        return def.Id;
    }

    private static FlowGraph LinearGraph() => new()
    {
        Nodes = {
            Node("start", FlowNodeTypes.Start, "开始"),
            Node("n1", FlowNodeTypes.Approval, "主管审批", User("1")),
            Node("n2", FlowNodeTypes.Approval, "HR 审批", User("1")),
            Node("end", FlowNodeTypes.End, "结束"),
        },
        Edges = { Edge("start", "n1"), Edge("n1", "n2"), Edge("n2", "end") },
    };

    // ── 委托/转交 ─────────────────────────────────────────

    [Fact]
    public void Delegate_TransfersTaskToAnotherUser()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var defId = Seed(db, "delegate_test", LinearGraph());
        var (inst, err) = engine.Start(new StartFlowRequest(defId, "委托测试", 2, "张三", "{}"));
        err.Should().BeNull();

        var task = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.Status == "pending");
        task.AssigneeId.Should().Be(1);

        // admin(1) 把任务转交给 lisi(3)
        engine.DelegateTask(task.Id, 1, "admin", 3, "李四", "出差委托").Should().BeNull();

        var after = db.FlowTasks.Single(t => t.Id == task.Id);
        after.AssigneeId.Should().Be(3);
        after.Action.Should().Be("delegate");
        // 原处理人不再有待办，新处理人有
        db.FlowTasks.Any(t => t.InstanceId == inst!.Id && t.AssigneeId == 1 && t.Status == "pending").Should().BeFalse();
        db.FlowTasks.Any(t => t.InstanceId == inst!.Id && t.AssigneeId == 3 && t.Status == "pending").Should().BeTrue();
        // 历史记录转交
        db.FlowHistories.Any(h => h.InstanceId == inst!.Id && h.Action == "delegate").Should().BeTrue();
    }

    [Fact]
    public void Delegate_ByNonAssignee_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var defId = Seed(db, "delegate_auth_test", LinearGraph());
        var (inst, _) = engine.Start(new StartFlowRequest(defId, "越权委托", 2, "张三", "{}"));
        var task = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.Status == "pending");

        // 张三(2) 不是处理人，不能转交
        engine.DelegateTask(task.Id, 2, "张三", 3, "李四", null).Should().Contain("无权");
    }

    // ── 加签 ─────────────────────────────────────────────

    [Fact]
    public void AddSign_RequiresAllSignersBeforeAdvancing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var defId = Seed(db, "addsign_test", LinearGraph());
        var (inst, err) = engine.Start(new StartFlowRequest(defId, "加签测试", 2, "张三", "{}"));
        err.Should().BeNull();

        var task = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.Status == "pending");
        var node1TaskId = task.Id;

        // admin(1) 加签 lisi(3)
        engine.AddSignTask(node1TaskId, 1, "admin", 3, "李四", "共同把关").Should().BeNull();
        var signTask = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.AssigneeId == 3 && t.Status == "pending");
        signTask.TaskKind.Should().Be("add_sign");

        // 原处理人先通过——节点不应推进（还有加签人未处理）
        engine.CompleteTask(node1TaskId, 1, "admin", "approve", "同意").Should().BeNull();
        db.FlowTasks.Any(t => t.InstanceId == inst!.Id && t.NodeId == "n2" && t.Status == "pending").Should().BeFalse();
        engine.GetInstance(inst!.Id)!.Status.Should().Be("pending");

        // 加签人通过后，节点才推进到 n2
        engine.CompleteTask(signTask.Id, 3, "李四", "approve", "同意").Should().BeNull();
        db.FlowTasks.Any(t => t.InstanceId == inst!.Id && t.NodeId == "n2" && t.Status == "pending").Should().BeTrue();
    }

    [Fact]
    public void AddSign_DuplicateUser_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var defId = Seed(db, "addsign_dup_test", LinearGraph());
        var (inst, _) = engine.Start(new StartFlowRequest(defId, "重复加签", 2, "张三", "{}"));
        var task = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.Status == "pending");

        // 加签给自己（引擎返回"已在当前处理人中"）
        engine.AddSignTask(task.Id, 1, "admin", 1, "admin", null).Should().NotBeNull();
    }

    // ── 驳回到指定节点 ───────────────────────────────────

    [Fact]
    public void RejectTo_RestartsAtTargetNode()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var defId = Seed(db, "rejectto_test", LinearGraph());
        var (inst, err) = engine.Start(new StartFlowRequest(defId, "驳回重审", 2, "张三", "{}"));
        err.Should().BeNull();

        // 推进到 n2
        var t1 = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.NodeId == "n1" && t.Status == "pending");
        engine.CompleteTask(t1.Id, 1, "admin", "approve", "同意").Should().BeNull();
        var t2 = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.NodeId == "n2" && t.Status == "pending");

        // n2 驳回到 n1
        engine.RejectTo(t2.Id, 1, "admin", "n1", "材料不全，退回重审").Should().BeNull();

        // n1 重新有待办，n2 无待办，实例仍 pending
        db.FlowTasks.Any(t => t.InstanceId == inst!.Id && t.NodeId == "n1" && t.Status == "pending").Should().BeTrue();
        db.FlowTasks.Any(t => t.InstanceId == inst!.Id && t.NodeId == "n2" && t.Status == "pending").Should().BeFalse();
        engine.GetInstance(inst!.Id)!.Status.Should().Be("pending");
        db.FlowHistories.Any(h => h.InstanceId == inst!.Id && h.Action == "reject_to").Should().BeTrue();

        // 重新走完 n1 → n2 → end
        var t1b = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.NodeId == "n1" && t.Status == "pending");
        engine.CompleteTask(t1b.Id, 1, "admin", "approve", "重审通过").Should().BeNull();
        var t2b = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.NodeId == "n2" && t.Status == "pending");
        engine.CompleteTask(t2b.Id, 1, "admin", "approve", "确认").Should().BeNull();
        engine.GetInstance(inst!.Id)!.Status.Should().Be("approved");
    }

    [Fact]
    public void RejectTo_InvalidTarget_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OaDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IFlowEngine>();

        var defId = Seed(db, "rejectto_invalid_test", LinearGraph());
        var (inst, _) = engine.Start(new StartFlowRequest(defId, "非法驳回", 2, "张三", "{}"));
        var task = db.FlowTasks.Single(t => t.InstanceId == inst!.Id && t.Status == "pending");

        engine.RejectTo(task.Id, 1, "admin", "start", null).Should().Contain("开始");
        engine.RejectTo(task.Id, 1, "admin", "no_such_node", null).Should().Contain("不存在");
    }
}
