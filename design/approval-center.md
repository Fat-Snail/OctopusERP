# Octopus 审批中心（Approval Center）设计方案

> 目标：把 OA 现有的「简易审批」升级为**贯穿全平台的审批中台**，既支持请假/报销这类**零代码拖拽**的通用流程，也支持「审批中心需要看到子系统信息」这类**需要子系统定制 + 二次开发**的复杂流程。
>
> 参考对象：AntFlow / AntFlowCore（中国式审批）、活字格（低代码流程）、Elsa Workflows（.NET 活动模型）。

---

## 0. 结论先行

| 问题 | 结论 |
|---|---|
| 审批中心放哪？ | **逻辑上独立的中台**，短期复用 OA 引擎，长期抽成 `OctopusFlow` 服务 + 共享 NuGet SDK。OA 是它的第一个消费者，不是唯一宿主。 |
| 流程怎么建模？ | 从现在的**线性链表**（`NodeOrder`）升级为**图**（节点 + 边 + 网关），这是支持会签/分支/并行的前提。 |
| 通用 vs 定制怎么分？ | **两级能力**：`配置化（拖拽 + 内置节点）` 覆盖 80%，`扩展点（注册表 + 接口）` 覆盖 20%。绝不用 if-else 堆定制。 |
| 子系统怎么接入？ | **三件套**：NuGet SDK（嵌入子系统）+ REST API（中心服务）+ Webhook（事件回调）。 |
| 子系统信息怎么展示？ | **表单 Schema 协议 + 定制视图扩展点**：子系统声明 schema，审批中心按协议渲染；复杂场景子系统注册自定义 Vue 组件。 |
| 二次开发空间在哪？ | 自定义节点、服务任务、脚本节点、事件监听器、表单组件、审批人策略 —— 全部走**注册表**，业务方实现接口即可插拔。 |

---

## 1. 现状盘点（OctopusOA 现有审批）

已读代码，现状能力与缺口如下：

### 已有（可直接保留/迁移）

| 能力 | 位置 | 说明 |
|---|---|---|
| 流程模板 + 线性节点 | `WorkflowTemplate` / `WorkflowNode` | `NodeOrder` 排序，单人审批 |
| 提交/通过/驳回/撤回 | `ApprovalService` | 状态机 `draft/pending/approved/rejected/cancelled` |
| 审批人解析 | `ApproverType` = `user/role/dept_leader` | 基于 UMC 同步的用户/角色 |
| 跳过条件 | `WorkflowNode.SkipCondition` | 简单二元表达式（`amount < 1000`） |
| 转交 | `ApprovalDelegate` | A 转 B，卸责接管 |
| 抄送 | `WorkflowNode.CcUsersJson` | 节点到达时通知 |
| 业务联动 | `TemplateCode == "attendance_fix"` | 硬编码 switch，通过后调考勤 |
| 跨系统回调 | `WebhookSender.PostToCrm/PostToMes` | HMAC，OA→CRM/MES |
| SLA 超期 | `ApprovalSlaWorker` | BackgroundService 扫描 |
| 通知 | SignalR + UMC 统一通知 | 已打通 |

### 关键缺口（本方案要解决）

1. **流程是线性链**：没有「会签 / 或签 / 条件分支 / 并行网关 / 子流程」，`CurrentNodeOrder` 只能表示「一个当前节点」。
2. **业务联动是硬编码**：`if (TemplateCode == "attendance_fix")`。每加一种联动就要改引擎代码 —— 这正是「可扩展性差」的根源。
3. **表单是自由 JSON**：`FormSchema` 没有协议，子系统信息无法被审批中心「看懂并渲染」。
4. **子系统集成是半手工**：靠 HMAC webhook 回调 + `OaApprovalId` 手动关联，没有统一 SDK，无法「在子系统里发起审批 + 审批中心里看子系统数据」。
5. **没有流程设计器**：节点靠接口 `SetNodes` 硬配，没有拖拽可视化。
6. **没有流程版本**：改了模板，历史实例引用的是「活的」节点定义，不可追溯。

---

## 2. 总体架构

### 2.1 分层（对齐活字格的「定义/持久化/执行」+ Elsa 的「活动/插件」）

```
┌────────────────────────────────────────────────────────────┐
│ 前端层  Frontend                                           │
│  · 审批中心 Portal（待办/发起/详情/流程跟踪图）              │
│  · 流程设计器（拖拽，仿钉钉/飞书）                          │
│  · 表单设计器（Schema 拖拽）                                │
│  · 子系统定制视图模块（Slot 扩展点，按业务注册 Vue 组件）     │
├────────────────────────────────────────────────────────────┤
│ API 层（审批中心服务）                                      │
│  · 定义 API / 实例 API / 任务 API / 历史 API / 事件订阅 API │
├────────────────────────────────────────────────────────────┤
│ 引擎层 Engine（核心，纯 .NET，可被 NuGet 嵌入）              │
│  · 图执行器（token/分支/汇聚）                              │
│  · 节点执行器（内置节点 + 自定义节点分派）                  │
│  · 审批人策略器（用户/角色/上级/表达式/脚本/远程）           │
│  · 表达式引擎（条件分支，升级现有 EvaluateCondition）        │
│  · 事件总线（OnNodeEnter/OnNodeLeave/OnApproved/…）         │
├────────────────────────────────────────────────────────────┤
│ 扩展层 Extension（二次开发核心）                            │
│  · INodeExecutor / IApproverStrategy / IScriptExecutor      │
│  · IFormComponentRegistry（定制视图）                       │
│  · IProcessEventListener（生命周期钩子）                    │
├────────────────────────────────────────────────────────────┤
│ 持久化层 Persistence（EF Core，SQLite dev / MySQL prod）     │
│  · 定义（版本化）/ 实例 / 任务 / 执行路径 / 历史             │
│  · 业务数据（表单 JSON）与流程数据分离，可跨库                │
└────────────────────────────────────────────────────────────┘
```

### 2.2 部署形态（渐进演进）

```
Phase 1（复用）：审批中心 = OA 现有引擎的「重构升级」，OA 仍是宿主
Phase 2（抽取）：OctopusFlow 独立服务（端口 5011），OA/CRM/WMS/MES 通过 SDK 接入
Phase 3（中台化）：多租户 / 流程市场 / 跨库、跨子系统事件编排
```

> 关键：`OctopusFlow.Core` 从一开始就设计成**无宿主依赖的纯库**（对应 AntFlowCore 的「NuGet 包级嵌入」），这样 Phase 1 的 OA 内嵌和 Phase 2 的独立服务可以无缝切换。

---

## 3. 流程建模：线性链 → 图

### 3.1 图结构（核心数据结构）

用「节点 + 边」表达，存 JSON（可导出 BPMN 2.0 子集，未来可导入 Camunda/Activiti 模型）：

```jsonc
{
  "id": "proc_leave_v3",
  "version": 3,
  "nodes": [
    { "id": "start",       "type": "start" },
    { "id": "n1",          "type": "approval", "config": { "strategy": "dept_leader" } },
    { "id": "gw1",         "type": "exclusive_gateway" },
    { "id": "n2",          "type": "counter_sign", "config": { "strategy": "role", "value": "oa_manager", "passRule": "all" } },
    { "id": "n3",          "type": "approval", "config": { "strategy": "user", "value": "5" } },
    { "id": "task_wf",     "type": "service_task", "config": { "kind": "webhook", "url": "..." } },
    { "id": "end",         "type": "end" }
  ],
  "edges": [
    { "from": "start", "to": "n1" },
    { "from": "n1",    "to": "gw1" },
    { "from": "gw1",   "to": "n2", "condition": "amount >= 5000" },
    { "from": "gw1",   "to": "n3", "condition": "amount < 5000" },
    { "from": "n2",    "to": "task_wf" },
    { "from": "n3",    "to": "task_wf" },
    { "from": "task_wf", "to": "end" }
  ]
}
```

### 3.2 数据表演进

| 现状表 | 目标表 | 变化 |
|---|---|---|
| `WorkflowTemplate` | `wf_definition` | 加 `version`、`graph_json`、`form_schema(协议化)`、`category`、`business_key` |
| `WorkflowNode` | 并入 `graph_json`（另建 `wf_node` 冗余表便于查询） | 节点从「行」变「图里的顶点」 |
| `Approval` | `wf_instance` | `CurrentNodeOrder` → `current_executions`（并行需多个 token） |
| `ApprovalRecord` | `wf_task` + `wf_history` | 拆成「任务」（会签/或签的单元）和「历史」（只读轨迹） |
| —（无） | `wf_execution` | 执行路径/token，承载并行分支 |
| —（无） | `wf_custom_node_registry` / `wf_event_listener` | 二次开发注册表 |
| `ApprovalDelegate` | `wf_task.delegated` + 转交历史 | 保留，归入任务粒度 |

> 迁移策略：旧 `oa_approval*` 数据保留只读，新实例写入新表；提供一次性数据迁移脚本。

---

## 4. 通用流程（配置化，覆盖 80%）

### 4.1 内置节点类型（对齐 AntFlow「中国式审批全家桶」）

| 节点类型 | 说明 | 配置项 |
|---|---|---|
| `start` / `end` | 起止 | — |
| `approval` | **单人审批**（现有能力的保留） | 审批人策略 + SLA |
| `counter_sign` | **会签**（全部通过才通过） | 审批人策略 + `passRule: all` |
| `or_sign` | **或签**（一人通过即通过） | 审批人策略 + `passRule: any` |
| `sequential_sign` | **顺序签**（会签但按顺序逐个） | 审批人策略 + 顺序 |
| `exclusive_gateway` | **排他分支**（if/else） | 多条出边 + 条件表达式 |
| `parallel_gateway` | **并行网关**（分叉/汇聚） | fork/join |
| `inclusive_gateway` | **包容网关**（多选并行） | 多条带条件出边 |
| `cc` | **抄送**（知会，不阻塞） | 抄送人 + 触发时机 |
| `service_task` | **服务任务**（调 HTTP/发消息/写库） | kind: webhook/sql/script/http |
| `script` | **脚本节点**（动态逻辑） | C# 表达式/代码片段 |
| `sub_process` | **子流程**（复用另一流程） | 子流程定义引用 |
| `custom:{id}` | **自定义节点**（二次开发） | 见 §5 |

### 4.2 审批人策略（策略模式，现有 `user/role/dept_leader` 的扩展）

```
user: <umcUserId>            —— 指定人
role: <oaRole>               —— 指定角色（现有）
dept_leader: <level>         —— 部门主管/上级 N 级
applicant_self               —— 发起人自己
expression: <C# lambda>      —— 表达式动态计算（AntFlow Natasha 思路）
remote: <http url>           —— 远程接口返回审批人（子系统可自行决定）
```

> 每种策略是一个 `IApproverStrategy` 实现，注册进策略表 —— 新增策略不改引擎。

### 4.3 条件表达式（升级现有 `EvaluateCondition`）

现状只支持二元比较。目标用**表达式树解析器**支持：

```
amount >= 5000 && category == "urgent"
days > 3 || (type == "sick" && days > 1)
```

支持字段来自 `form_data` + 实例 `variables`。脚本节点可写任意 C#（Natasha 动态编译，受限沙箱）。

---

## 5. 定制流程（二次开发，覆盖 20%）

> 这是「可扩展、可二次开发」的核心。**所有扩展点都是「注册表 + 接口」，业务方只实现接口、装配时注册，引擎零改动。**

### 5.1 五类扩展点

| 扩展点 | 接口 | 用途示例 |
|---|---|---|
| **自定义节点** | `INodeExecutor` | 采购单审批通过后自动生成入库单（替代现有硬编码 switch） |
| **审批人策略** | `IApproverStrategy` | 「金额 >10 万走财务总监」这种跨系统规则 |
| **脚本执行器** | `IScriptExecutor` | 复杂业务规则、多字段联动计算 |
| **事件监听器** | `IProcessEventListener` | `OnApproved` 时同步库存 / 生成凭证 / 发外部系统 |
| **表单/视图组件** | `IFormComponentRegistry` | 审批中心渲染子系统定制区块（见 §6.3） |

```csharp
// 示例：采购审批通过 → 自动创建 WMS 入库单（现由 MES webhook 半手工做）
public class PurchaseApprovedListener : IProcessEventListener
{
    public Task OnApproved(ProcessContext ctx, CancellationToken ct)
    {
        if (ctx.Definition.BusinessKey != "purchase_order") return Task.CompletedTask;
        // 调用 WMS SDK 创建入库单
        return _wmsClient.CreateInboundOrder(ctx.FormData, ct);
    }
}
```

### 5.2 服务任务（解耦外部系统）

`service_task` 节点支持声明式配置：`webhook`（HTTP）、`script`（C#）、`sql`（受控）、`remote_api`（内部服务密钥）。业务方可通过 SDK 注册自己的 `IServiceTaskHandler`。

### 5.3 脚本即能力

引入动态编译（Natasha 思路）做**脚本节点**，让复杂审批人选择、条件、数据变换不用改代码：

```csharp
// 审批人脚本：金额超限走高层，否则走部门
return amount > 100000 ? GetUser("cfo") : GetDeptLeader(applicant);
```

> 安全约束：脚本在**受限沙箱**（无 IO / 网络白名单），仅用于纯计算与决策，副作用一律走 `service_task` / 事件监听器，保证可审计。

---

## 6. 子系统集成与「审批中心看子系统信息」

### 6.1 三件套集成

```
┌─────────────┐   SDK 嵌入（OctopusFlow.Client NuGet）   ┌──────────────┐
│ CRM / MES / │ ───────────────────────────────────────→ │              │
│ WMS / HRM … │   发起 / 查询 / 回调绑定（HMAC 内置）      │  审批中心    │
└─────────────┘                                         │  (OctopusFlow)│
     │  Webhook 回调（审批结果 / 节点事件）                 │              │
     └──────────────────────────────────────────────────→│              │
                                                          └──────┬───────┘
                                     REST API（待办/详情/流程跟踪）│
                                                          ┌──────┴───────┐
                                                          │ 各子系统前端  │
                                                          │（内嵌审批面板）│
                                                          └──────────────┘
```

- **SDK（嵌入）**：子系统 `builder.AddOctopusFlow()` 后即可 `_flow.Submit(new PurchaseApproval{...})`，无需手写 HMAC。
- **REST API（中心）**：待办聚合、流程跟踪、历史查询 —— 任何前端都能拉。
- **Webhook（回调）**：审批通过/驳回/节点事件回推子系统（保留并规整现有 OA→CRM/MES 链路）。

### 6.2 两种发起场景

| 场景 | 发起方式 |
|---|---|
| **子系统内做审批** | 子系统用 SDK 发起，`business_key` 关联子系统单据（如 `purchase_order:123`），审批中心只存流程 + 表单快照 |
| **主系统（审批中心）做审批** | 用户在审批中心 Portal 直接选通用模板发起（请假/报销） |

### 6.3 审批中心展示子系统信息（定制视图）

「审批中心要能看到子系统信息」分三档，逐级增加定制成本：

| 档位 | 机制 | 适用 |
|---|---|---|
| **L1 协议渲染** | 子系统通过 `form_schema`（JSON Schema）声明字段，审批中心通用渲染器自动展示 | 纯字段展示 |
| **L2 远程视图** | 审批中心 iframe / 微前端挂载子系统提供的只读详情页 URL | 已有子系统详情页 |
| **L3 定制组件** | 子系统注册 Vue 组件到 `IFormComponentRegistry`，审批中心按 `componentId` 动态加载 | 复杂联动、图表、地图等 |

```
// 审批中心详情页：FormSchema 声明里引用子系统组件
{
  "fields": [ { "key": "amount", "type": "number", "label": "金额" } ],
  "extensions": [
    { "slot": "after_form", "componentId": "mes:purchase-order-summary", "props": { "orderId": "123" } }
  ]
}
```

> 这与活字格的「业务数据库 + 流程数据库分离、流程引用业务数据」理念一致：审批中心不复制子系统数据，只存「关联键 + 展示协议 + 快照」。

---

## 7. 前端模块（审批中心 Portal + 设计器）

| 模块 | 说明 | 对应 OA 现状 |
|---|---|---|
| **待办中心** | 聚合全平台待办（会签/或签任务粒度） | 已有，升级为任务粒度 |
| **发起中心** | 通用模板发起 + 表单渲染 | 已有 `templates` |
| **流程跟踪图** | 节点高亮 + 分支走向 + 会签状态 | **新增**（现只有线性记录） |
| **流程设计器** | 拖拽节点 + 连线 + 条件编辑 + 版本发布 | **新增** |
| **表单设计器** | Schema 拖拽 + 自定义组件挂载 | **新增** |
| **定制视图 Slot** | 动态加载子系统注册组件 | **新增** |
| **管理端** | 流程定义版本管理、扩展点注册表查看、事件监听器管理 | **新增** |

> 设计器不重复造轮子，借鉴 AntFlow-Designer / LogicFlow 的交互；数据格式用 §3.1 的 JSON 图，避免绑死某个组件库。

---

## 8. 三个参考系统的借鉴点

| 参考 | 借鉴什么 | 不照搬什么 |
|---|---|---|
| **AntFlow / AntFlowCore** | ①「中国式审批全家桶」节点清单（会签/或签/加签/转办/抄送/回退/子流程）② NuGet 包级嵌入（`< 800KB` 引擎库）③ 脚本即能力（Natasha）④ 仿钉钉设计器交互 | 其 FreeSql/多租户全量依赖；我们用现有 EF Core 栈 |
| **活字格** | ① 三层分离（定义/持久化/执行）② **流程库与业务库分离** ③ **流程定义版本化** ④ 开发者设计 + 业务人员在线编辑 + 管理员运维的三角色分工 | 其整套低代码运行时；我们保留代码扩展能力 |
| **Elsa Workflows** | ① **Activity 模型**（一切节点是 Activity，可插拔）② Plugin/Module 注册机制 ③ 事件与 Workflow 实例分离 ④ 设计器 + 运行时解耦 | 其偏「自动化编排」的调度语义；审批需更强的人工任务/会签语义 |

**一句话总结**：用 Elsa 的「活动 + 插件」可插拔骨架，装 AntFlow 的「中国式审批节点」，按活字格的「分层 + 版本化 + 流程/业务分离」做中台化治理。

---

## 9. 分阶段落地路线

### Phase 1：引擎重构（图模型 + 会签/分支）· 兼容现有
- [ ] `wf_definition/wf_instance/wf_task/wf_execution/wf_history` 建表 + 迁移
- [ ] 图执行器：排他分支、并行网关、会签、或签
- [ ] 审批人策略接口化 + 表达式引擎升级
- [ ] 现有 OA 审批（leave/expense/attendance_fix）切到新引擎，行为不变，测试全绿

### Phase 2：扩展点 + 设计器
- [ ] `INodeExecutor` / `IProcessEventListener` / `IScriptExecutor` 注册表
- [ ] 服务任务（webhook/script/sql）替换现有硬编码 `attendance_fix` 联动
- [ ] 拖拽流程设计器 + 流程图 JSON 存储 + 版本发布

### Phase 3：中台化 + 子系统接入
- [ ] `OctopusFlow.Client` NuGet SDK（发起/查询/回调）
- [ ] CRM / WMS / MES 改用 SDK 接入（替换现有手写 HMAC）
- [ ] 审批中心 Portal 独立化（待办聚合、流程跟踪、定制视图 Slot）

### Phase 4：定制视图 + 二次开发样板
- [ ] `IFormComponentRegistry` + 子系统 Vue 组件注册
- [ ] 脚本节点沙箱 + 审计
- [ ] 一个完整「定制流程」样板（如采购审批 → 自动建入库单 → 审批中心看采购明细）

---

## 10. 需要你拍板的几个决策点

1. **独立服务 vs 复用 OA**：先复用 OA 演进（推荐，风险低），还是直接起 `OctopusFlow` 新服务？
2. **流程模型**：自研轻量图引擎（推荐，贴合现有 .NET/EF 栈），还是直接引入 AntFlowCore / Elsa 做内核？
3. **设计器**：用开源 LogicFlow/AntFlow-Designer 二次封装，还是自研？
4. **表单**：要不要顺带做「表单设计器」，还是先保持 Schema 协议、表单由各子系统自己渲染？
5. **首期定制样板**：选哪个业务做第一个「复杂定制流程」试点？（建议采购订单审批，能串起 MES/WMS/审批中心）

---

## 附：关键接口草图

```csharp
// 引擎核心（无宿主依赖，可嵌入或独立部署）
public interface IFlowEngine
{
    Task<Instance> StartAsync(StartRequest req, CancellationToken ct);   // 发起
    Task<TaskResult> CompleteTaskAsync(long taskId, string action, string? comment, long userId, CancellationToken ct); // 通过/驳回/转交
    Task<IReadOnlyList<Task>> GetPendingAsync(long userId, CancellationToken ct);
}

// 扩展点（二次开发实现并注册）
public interface INodeExecutor          { Task ExecuteAsync(NodeContext ctx, CancellationToken ct); }
public interface IApproverStrategy      { Task<IReadOnlyList<long>> ResolveAsync(NodeContext ctx, CancellationToken ct); }
public interface IProcessEventListener  { Task OnAsync(ProcessEvent evt, ProcessContext ctx, CancellationToken ct); }
public interface IScriptExecutor        { Task<object?> EvaluateAsync(string script, IDictionary<string, object?> vars, CancellationToken ct); }

// 子系统 SDK
public interface IFlowClient
{
    Task<long> SubmitAsync(SubmitModel model, CancellationToken ct);      // 发起（自动带业务键）
    Task<Instance> GetAsync(long id, CancellationToken ct);
    Task SubscribeAsync(string businessKey, Func<ProcessEvent, Task> handler, CancellationToken ct); // 订阅审批结果
}
```

---

## 11. 实现状态（Phase 1 · 已落地）

> 代码位置：`OctopusOA/src/OctopusOA.Api/Workflow/`（纯引擎，无 HTTP 依赖，可整包抽出）。
> 测试：OA **121** / MES **120** / WMS **42** 全绿。

### 已实现

| 能力 | 文件 | 说明 |
|---|---|---|
| 图模型 | `Workflow/FlowModel.cs` | `FlowGraph/FlowNode/FlowEdge`，节点类型字符串化 |
| 持久化 | `Workflow/FlowPersistence.cs` | `wf_definition/wf_instance/wf_task/wf_execution/wf_history` |
| 图执行器 | `Workflow/FlowEngine.cs` | 令牌模型；线性/排他网关/并行 fork-join；单人/会签/或签；驳回/撤回 |
| 表达式引擎 | `Workflow/ExpressionEvaluator.cs` | `&& \|\| !` 括号 + 比较 + null 字面量 |
| 审批人策略 | `Workflow/ApproverStrategy.cs` | `IApproverStrategy` + 注册表；内置 user/role/dept_leader |
| **服务任务** | `Workflow/FlowExtensions.cs` + `Services/CreateWmsInboundHandler.cs` | `IServiceTaskHandler`；采购通过→WMS 入库 |
| **事件监听器** | `Workflow/FlowExtensions.cs` + `Services/PurchaseApprovedListener.cs` | `IProcessEventListener`；采购通过→回推 MES |
| HTTP 端点 | `Controllers/FlowController.cs` | `/api/flow/*`，支持 `definitionId` 或 `definitionCode` 发起 |
| 采购流程 | `Persistence/OaDbSeeder.cs` | `purchase_order` 图定义（审批→条件→服务任务） |
| MES 接入 | `OctopusMES/Services/OaFlowClient.cs` | 采购提交→OA 流程；回调→采购单状态 |

### 采购订单 → MES/WMS 联动链路

```
MES 提交采购单
  → IOaFlowClient → OA POST /api/flow/submit (definitionCode=purchase_order)
  → 审批中心：采购主管 → 财务 → [金额≥10万→总经理] → service_task
  → CreateWmsInboundHandler → WMS POST /api/webhook/receive-goods (InboundType=purchase)
  → PurchaseApprovedListener → MES POST /api/webhook/approval-callback
  → MES 采购单状态 approved
```

### 明确边界（后续）

1. 现有 leave/expense/attendance_fix 仍走旧 `/api/approval`（**零回归**）；物理切换到新引擎 + 兼容映射留作下一步（避免破坏 P17 直连旧表的测试）。
2. 跨系统 HTTP 为配置门控 + fire-and-forget，与现有 webhook 模式一致；未做 OA↔MES↔WMS 三服务同进程端到端测试。
3. 前端流程设计器 / 审批中心 Portal / 定制视图 Slot 属 Phase 2+。

### 11.1 前端与迁移补充

- **前端**：审批中心 Portal（待办/发起/我的/详情+流程跟踪）+ 拖拽设计器（`web/.../src/views/flow/*`、`src/api/flow/*`），路由 `/flow/*` 与侧边栏已接入；`vue-tsc` 零错误、`vite build` 通过。
- **迁移**：`leave/expense/attendance_fix` 已种子为图定义，走 `/api/flow` 新引擎；`attendance_fix` 通过 → `AttendanceFixListener` 自动补卡（与旧路径等价）。
- **兼容**：旧 `/api/approval` 路径完整保留，**零回归**。
- **跨系统**：OA 出站载荷契约测试（`PurchaseFlowOutboundContractTests`）校验发往 WMS `receive-goods`（`InboundType=purchase`）与 MES `approval-callback` 的载荷；与 WMS/MES 接收测试共同构成链路契约验证。

### 11.2 表单能力分层（已实现 L1/L2）

| 档位 | 状态 | 说明 |
|---|---|---|
| L1 通用 Schema | ✅ | 可视化表单设计器（`views/flow/components/FormDesigner.vue`），拖字段生成 `{fields}` |
| L2 远程视图 | ✅ | `FormSchema.extensions: [{ type:'iframe', slot, title, url, height }]`，`DynamicForm` 在表单前/后渲染 iframe；设计器内可配置；示例 `/subsystem-demo.html` |
| L3 自定义组件 | ⏳ | `type:'component'` + `componentId` 已留协议，组件注册表待实现 |
| L4 流程逻辑定制 | ✅ | `IServiceTaskHandler` / `IProcessEventListener` / `IApproverStrategy` |

> L2 注意：被嵌入子系统页需允许 iframe（无 `X-Frame-Options: DENY`），且要有可访问的会话/签名 URL（跨域 iframe 不带 OA 的 Bearer token）。

### 11.3 L3 自定义组件（已实现）

- **注册表**：`web/.../src/lib/formComponentRegistry.ts`（`registerFormComponent / getFormComponent`）
- **注册处**：`src/lib/registerFormComponents.ts`（在 `main.ts` 引入）
- **表单引用**：`FormField.componentId`，`DynamicForm` 优先用注册组件渲染（编辑/只读都支持）
- **组件契约**：props `modelValue`、`readonly`；emit `update:modelValue`
- **设计器**：字段属性面板可填「自定义组件 ID」
- **示例**：`demo:product-picker`（`views/flow/components/DemoProductPicker.vue`），已挂在采购表单的「商品选择（自定义组件）」字段

### 11.4 详情页只读流程图

- 共享布局：`views/flow/flowLayout.ts`（设计器与详情预览共用 `layoutGraph` / `edgePath`，避免两处漂移）
- 只读组件：`views/flow/components/FlowGraphPreview.vue`
- 详情页 `flow/detail/index.vue` 新增「流程跟踪图」：节点按状态高亮（已通过/待处理/已驳回/未到达），保留时间线作为明细
- 状态推导：审批节点取 `tasks`（pending/approved/rejected）；非审批节点按 `history.nodeName` 匹配
