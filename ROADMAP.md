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

## 八、P4：全链路 BI 看板（待开发）

> 依赖：CRM + WMS + MES 数据全部就绪后聚合

### 看板页面
1. **销售漏斗**：询盘→报价→合同各阶段数量+转化率
2. **时效仪表盘**：7 大指标当前值 vs SLA 目标，RAG 红绿黄
3. **合同全链路时间轴**：选定合同，展示从询盘到回款的完整时间节点
4. **审批积压热力图**：各审批人待办数 + 平均处理时长
5. **OTD 趋势**：按月展示发货准时率 + 采购准时率折线图

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
| `TODO.md` | 整体进度清单（跨所有模块）|
| `CLAUDE.md` | 系统规格（架构、数据模型、接口清单、开发约定）|
| `DECISIONS.md` | 关键架构决策记录 |
