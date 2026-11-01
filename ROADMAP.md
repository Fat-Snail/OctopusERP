# OctopusERP 全链路实施路线图

---

## 一、全链路架构总览

```
客户询盘 ─→ 报价 ─→ 合同 ─→ 备货 ─→ 生产/采购 ─→ 发货 ─→ 回款
  CRM          CRM     CRM      WMS       MES          WMS     CRM
   │            │       │        │          │            │       │
   └────────────┴───────┴────────┴──────────┴────────────┴───────┘
                         OA 审批流贯穿全链路（QUOTE / CONTRACT / PURCHASE / PAYMENT）
                         UMC 统一身份 / SSO / RBAC（所有模块共用）
                         PLM 商品主数据（SKU、BOM、价格基准）
                         BI 看板（时效 + 审批进度，以合同为主键聚合）
```

### 模块职责一览

| 模块 | 一句话职责 | 状态 | 端口 |
|------|-----------|------|------|
| **UMC** | 统一身份/SSO/RBAC，所有模块登录入口 | ✅ 完成 | 5001/5173 |
| **OA** | 审批流引擎贯穿全链路，待办+超时预警 | ✅ 完成 | 5002/5174 |
| **PLM** | 商品/SKU/BOM 主数据，报价和采购的价格基准 | ✅ 完成 | 5003/5175 |
| **CRM** | 询盘→报价→合同→回款，客户档案管理 | 🚧 P1 | 5004/5176 |
| **WMS** | 库存管理，入/出库，触发缺料备货 | ⬜ P2 | 5005/5177 |
| **MES** | 生产工单+采购申请，跟踪交期 | ⬜ P3 | 5006/5178 |

---

## 二、核心业务流（7 步）

```
① CRM：客户询盘
   销售新建询盘单，关联 PLM 商品 SKU，自动拉出厂价作为报价基准

② CRM → OA：报价审批（templateCode: QUOTE_APPROVAL）
   提交报价单 → OA 审批（销售经理 → 财务复核）
   SLA：≤ 2 个工作日；超时预警推送站内消息

③ CRM：签订合同
   客户确认后创建销售合同，状态变「执行中」，触发履约链路
   合同承诺交期写入 DeliveryDate → OTD 起算点

④ CRM → WMS → MES：备货决策
   合同创建后自动查 WMS 可用库存
   ├─ 有货 → WMS 生成出库预留单
   └─ 缺货 → MES 创建生产工单 / 采购申请

⑤ MES → OA → WMS：生产/采购
   采购超阈值须经 OA 审批（templateCode: PURCHASE_APPROVAL）
   完工/到货后写回 WMS 入库，库存更新

⑥ WMS → CRM：发货出库
   仓库拣货打包，录入物流单号
   回调 CRM 写入 ActualDeliveryDate（OTD 计算依据）

⑦ CRM → OA：对账回款（templateCode: PAYMENT_APPROVAL）
   销售上传发票、提交收款申请 → OA 财务审批
   通过后合同标记「已结清」，DSO 计算截止点
```

### 模块间事件衔接

所有跨模块通信统一走 **HTTP Webhook + HMAC-SHA256 签名**，复用 UMC→OA 的 UserSync 机制：

| 触发动作 | 来源 | 目标 | 事件/单据 |
|---------|------|------|----------|
| 报价单提交审批 | CRM | OA | `POST /api/approval/submit`（QUOTE_APPROVAL） |
| OA 审批通过/驳回 | OA | CRM | `POST /api/crm/approval-callback`（审批结果） |
| 合同签订 | CRM | WMS | `POST /wms/api/stock/check`（库存查询） |
| 库存不足 | WMS | MES | `POST /mes/api/workorder`（生产/采购工单） |
| 采购超阈值 | MES | OA | `POST /api/approval/submit`（PURCHASE_APPROVAL） |
| 生产完工 | MES | WMS | `POST /wms/api/inbound`（入库单） |
| 出库完成 | WMS | CRM | `POST /crm/api/contract/shipment`（物流信息） |
| 收款申请 | CRM | OA | `POST /api/approval/submit`（PAYMENT_APPROVAL） |
| 用户变更 | UMC | 所有 | UserSync Webhook（现有机制复用） |
| SKU/价格变更 | PLM | CRM | `POST /crm/api/plm/sync`（商品同步） |

---

## 三、时效 BI 核心指标

| 指标 | 计算逻辑 | 数据来源 | SLA |
|------|---------|---------|-----|
| 询盘转报价时效 | 询盘创建 → 报价提交（工作小时） | CRM | ≤ 8h |
| 报价审批时效（TAT） | OA 节点进入 → 审批完成（工作小时） | OA ApprovalRecord | ≤ 2 工作日 |
| 合同签署周期 | 合同创建 → 状态「执行中」（日历天） | CRM | ≤ 7天 |
| **客户侧 OTD** | 出库时间 ≤ 合同 DeliveryDate 准时率 | WMS + CRM | ≥ 95% |
| 供应侧交期达成率 | MES 实际完工 ≤ 计划完工 | MES | ≥ 90% |
| 审批超时率 | 超 SLA 未完成 / 全部审批 | OA | ≤ 5% |
| DSO 应收账期 | 出库日 → 回款日均值（日历天） | CRM | ≤ 30天 |

**BI 看板结构**：以合同为主键，漏斗视图展示各阶段在途数量/平均停留时间；支持按客户/产品线/销售员/月份下钻。

---

## 四、审批可观察性设计

复用 OA 现有 `Approval` + `ApprovalRecord` 体系，扩展三处：

### 1. 单据时间轴（前端组件）
每张业务单据（报价/合同/采购单）详情页嵌入「审批进度」侧栏：
- 直接调用 `GET /api/approval/{id}` 渲染时间轴
- 显示：节点名 → 操作人 → 时间 → 意见 → 用时
- 超时节点标红🔴，进行中节点高亮橙色🟠

### 2. SLA 超时预警
- `WorkflowNode` 新增 `SlaHours` 字段
- 审批引擎推进节点时写入 `CurrentNodeDeadline = 进入时间 + SlaHours`
- 前端倒计时：超时前 4h 变⚠️黄色，超时后变🔴红色

### 3. 催办通知
- OA 定时任务（每 30 分钟）扫描 `CurrentNodeDeadline < UtcNow + 4h` 的在途审批
- 通过 `NoticeService` 推送站内消息给当前节点审批人

---

## 五、P1：CRM 客户关系管理

> **依赖**：UMC（SSO）+ OA（审批流）+ PLM（SKU价格基准）— 均已完成

### 数据模型（10 张表）

| 表名 | 说明 |
|------|------|
| `crm_sync_user` | 从 UMC 同步的用户缓存（同 OA 模式） |
| `crm_customer` | 客户档案（编码/名称/行业/等级/状态） |
| `crm_contact` | 客户联系人（一对多，标记主联系人） |
| `crm_inquiry` | 询盘（关联客户/负责人/预计交期） |
| `crm_inquiry_item` | 询盘明细（产品/数量/规格） |
| `crm_quote` | 报价单（关联询盘/总金额/有效期/OA审批ID） |
| `crm_quote_item` | 报价明细（单价/数量/小计） |
| `crm_contract` | 销售合同（承诺交期 DeliveryDate/实际交期/OA审批ID） |
| `crm_contract_item` | 合同明细 |
| `crm_payment` | 回款记录（金额/日期/OA审批ID） |

### 状态机

```
询盘：open → quoted → won / lost
报价单：draft → pending_approval → approved / rejected → confirmed
合同：draft → pending_approval → active → shipped → completed / terminated
回款：pending → pending_approval → confirmed
```

### API 接口清单

```
客户管理：
GET/POST/PUT/DELETE  /api/customer
GET                  /api/customer/{id}/contacts
POST/PUT/DELETE      /api/customer/{id}/contact

询盘管理：
GET/POST/PUT/DELETE  /api/inquiry
POST/DELETE          /api/inquiry/{id}/item

报价管理：
GET/POST/PUT/DELETE  /api/quote
POST/DELETE          /api/quote/{id}/item
PUT                  /api/quote/{id}/submit      提交审批
PUT                  /api/quote/{id}/confirm     客户确认报价

合同管理：
GET/POST/PUT/DELETE  /api/contract
PUT                  /api/contract/{id}/submit   提交审批
PUT                  /api/contract/{id}/execute  审批通过→执行
PUT                  /api/contract/{id}/ship     记录发货
PUT                  /api/contract/{id}/complete 合同完成

回款管理：
GET/POST             /api/payment
PUT                  /api/payment/{id}/submit    提交收款审批

统计看板：
GET                  /api/stats/summary          销售漏斗 + 时效指标
GET                  /api/stats/pipeline         合同在途进度
GET                  /api/stats/overdue           逾期预警

审批回调：
POST                 /api/approval-callback       接收 OA 审批结果（HMAC）

UMC 集成：
POST                 /api/users/sync              用户同步（HMAC）
POST                 /api/auth/backchannel-logout OA/UMC 登出
GET                  /api/me                      当前用户信息
```

### 测试计划

| 测试文件 | 测试数 | 覆盖重点 |
|---------|--------|---------|
| `CustomerFlowTests.cs` | 8 | 客户CRUD + 联系人 + 等级筛选 |
| `SalesPipelineFlowTests.cs` | 10 | 询盘→报价→审批回调→确认→合同 |
| `ContractPaymentTests.cs` | 8 | 合同执行→发货→回款→DSO计算 |
| `StatsTests.cs` | 4 | 漏斗统计 + 时效指标 + 逾期预警 |

---

## 六、P2：WMS 仓储管理（待开发）

> 依赖：CRM（合同明细）+ PLM（SKU物料码）

### 最小可用功能
- 商品库存管理（按 SKU + 仓库）
- 入库单（采购到货 / 生产完工）
- 出库单（销售发货）
- 库存盘点
- 库存预警（安全库存）

### 核心数据表
```
wms_warehouse       仓库档案
wms_location        库位
wms_inventory       库存（sku + 仓库 + 批次 + 可用量/预留量）
wms_inbound_order   入库单（含明细）
wms_outbound_order  出库单（含明细，关联 crm_contract）
wms_stocktake       盘点单
```

---

## 七、P3：MES 生产与采购（待开发）

> 依赖：WMS（缺料触发）+ OA（采购审批）+ PLM（BOM展开）

### 最小可用功能
- 生产工单（关联合同 + BOM展开物料）
- 采购申请（超阈值走 OA 审批）
- 供应商档案
- 工单进度跟踪（待产 / 生产中 / 完工 / 入库）
- 采购到货登记（写回 WMS）

### 核心数据表
```
mes_supplier        供应商档案
mes_purchase_order  采购单（关联供应商 + WMS入库）
mes_work_order      生产工单（关联 crm_contract + plm_product）
mes_wo_progress     工单进度记录
```

---

## 八、P4：全链路 BI 看板（✅ 2026-05-04 完成）

### 已交付看板页面
1. **销售漏斗**：询盘→报价→合同各阶段数量 + 转化率
2. **时效仪表盘**：7 大指标当前值 vs SLA 目标，RAG 红绿黄
3. **合同全链路时间轴**：选定合同，展示从询盘到回款的完整时间节点
4. **审批积压**：CRM 侧报价/合同/回款三类待审批数量 + 超时计数
5. **OTD 趋势折线**：6 个月月度趋势，SVG 折线 + 95% SLA 参考线

### 后端接口
```
GET /api/stats/bi/efficiency          时效仪表盘 7 项指标
GET /api/stats/bi/otd-trend           OTD 月度趋势
GET /api/stats/bi/approval-backlog    审批积压
GET /api/stats/bi/contract-timeline/{id}  合同时间轴
GET /api/stats/bi/contracts           合同漏斗数据
```

---

---

## 九、P5：H5 移动端（OA + UMC）

> 设计文档：`design/oa-h5.md`、`design/umc-h5.md`、`design/tokens.md`（2026-05-09 产出）

### 定位

| 系统 | H5 范围 | 新建目录 | 端口 |
|---|---|---|---|
| UMC H5 | SSO 登录页（所有子系统入口）+ 个人资料/改密 | `OctopusUMC/web/OctopusUMC.H5/` | 5179 |
| OA H5 | 员工高频场景：打卡/审批/公告/通讯录/会议室 | `OctopusOA/web/OctopusOA.H5/` | 5180 |

### 技术栈

```
Vue 3 + Vite + TypeScript（strict）
UnoCSS（atomic CSS，比 Tailwind 更适合 H5 体积）
oidc-client-ts（复用现有 OIDC 配置，无需新增 UMC 客户端）
底部 TabBar + 顶部 NavBar（自定义，不依赖 reka-ui）
viewport-fit=cover，safe-area-inset 支持刘海屏
```

### OA H5 导航架构

```
底部 TabBar：[首页] [审批] [考勤] [我的]

首页      → 4 统计卡 + 快捷入口宫格 + 待办列表
审批      → Segment（发起/我的/待审批）+ 详情页（含通过/驳回）
考勤      → 打卡卡片 + 月历 → 月度明细列表
我的      → 资料展示 + 退出登录

独立页面（从快捷入口进入）：
公告列表/详情 / 通讯录 / 会议室预订
```

### 不做的功能（管理员操作，保留桌面端）

审批模板管理、考勤统计（管理视角）、班次管理、会议室管理、用户管理

---

## 十、P6：跨系统 Webhook 调用链

> Review 发现：ROADMAP 设计的 8 条跨系统事件链，只有接收方（Controller），缺少发送方（主动 POST）。

### 当前缺口

| 事件 | 接收方 | 发送方 |
|---|---|---|
| OA 审批完成 → CRM 回调 | ✅ `CRM ApprovalCallbackController` | ❌ OA 无主动 POST |
| CRM 合同生成 → WMS 库存预留 | ❌ WMS 缺此端点 | ❌ CRM 无调用 |
| MES 工单完工 → WMS 自动入库 | ❌ WMS 缺此端点 | ❌ MES 无调用 |
| WMS 出库完成 → CRM 回填发货日 | ❌ CRM 无此 Webhook | ❌ WMS 无调用 |

### 实现方案

统一 `WebhookSender` 工具类（复用 `UserSyncService` 的 HMAC 模式）：

```csharp
// 复用现有 HMAC-SHA256 签名机制，新增发送能力
public class WebhookSender(IHttpClientFactory factory, IConfiguration config, ILogger<WebhookSender> log)
{
    public async Task PostAsync(string baseUrl, string path, object payload, string secret)
    {
        var json = JsonSerializer.Serialize(payload);
        var sig  = ComputeHmacSha256(json, secret);
        var req  = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{path}");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Headers.Add("X-Signature", $"sha256={sig}");
        try { await factory.CreateClient().SendAsync(req); }
        catch (Exception ex) { log.LogError(ex, "Webhook 发送失败 {Path}", path); }
        // 注意：失败只记日志，不阻塞主流程（最终一致性）
    }
}
```

### 新增端点清单

```
WMS  POST /api/stock/reserve              CRM 合同生成时调用，库存预留
WMS  POST /api/inbound/receive-from-mes  MES 完工时调用，自动入库
CRM  POST /api/contract/ship-callback     WMS 出库完成时调用，回填 ActualDeliveryDate
```

### 配置节（各系统 appsettings.json）

```json
"Webhooks": {
  "CrmBaseUrl": "http://localhost:5004",
  "WmsBaseUrl": "http://localhost:5005",
  "MesBaseUrl": "http://localhost:5006",
  "OaBaseUrl":  "http://localhost:5002",
  "SharedSecret": "octopus-webhook-secret-dev"
}
```

---

## 十一、P7：OA 审批 SLA 催办

> ROADMAP 四章有完整设计，但实体字段和后台任务均未实现。

### 数据模型变更

```csharp
// WorkflowNode 新增
public int? SlaHours { get; set; }          // null = 不限制

// Approval 新增
public DateTime? CurrentNodeDeadline { get; set; }  // 推进节点时计算写入
```

### BackgroundService

```csharp
public class ApprovalSlaWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await using var scope = provider.CreateAsyncScope();
            var db     = scope.ServiceProvider.GetRequiredService<OaDbContext>();
            var notice = scope.ServiceProvider.GetRequiredService<NoticeService>();

            var now = DateTime.UtcNow;
            // 超时未处理 → 催办
            var overdue = await db.Approvals
                .Where(a => a.Status == "pending"
                         && a.CurrentNodeDeadline.HasValue
                         && a.CurrentNodeDeadline < now)
                .ToListAsync(ct);
            foreach (var a in overdue)
                await notice.SendAsync(a.CurrentApproverId, $"审批催办：{a.Title} 已超期");

            // 即将超时（4h 内）→ 预警
            var warning = await db.Approvals
                .Where(a => a.Status == "pending"
                         && a.CurrentNodeDeadline.HasValue
                         && a.CurrentNodeDeadline > now
                         && a.CurrentNodeDeadline < now.AddHours(4))
                .ToListAsync(ct);
            foreach (var a in warning)
                await notice.SendAsync(a.CurrentApproverId, $"审批预警：{a.Title} 将在 4 小时内超期");

            await Task.Delay(TimeSpan.FromMinutes(30), ct);
        }
    }
}
```

### 种子 SLA 建议

| 模板 | 节点 | SlaHours |
|---|---|---|
| 请假审批 | 直属主管 | 48 |
| 请假审批 | HR 确认 | 24 |
| 报销审批 | 直属主管 | 24 |
| 报销审批 | 财务审核 | 24 |
| 报销审批 | 总经理 | 48 |
| 补卡审批 | 全部节点 | 4 |

---

## 十二、P8：生产就绪

### 数据库迁移（SQLite → MySQL）

```bash
# 每个系统单独执行
cd OctopusXXX/src/OctopusXXX.Api
dotnet ef migrations add InitialCreate
dotnet ef migrations script > migration.sql   # 人工审核后执行
```

`appsettings.Production.json` 中替换连接串为 MySQL；测试继续使用 SQLite `:memory:`，双轨不冲突。

### Redis 缓存

优先缓存（TTL 5 分钟，CRUD 时主动失效）：

| 缓存 Key | 数据 | 失效触发 |
|---|---|---|
| `umc:menu-tree` | UMC 菜单树 | MenuController CRUD |
| `umc:user-perms:{userId}` | 用户权限 Claims | 角色绑定变更 |
| `oa:dept-tree` | OA 部门树 | 部门同步 Webhook |
| `plm:category-tree` | PLM 类目树 | CategoryController CRUD |

### 安全加固

| 项目 | 涉及系统 | 实现方式 |
|---|---|---|
| 统一异常中间件（不暴露堆栈） | 全部 | `UseExceptionHandler` 返回 `{ code:500, msg:"服务器内部错误" }` |
| 接口限流 | CRM/WMS/MES 缺失 | 复制 UMC 的 `AddRateLimiter(FixedWindow)` 配置 |
| 操作日志 AOP | CRM/WMS/MES 缺失 | 参考 UMC `OperationLogAttribute` 实现 |
| OpenIddict 生产证书 | UMC | 替换 `AddDevelopmentEncryptionCertificate` → `.pfx` |
| 强制 HTTPS | 全部生产环境 | `UseHttpsRedirection` + `UseHsts` |

---

## 十三、P9：统一通知中心

> 目标：各子系统的关键事件（审批结果/库存预警/工单完工）统一通过 UMC SignalR 推送到前端铃铛。

### 架构

```
CRM/WMS/MES 关键事件发生
  → POST UMC /api/internal/notify（内网 HMAC 签名）
  → UMC NoticeService 持久化 + OnlineUserHub.SendAsync("Notification", payload)
  → 前端 SignalR 客户端接收 → 铃铛未读数 +1 → 点击展开跨系统通知列表
```

### 通知类型

```typescript
type NotificationType =
  | 'approval_result'    // OA 审批通过/驳回
  | 'approval_reminder'  // SLA 催办
  | 'stock_alert'        // WMS 库存低于安全线
  | 'workorder_done'     // MES 工单完工
  | 'notice'             // UMC 公告
  | 'mention'            // @我的消息（预留）
```

### 新增接口

```
POST /api/internal/notify     接收各系统推送（HMAC-SHA256 签名，不对外暴露）
GET  /api/notifications       当前用户通知列表（分页）
PUT  /api/notifications/read-all  全部标记已读
```

---

## 十四、P10：PLM 升级（列表强化 + 类目模板进阶 + 跨系统标准化 API）

> **痛点**：当前商品列表信息密度低、无图标、跨系统调用零散；类目模板版本机制后端有但前端可用性弱。
> **对标**：金蝶云星空·物料主数据、有赞·商品中心。

### 14.1 商品列表展示强化

**新增字段（ProductResponse 扩展）**：

| 字段 | 来源 | 说明 |
|---|---|---|
| `thumbnailUrl` | PLM 本地 | 主图缩略图 80×80（前端列首列展示） |
| `skuCount` | PLM `Skus.Count` | SKU 总数徽标 |
| `priceRange` | PLM `Skus.Min/Max(p.Price)` | "¥999 ~ ¥1299" 区间显示 |
| `totalStock` | **WMS 实时聚合** | 跨仓库存合计（带预留量提示） |
| `salesQty30d` | **CRM 30 日合同** | 近 30 天销量（用于热销排序） |
| `channelBindings` | PLM ChannelMapping | 已上架渠道徽标（1688/淘宝/官网） |
| `statusIcon` | PLM 内部 | 状态图标 + 颜色（草稿灰/审核橙/上架绿/下架红） |
| `lifecycleStage` | 计算字段 | 新品/在售/促销/清仓/停产 |

**前端改造**：

- [ ] `ProductListView.vue` 替换为 TanStack Table 卡片+表格混合视图
- [ ] 列首列：缩略图（点击大图预览）+ ProductName + ProductCode（mono）
- [ ] 状态列：使用 lucide-vue-next 图标（`FileEdit` 草稿 / `Clock` 审核中 / `CheckCircle2` 已上架 / `XCircle` 已下架）
- [ ] 排序支持：按销量、按更新时间、按价格、按库存
- [ ] 高级筛选抽屉：类目树 + 状态多选 + 价格区间 + 上架渠道 + 创建人
- [ ] 列表行右键/长按菜单：快速操作（提交审核/克隆商品/复制 SKU/上下架）

### 14.2 类目模板进阶

**新增能力**：

- [ ] **属性模板可视化编辑器**：左拖右放绑定 SPU/SKU 属性，预览 SKU 矩阵
- [ ] **版本对比页**：左右两栏 diff，新增/删除属性高亮
- [ ] **版本回滚**：从历史版本一键回滚（需二次确认 + 审计日志）
- [ ] **批量应用**：将类目模板变更批量应用到子类目下的全部商品
- [ ] **模板继承**：子类目自动继承父类目属性，可覆盖

**API**：

```
GET    /api/category/{id}/template/versions       版本列表
GET    /api/category/{id}/template/diff?from=&to= 版本差异
PUT    /api/category/{id}/template/rollback/{ver} 回滚到指定版本
POST   /api/category/{id}/template/apply-all      批量应用到子类目商品
```

### 14.3 跨系统标准化 API（PLM 作为商品主数据源）

**统一商品资料查询接口**：

```
POST /api/product/by-codes              批量查询（CRM/WMS/MES 共用）
  Request:  { codes: ["P001","P002"] }
  Response: { items: [{ productId, code, name, mainImage, price, ... }] }

POST /api/product/by-ids                按 ID 批量查询（性能场景）

GET  /api/product/{id}/cross-system     跨系统数据聚合
  Response: {
    plm: { ... }, 
    wms: { totalStock, byWarehouse: [...] },     // WMS 调用 PLM 时 webhook 反向聚合
    crm: { salesQty30d, contractCount },
    mes: { activeWorkOrders, plannedQty }
  }
```

**事件订阅（PLM → 其他系统）**：

| 事件 | 推送目标 | 用途 |
|---|---|---|
| `product.created` | CRM/WMS/MES | 新增商品自动同步基础资料 |
| `product.updated` | CRM/WMS/MES | 名称/价格/规格变更 |
| `product.published` | CRM/WMS | 上架后允许下单/入库 |
| `product.discontinued` | CRM/WMS/MES | 停产后 CRM 禁止报价、WMS 标记呆滞 |
| `sku.price_changed` | CRM | 重新计算未签合同的报价 |

实现方式：复用 `WebhookSender`（见十章），各系统订阅自己关心的事件，PLM 维护订阅列表配置。

### 14.4 验收标准

- [ ] 商品列表首屏 SSR 友好，1000 条商品滚动流畅（虚拟列表）
- [ ] 跨系统聚合接口 P95 ≤ 200ms（缓存命中场景）
- [ ] 类目模板回滚必须有审计记录（谁/何时/旧版本/新版本）
- [ ] PLM 商品事件订阅链路集成测试 ≥ 5 个

---

## 十五、P11：WMS 升级（参考聚水潭，从基础库存升级到执行级 WMS）

> **痛点**：当前 WMS 仅支持仓库/库存/入出库单据，没有库位、波次、批次、面单，无法承载真实仓库作业。
> **对标**：聚水潭 WMS、旺店通、E-WMS。

### 15.1 多货主多仓 + 库区/库位精细化

**数据模型新增/改造**：

```
wms_owner            货主（多店铺/多公司复用同一仓库）
  - OwnerId, OwnerCode, OwnerName, ContactInfo

wms_warehouse        增加 OwnerId（按货主隔离）

wms_zone             库区（拣货区/存储区/退货区/质检区/不良品区）
  - ZoneId, WarehouseId, ZoneCode, ZoneType, PriorityOrder

wms_location         扩展字段：
  - ZoneId, BinType（货架/平台/托盘）, AbcClass（A/B/C 分类）
  - MaxQty, IsLocked, LastMoveAt

wms_inventory        改造按"仓库 + 库位 + 批次 + 货主"四维聚合
  - 唯一索引：(WarehouseId, LocationId, PlmProductId, BatchNo, OwnerId)
```

**ABC 分类规则**：根据近 30 天出库频次自动归类，A 类放靠近出货口的库位（AdvanceWmsService 定时计算）。

### 15.2 批次 / 序列号管理

```
wms_batch            批次表
  - BatchId, BatchNo, PlmProductId, SupplierId, ProduceDate, ExpireDate
  - InitialQty, RemainQty, QualityStatus（pending/passed/rejected）

wms_serial           序列号表（贵重商品/电子产品）
  - SerialId, SerialNo, PlmProductId, BatchId
  - Status（in_stock/shipped/returned/scrapped）
  - 出入库每个序列号都要扫码登记
```

**先进先出（FIFO）/ 近效期先出（FEFO）策略**：出库分配时按 `ExpireDate ASC` 自动选批次。

### 15.3 入库流程升级

```
采购到货登记 → 卸货分拣 → 库位上架（PDA 扫描）→ 上架确认 → 库存生效
       ↓
入库质检（QC）— 抽检/全检规则 → 合格入库 / 不良品入退货区
```

**新增表**：

```
wms_inbound_qc           入库质检单
  - QcId, InboundId, SampleSize, PassedQty, RejectedQty, QcOperator
  - QcResult, Photos, Remark
```

**关键能力**：
- [ ] PDA H5 扫码上架（已有 H5 设计基础，扩展到 PDA 端）
- [ ] 收货差异处理：实收 ≠ 应收时触发 OA 审批（templateCode: `INBOUND_DIFF_APPROVAL`）
- [ ] 一物一码（贵重商品强制扫描序列号）

### 15.4 出库流程升级（核心：波次拣货）

```
销售订单（CRM 合同 / 多渠道）
   ↓
订单池（待波次） → 创建波次 → 拣货任务（按库位路径优化）
   ↓                ↓
拣货员 PDA 拣货 → 复核区扫描 → 打包 → 称重打面单 → 发货扫描出门
```

**新增表**：

```
wms_wave             波次单（一波多单合并拣货）
  - WaveId, WaveCode, Status, OrderCount, SkuCount
  - PickerId, PickStartAt, PickFinishAt

wms_pick_task        拣货任务（波次内每条拣货明细）
  - TaskId, WaveId, OrderId, LocationId, PlmProductId, BatchNo
  - PickedQty, PickedBy, PickedAt

wms_package          包裹
  - PackageId, OutboundId, WeightG, LengthCm, WidthCm, HeightCm
  - LogisticsCompany, TrackingNo, ShippedAt
```

**波次创建策略（参考聚水潭）**：

| 策略 | 说明 |
|---|---|
| 单品单订单 | 1 SKU 单品订单合并波次，超快拣 |
| 多品订单分播 | 多品订单做"播种墙"二次分拣 |
| 同库位归并 | 同库位的拣货任务合并，减少行走距离 |
| 急单优先 | 标记加急的订单单独成波，优先发货 |

### 15.5 三方物流接入（面单 + 物流跟踪）

**新增模块**：

```
wms_logistics_provider     物流商配置
  - ProviderId, ProviderCode（SF/YTO/STO/JD/EMS）
  - ApiBaseUrl, AppKey, AppSecret, MerchantId
  - SupportedServices（同城/普通/冷链）

wms_logistics_account      物流账号（多店铺多账号）
wms_waybill                运单（面单）
  - WaybillId, PackageId, TrackingNo, WaybillImageUrl
  - ProviderCode, ServiceType, EstimatedArrival
```

**第三方对接**：
- [ ] 顺丰开放平台 API（下单/取消/电子面单/路由查询）
- [ ] 中通/韵达/圆通通过快递鸟（KdNiao）统一对接
- [ ] 自取面单 / 客户自送
- [ ] 物流路由轨迹（每 4h 拉取一次状态，写入 `wms_logistics_track`）
- [ ] 异常物流自动告警（卡件超 3 天/退回件）

### 15.6 库内移库 + 调拨 + 盘点升级

**库内移库**：A 库位 → B 库位（一步操作，PDA 扫描原位 → 扫描目标位）

**调拨单**：A 仓 → B 仓（出库 + 在途 + 入库三段式）

**盘点升级**：
- [ ] 循环盘点（按 ABC 分类频率，A 类每月、B 类每季度、C 类每半年）
- [ ] 抽盘（按库位/批次抽样）
- [ ] 盲盘（盘点员看不到账面数量，避免主观偏差）
- [ ] 差异自动生成调整单 → OA 审批通过后调整库存

### 15.7 验收标准

- [ ] 单波次 100 单合并拣货，路径优化后行走距离减少 ≥ 40%
- [ ] 顺丰电子面单接口对接通，能下单 + 打印 + 跟踪
- [ ] 序列号商品 100% 强制扫码出入库
- [ ] 循环盘点定时任务跑起来，差异率展示在 BI

---

## 十六、P12：MES 升级（时效统计 + OEE + 节拍 + 瓶颈分析）

> **痛点**：用户反馈 MES 没有时效统计；目前只有工单 CRUD，缺少生产数据沉淀。
> **对标**：金蝶云星空·制造、SAP MES。

### 16.1 工单时效统计

**新增字段（MesWorkOrder 扩展）**：

```
PlannedStartTime / PlannedEndTime    计划时段
ActualStartTime / ActualEndTime      实际时段
PlannedDurationHr / ActualDurationHr 计算字段
ScheduleVariance                     进度偏差（实际 - 计划，正数表示延迟）
LeadTimeHr                           交期周期（接单 → 完工）
```

**统计指标接口**：

```
GET /api/stats/wo-efficiency
  → {
      onTimeRate,            // 按期完工率
      avgLeadTime,           // 平均交期周期
      scheduleAdherence,     // 排程依从度
      bottleneckProcesses,   // 瓶颈工序 TOP 5
      operatorEfficiency     // 工人效率排名
    }

GET /api/stats/wo-trend?months=6     6 月趋势（按期率/产量折线）
GET /api/stats/process-cycle-time    工序节拍分析
```

### 16.2 工序级管理（报工系统）

**改造 `MesWorkOrderProcess`**：

```
ProcessId
WorkOrderId
ProcessNo            工序号（顺序）
ProcessName          工序名（裁剪/缝制/质检/包装）
PlannedStartTime
PlannedEndTime
ActualStartTime
ActualEndTime
StandardCycleSec     标准节拍（秒/件）
ActualCycleSec       实际节拍
QualifiedQty         合格数
DefectQty            不良数
ReworkQty            返工数
OperatorId           操作工
EquipmentId          设备
DowntimeMinutes      停机时间
DowntimeReason       停机原因（缺料/故障/换模/培训）
Status               waiting/running/paused/completed
```

**报工方式**：

- [ ] PDA H5 扫码报工（开工/暂停/完工 + 数量录入）
- [ ] 工序流转看板（生产现场大屏，实时显示各工序在制品数量）
- [ ] 异常报工：操作工现场报"缺料/设备故障"自动创建处理单

### 16.3 设备 OEE（设备综合效率）

**新增表**：

```
mes_equipment           设备档案
  - EquipmentId, EquipmentCode, EquipmentName, Type
  - MaxCapacity（额定产能）, Status（idle/running/down/maintenance）

mes_equipment_log       设备运行日志（来自 IoT 上报或人工登记）
  - LogId, EquipmentId, EventType（start/stop/down/repair）
  - StartAt, EndAt, OperatorId, Reason
```

**OEE 计算**（每日跑批写入 `mes_equipment_oee_daily`）：

```
可用率 = 实际运行时间 / 计划运行时间
表现率 = 实际产量 / (运行时间 × 标准节拍)
质量率 = 合格品数 / 总产量
OEE   = 可用率 × 表现率 × 质量率
```

### 16.4 瓶颈与产能分析

```
GET /api/stats/capacity-load?date=     设备/工序负载率
GET /api/stats/bottleneck              瓶颈识别（最长队列工序）
GET /api/stats/operator-ranking        工人效率排名（产量/合格率）
```

**前端看板**：
- [ ] 工序节拍折线图（标准 vs 实际，标红超标点）
- [ ] OEE 仪表盘（可用率/表现率/质量率分项 + 总 OEE）
- [ ] 不良品 Pareto 图（按缺陷类型排序）

### 16.5 验收标准

- [ ] 工单进度自动统计准/晚/超期，BI 看板能看到趋势
- [ ] 至少 1 条产线接入设备日志，OEE 数值真实计算
- [ ] 工序级报工链路通（开工 → 暂停 → 完工 → 数量回写）
- [ ] 测试覆盖：`WorkOrderEfficiencyTests.cs` ≥ 8 个

---

## 十七、P13：OctopusFIN（财务）— 新建子系统

> 参考金蝶·总账/应收应付/费控。MVP 范围聚焦总账 + 应收应付。

### 17.1 模块边界

```
OctopusFIN
├── 总账（GL）：科目、凭证、日记账、试算平衡、月结
├── 应收（AR）：基于 CRM 合同/回款自动生成应收凭证
├── 应付（AP）：基于 MES 采购/收货自动生成应付凭证
├── 费控：员工报销（OA 已有审批 → FIN 生成凭证 + 出纳付款）
└── 资金：银行账户、收付款单、银企对账
```

### 17.2 数据模型（核心 12 张表）

```
fin_account              科目表（树形，含资产/负债/权益/收入/费用）
fin_voucher              凭证主表（凭证字号/制单/审核/记账）
fin_voucher_entry        凭证分录（借贷方明细）
fin_period               会计期间（年/月/状态：open/closed/locked）
fin_ar_invoice           应收发票（关联 crm_contract）
fin_ar_receipt           应收收款（关联 crm_payment）
fin_ap_invoice           应付发票（关联 mes_purchase_order）
fin_ap_payment           应付付款
fin_expense_claim        费用申请（关联 oa_approval）
fin_bank_account         银行账户
fin_bank_statement       银行对账单
fin_journal              日记账（自动生成的凭证+手工凭证统一视图）
```

### 17.3 跨系统集成点

| 来源 | 触发 | 自动生成 |
|---|---|---|
| CRM 合同发货 | WMS 发货回调 → CRM 通知 FIN | 应收发票 |
| CRM 回款确认 | OA 财务审批通过 | 凭证（借：银行 / 贷：应收）+ 关闭应收 |
| MES 采购到货 | WMS 入库回调 → MES 通知 FIN | 应付发票 |
| OA 报销审批通过 | OA 审批回调 | 费用凭证（借：管理费用 / 贷：应付员工）|
| OA 付款审批通过 | OA 审批回调 | 付款凭证 + 银行付款单 |

### 17.4 实施阶段

- 阶段 1：科目 + 凭证 + 月结基础（不接业务，纯手工）
- 阶段 2：AR/AP 自动凭证（接 CRM/MES）
- 阶段 3：费控（接 OA 报销）
- 阶段 4：报表（资产负债表 / 利润表 / 现金流量表）

---

## 十八、P14：OctopusHRM（人力资源）— 新建子系统

> 参考金蝶 HRM。复用 OA 已有职员管理 + 考勤数据。

### 18.1 模块边界

```
OctopusHRM
├── 员工档案（从 OA Employee 升级，含合同/社保/薪资历史）
├── 薪酬：薪资项配置 + 月度算薪 + 工资条 + 社保公积金
├── 绩效：KPI/OKR 设置 + 评分 + 360 评估
├── 招聘：JD 发布 + 投递 + 面试 + Offer 流程
├── 培训：课程 + 报名 + 完成证书
└── 假期：假期类型/余额/年假调度（与 OA 考勤打通）
```

### 18.2 核心数据表

```
hrm_employee_profile     员工档案（扩展 OA Employee）
hrm_contract             劳动合同
hrm_salary_structure     薪资结构（基本工资/绩效/补贴/扣款项）
hrm_payroll              工资单
hrm_payroll_item         工资明细项
hrm_social_insurance     社保公积金缴纳记录
hrm_kpi_goal             KPI 目标
hrm_kpi_review           KPI 评分
hrm_recruit_job          招聘职位
hrm_recruit_candidate    候选人
hrm_recruit_interview    面试记录
hrm_leave_balance        假期余额（年假/事假/病假）
```

### 18.3 跨系统集成

- 月度算薪：自动拉 OA 考勤（出勤/加班/请假天数）→ 计算工资
- 算薪完成 → 推 FIN 生成薪资凭证（借：应付职工薪酬 / 贷：银行）
- 入职流程：HRM 招聘 → Offer 接受 → 自动创建 OA Employee（HR 临时档案）→ H5 入职填写 → 转正生成 UMC 账号

---

## 十九、P15：OctopusOMS（订单中台）— 新建子系统（聚水潭风格）

> 多渠道电商订单聚合，是聚水潭最核心的功能。

### 19.1 模块边界

```
OctopusOMS（电商订单中台）
├── 渠道接入：淘宝/京东/拼多多/抖音/小红书/Amazon/独立站
├── 订单聚合：自动同步 + 订单合并 + 拆单 + 转单到 WMS
├── 商品映射：渠道 SKU ↔ PLM SKU
├── 库存同步：WMS 库存 → 各渠道（按规则分配）
├── 售后管理：退货/换货/补发/退款
└── 客服工单：客户咨询/投诉/订单异常
```

### 19.2 数据模型

```
oms_channel              渠道（淘宝/京东等）
oms_shop                 店铺（同一渠道下多店铺）
oms_order                订单（统一订单，跨渠道）
oms_order_item           订单明细
oms_sku_mapping          渠道 SKU ↔ PLM SKU 映射
oms_inventory_sync_rule  库存分配规则（如：A 店铺占 60%、B 店铺占 40%）
oms_after_sale           售后单（退/换/补/退款）
```

### 19.3 关键能力

- [ ] **订单合并**：同一买家同一收货地址多笔订单自动合并发货
- [ ] **订单拆分**：跨仓库 / 跨货主订单自动拆单
- [ ] **预售订单**：未到货商品先收单，到货后自动转 WMS
- [ ] **赠品规则**：满 X 元送 Y 商品，自动加入订单明细
- [ ] **黑名单/风控**：地址异常/退货高频客户标记拦截

### 19.4 渠道对接

| 渠道 | 接入方式 | 优先级 |
|---|---|---|
| 淘宝/天猫 | 顶级开放平台（top.taobao.com） | ★★★ |
| 京东 | JOS 开放平台 | ★★★ |
| 拼多多 | 开放平台 | ★★ |
| 抖音小店 | 抖店开放平台 | ★★ |
| 独立站（Shopify） | REST + Webhook | ★ |
| Amazon | SP-API | ★ |

---

## 二十、P16：客户/供应商门户

### 20.1 OctopusPortal.Customer（客户门户）

```
独立 H5 + PWA，外网可访问
├── 我的询盘：提交询盘 + 查询报价进度
├── 我的合同：查看合同状态 + 下载合同 PDF
├── 物流追踪：扫描发货单查看物流
├── 对账中心：查看应收/已付/未付
├── 在线客服：与销售一对一沟通
└── 文档下载：产品手册/技术文档
```

### 20.2 OctopusPortal.Supplier（供应商门户）

```
├── 询价响应：客户发布询价 → 供应商在线报价
├── 我的订单：查看采购订单 + 备货状态
├── 发货登记：上传物流单号
├── 对账中心：查看采购应付
└── 资质上传：营业执照/认证证书
```

### 20.3 技术方案

- 单独的 UMC OIDC 客户端（外部用户：customer / supplier 角色）
- 数据隔离：只能看到与自己相关的数据（数据权限范围 = 仅本人）
- API 网关：所有门户请求走 OctopusGateway（限流 + 鉴权 + 日志）

---

## 二十一、P17：低代码 BPM（工作流引擎升级）

> 当前 OA 审批流是固定模板 + JSON FormSchema，向"全员可配置"升级。

### 21.1 流程设计器

- [ ] 拖拽式流程节点（开始/审批/抄送/分支/并行/结束）
- [ ] 条件分支（基于表单字段，如：金额 > 10w 走总经理）
- [ ] 自由跳转（驳回到指定节点）
- [ ] 委托/转交/加签
- [ ] 表单设计器（拖拽字段：单行文本/数字/日期/下拉/明细表/审批意见）

### 21.2 流程引擎升级

```csharp
// 当前：节点顺序写死
// 升级：DSL 表达式（参考 Camunda BPMN 简化版）
public class WorkflowDefinition
{
    public string Bpmn { get; set; }  // BPMN 2.0 XML
    // OR
    public string Json { get; set; }  // 自定义 JSON DSL
}
```

可选：直接引入 [Elsa Workflows](https://elsa-workflows.github.io/elsa-core/)（.NET 工作流引擎）。

### 21.3 表单引擎

前端 `DynamicForm.vue` 升级：
- [ ] 字段联动（A 字段值变化 → B 字段隐藏/显示/必填切换）
- [ ] 公式字段（如：`总金额 = 数量 × 单价`）
- [ ] 明细表（一对多子表，如报销发票多条）
- [ ] 关联数据（下拉数据来自接口，如选择客户）

---

## 二十二、长期愿景（12 个月）

### 22.1 多租户 SaaS 化

将 OctopusERP 改造成多租户架构，每个租户数据隔离：
- 数据库层：所有表加 `TenantId`（或独立 schema/独立 db）
- API 层：JWT 中携带 TenantId，中间件自动注入查询过滤
- 计费：按用户数/存储量/调用次数
- 对外发布为开源版 + 商业版双轨

### 22.2 移动 App（原生壳 + WebView）

H5 完成后用 Capacitor 套壳成 iOS/Android App：
- 推送通知（极光/腾讯信鸽）
- 离线打卡
- 扫码（条码/二维码）
- 定位打卡（GPS 围栏）

### 22.3 AI 赋能

| 场景 | 模型 | 用途 |
|---|---|---|
| 智能审批 | LLM | 自动分类审批类型，给审批人摘要 |
| 销售助理 | RAG | 根据历史合同推荐报价 |
| 库存预测 | 时序模型 | 预测安全库存，自动建议采购 |
| 客户洞察 | LLM | 分析客户沟通记录，识别意向 |
| 智能客服 | RAG + LLM | 客户门户在线客服自动应答 |

### 22.4 国际化 + 本地化

- i18n（中/英/日/西班牙语）
- 多币种 + 汇率（FIN 模块）
- 多税制（增值税 / GST / VAT）
- 时区支持（每用户独立时区）
- 跨境电商（聚水潭海外仓 + 报关报检）

---

## 模块依赖与建设顺序图

```
现有：UMC ─ OA ─ PLM ─ CRM ─ WMS ─ MES（已完成）
        │     │
        │     └─ P5 H5 移动端
        │
        ├─ P10 PLM 升级 ────────┐
        ├─ P11 WMS 升级 ────────┤
        ├─ P12 MES 升级 ────────┤
        │                       ↓
        ├─ P13 FIN ──────────── 业务能力完整
        ├─ P14 HRM ─────────── ┐
        ├─ P15 OMS ─────────── ┤
        │                      ↓
        ├─ P16 客户/供应商门户  对外能力
        ├─ P17 低代码 BPM      ↓
        │                      产品化
        └─ 长期：多租户 / 移动 App / AI / 国际化
```

---

## 历史：OA 五大常用功能（2026-04-18 ~ 2026-04-19）

| Step | 功能 | 状态 | 测试数 | 交付日期 |
|------|------|------|--------|---------|
| 1 | 通讯录 | ✅ | 11 | 2026-04-18 |
| 2 | 公告通知 | ✅ | 12 | 2026-04-18 |
| 3 | 考勤打卡（多班次+日历） | ✅ | 16 | 2026-04-19 |
| 4 | 待办中心 | ✅ | 6 | 2026-04-19 |
| 5 | 会议室预订 | ✅ | 12 | 2026-04-19 |

**OA 持久化迁移（2026-04-19）**：InMemory → SQLite (EF Core 8)，90 个测试保留通过，前端零修改。

---

## 文档关系

| 文档 | 内容 |
|------|------|
| `ROADMAP.md`（本文件） | 全链路架构 + 各模块实施方案 + API 清单 |
| `SCHEDULE.md` | 排程节奏 + 主流系统经验 + 季度规划 + 风险对策 |
| `TODO.md` | 整体进度清单（跨所有模块）|
| `CLAUDE.md` | 系统规格（架构、数据模型、接口清单、开发约定）|
| `DECISIONS.md` | 关键架构决策记录 |
| `design/` | H5 移动端设计规格（tokens / oa-h5 / umc-h5）|
| `DECISIONS.md` | 关键架构决策记录 |
