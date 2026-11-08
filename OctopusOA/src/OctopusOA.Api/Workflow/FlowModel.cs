using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctopusOA.Api.Workflow;

/// <summary>流程节点类型常量（字符串存储，支持 "custom:xxx" 这类二次开发扩展）</summary>
public static class FlowNodeTypes
{
    public const string Start = "start";
    public const string End = "end";
    public const string Approval = "approval";               // 单人审批
    public const string CounterSign = "counter_sign";        // 会签：全部通过
    public const string OrSign = "or_sign";                  // 或签：任一通过
    public const string ExclusiveGateway = "exclusive_gateway";
    public const string ParallelGateway = "parallel_gateway";
    public const string ServiceTask = "service_task";
    public const string Script = "script";
    public const string SubProcess = "sub_process";
}

/// <summary>流程定义图（节点 + 边），序列化为 FlowDefinition.GraphJson</summary>
public class FlowGraph
{
    public List<FlowNode> Nodes { get; set; } = new();
    public List<FlowEdge> Edges { get; set; } = new();

    public static FlowGraph FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new FlowGraph();
        return JsonSerializer.Deserialize<FlowGraph>(json, JsonOpts) ?? new FlowGraph();
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public FlowNode? Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);
    public FlowEdge? OutEdge(string nodeId) => Edges.FirstOrDefault(e => e.From == nodeId);
    public List<FlowEdge> OutEdges(string nodeId) => Edges.Where(e => e.From == nodeId).ToList();
    public List<FlowEdge> InEdges(string nodeId) => Edges.Where(e => e.To == nodeId).ToList();
}

public class FlowNode
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = FlowNodeTypes.Approval;
    public string Name { get; set; } = "";
    /// <summary>设计器画布坐标（引擎忽略，仅用于可视化布局）</summary>
    public double? X { get; set; }
    public double? Y { get; set; }
    public FlowNodeConfig? Config { get; set; }
}

public class FlowEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    /// <summary>排他/包容网关出边条件；空表示默认分支</summary>
    public string? Condition { get; set; }
}

public class FlowNodeConfig
{
    /// <summary>审批人策略：user / role / dept_leader / 自定义</summary>
    public string ApproverType { get; set; } = "user";
    public string? ApproverValue { get; set; }
    public int? SlaHours { get; set; }
    public string? SkipCondition { get; set; }
    public List<long>? CcUsers { get; set; }
    /// <summary>会签/或签通过规则：all / any（默认由节点类型决定）</summary>
    public string? PassRule { get; set; }
    /// <summary>服务任务类型：webhook / script / http / ...</summary>
    public string? ServiceKind { get; set; }
    public string? ServiceConfig { get; set; }
}
