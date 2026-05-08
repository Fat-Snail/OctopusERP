# OctopusERP — Claude Code 指南

## 项目定位

| 系统 | 定位 | 角色 |
|---|---|---|
| **OctopusUMC** | 统一用户中心（身份提供商 IdP） | 认证主体，管理全平台用户、权限、组织 |
| **OctopusOA** | 办公自动化系统 | 审批流引擎，贯穿全链路 |
| **OctopusPLM** | 商品生命周期管理 | 商品/SKU/类目/属性/审核流 |
| **OctopusCRM** | 客户关系管理 | 询盘→报价→合同→回款，客户档案管理 |
| **OctopusWMS** | 仓储管理系统 | 入库/出库/库存查询/盘点 |
| **OctopusMES** | 生产执行与采购系统 | 供应商/采购订单/工单管理 |

**核心约束**：
- 开发全程采用 **TDD（测试驱动开发）**，先写测试再写实现
- OA 用户退出时必须**同步注销 UMC 会话**（Back-Channel Logout）
- 生产环境标准交付：所有关键路径有集成测试覆盖

---

## 目录结构

```
/
├── OctopusUMC/
│   ├── src/
│   │   ├── OctopusUMC.Core/              # 领域层
│   │   ├── OctopusUMC.Infrastructure/    # EF Core、OpenIddict、仓储
│   │   └── OctopusUMC.Api/               # Controllers、Program.cs
│   ├── tests/
│   │   └── OctopusUMC.Api.Tests/         # API 集成测试（WebApplicationFactory）
│   └── web/OctopusUMC.Web/               # Vue 3 + Vite（UMC 管理后台）
│
├── OctopusOA/
│   ├── src/OctopusOA.Api/
│   │   ├── Persistence/                  # EF Core：Entities / OaDbContext / OaDbSeeder
│   │   ├── Services/                     # 业务服务（Scoped）
│   │   └── octopus_oa.db                 # 开发用 SQLite（启动时自动创建+种子）
│   ├── tests/OctopusOA.Api.Tests/
│   └── web/OctopusOA.Web/                # Vue 3 + Vite（OA 前端）
│
├── OctopusPLM/
│   ├── src/OctopusPLM.Api/
│   │   ├── Persistence/                  # EF Core：PlmDbContext / PlmDbSeeder
│   │   └── octopus_plm.db
│   ├── tests/OctopusPLM.Api.Tests/       # 22 个集成测试
│   └── web/OctopusPLM.Web/              # Vue 3 + Vite + shadcn-vue + Tailwind v4
│
├── OctopusCRM/
│   ├── src/OctopusCRM.Api/
│   │   ├── Persistence/                  # EF Core：CrmDbContext / CrmDbSeeder (10张表)
│   │   └── octopus_crm.db
│   ├── tests/OctopusCRM.Api.Tests/       # 31 个集成测试
│   └── web/OctopusCRM.Web/              # Vue 3 + Vite + shadcn-vue + Tailwind v4（三栏布局，与 OA/PLM 统一）
│
├── OctopusWMS/
│   ├── src/OctopusWMS.Api/
│   │   ├── Persistence/                  # EF Core：WmsDbContext / WmsDbSeeder (10张表)
│   │   └── octopus_wms.db
│   ├── tests/OctopusWMS.Api.Tests/       # 23 个集成测试
│   └── web/OctopusWMS.Web/              # Vue 3 + Vite + shadcn-vue + Tailwind v4
│
├── OctopusMES/
│   ├── src/OctopusMES.Api/
│   │   ├── Persistence/                  # EF Core：MesDbContext / MesDbSeeder (6张表)
│   │   └── octopus_mes.db
│   ├── tests/OctopusMES.Api.Tests/       # 20 个集成测试
│   └── web/OctopusMES.Web/              # Vue 3 + Vite + shadcn-vue + Tailwind v4
│
├── aspire/OctopusAspire.AppHost/         # .NET Aspire 一键启动编排（12 个服务）
├── CLAUDE.md
├── TODO.md
└── DECISIONS.md
```

---

## 技术栈

| 层次 | 技术 | 说明 |
|---|---|---|
| 后端运行时 | .NET 8 / .NET 10 | UMC/OA 用 .NET 8；PLM 用 .NET 10 |
| 认证框架 | OpenIddict | OIDC/OAuth2 服务端，MIT 协议 |
| ORM | EF Core 8 + SQLite | 开发用 SQLite 文件；生产切换 MySQL |
| 实时通信 | SignalR | 在线用户、公告推送 |
| API 文档 | Swagger | XML 注释 + GenerateDocumentationFile |
| 日志 | Serilog | 控制台 + 文件 + 结构化 |
| 后端测试 | xUnit + WebApplicationFactory | SQLite 内存模式集成测试 |
| UMC/OA 前端 | Vue 3 + Vite + pure-admin-thin | Element Plus + UnoCSS |
| PLM 前端 | Vue 3 + Vite + shadcn-vue | Tailwind CSS v4 + TanStack Table + lucide-vue-next |
| OIDC 客户端 | oidc-client-ts | OA.Web / PLM.Web 使用 |
| 包管理器 | npm | |

---

## 默认端口

| 服务 | 地址 | 备注 |
|---|---|---|
| OctopusUMC.Api | http://localhost:5001 | Aspire 固定端口 |
| OctopusUMC.Web | http://localhost:5173 | Vite dev server |
| OctopusOA.Api | http://localhost:5002 | Aspire 固定端口 |
| OctopusOA.Web | http://localhost:5174 | Vite dev server |
| OctopusPLM.Api | http://localhost:5003 | |
| OctopusPLM.Web | http://localhost:5175 | |
| OctopusCRM.Api | http://localhost:5004 | JWT Bearer 向 UMC 验证 |
| OctopusCRM.Web | http://localhost:5176 | OIDC client_id: octopus-crm-web |
| OctopusWMS.Api | http://localhost:5005 | JWT Bearer 向 UMC 验证 |
| OctopusWMS.Web | http://localhost:5177 | OIDC client_id: octopus-wms-web |
| OctopusMES.Api | http://localhost:5006 | JWT Bearer 向 UMC 验证 |
| OctopusMES.Web | http://localhost:5178 | OIDC client_id: octopus-mes-web |
| Aspire Dashboard | http://localhost:15256 | 服务监控 + 日志聚合 |

> 通过 Aspire 启动时用 HTTP，由 `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` 允许。

---

## 一键启动（.NET Aspire）

```bash
cd aspire/OctopusAspire.AppHost
dotnet run
# Dashboard: http://localhost:15256
```

**关键配置要点（AppHost.csproj）**：
- `TargetFramework`：必须 `net8.0`（net10.0 有 DCP TLS 兼容问题）
- ProjectReference 上加 `<IsAspireProjectResource>true</IsAspireProjectResource>`
- `AddProject<T>` 后用 `.WithEndpoint("http", e => e.Port = N)` 覆盖端口（不能再调 `WithHttpEndpoint`）
- `AddViteApp(name, path)`（Aspire 13.x，不是 `AddNpmApp`）

**launchSettings.json 必须设置**：
- `HTTPS_PROXY=""` / `HTTP_PROXY=""`：清空代理，防 DCP TLS EOF
- `PATH` 含 `/usr/local/bin`：确保 node/npm 可找到

**常见 Aspire 问题**：

| 问题 | 解决 |
|---|---|
| `Projects.OctopusUMC_Api` 找不到 | ProjectReference 缺少 `IsAspireProjectResource` |
| `http endpoint already exists` | 用 `.WithEndpoint("http", e => e.Port = N)` 而非 `WithHttpEndpoint` |
| DCP TLS EOF | launchSettings.json 设 `HTTPS_PROXY=""` |
| 端口被占用 | `kill $(lsof -ti:20228 -ti:15256)` |

---

## 架构原则

### 整洁架构（后端）

```
依赖方向（严格单向）：Api → Infrastructure → Core
```

- **Core**：零外部依赖，含实体、接口、用例、领域事件
- **Infrastructure**：实现 Core 接口，含 DbContext、仓储、OpenIddict
- **Api**：HTTP 编排层，Controller ≤ 10 行，只调用 UseCase

### TypeScript 严格要求

- `strict: true`，禁 `any`，禁 `@ts-ignore`
- 前端类型必须与后端 C# DTO 一一镜像（camelCase 字段名、类型、可选性）
- 类型定义集中放 `src/api/{模块}/types.ts`
- 全局通用类型（`ApiResponse<T>`、`PagedResult<T>`）放 `src/api/types.ts`
- 详细规则见 `.claude/rules/code-style.md`

### TDD 流程

```
1. 写失败的测试（Red）
2. 写最小实现让测试通过（Green）
3. 重构代码，测试保持通过（Refactor）
```

---

## OctopusUMC 功能清单

### 阶段一：核心框架（已完成）

| 模块 | 功能描述 |
|---|---|
| **OIDC 身份提供商** | 授权码流 + PKCE、客户端凭据流、刷新令牌流 |
| **用户认证** | 注册、登录（Cookie 会话）、登出 |
| **SSO** | 为 OA 等外部系统提供 SSO，支持 Back-Channel Logout |

### 阶段二：权限体系（已完成，79 个测试）

| 模块 | 功能描述 |
|---|---|
| **RBAC** | 菜单/按钮权限 + 五种数据权限范围 |
| **用户管理** | CRUD + 绑定角色/部门/职位 + 启用禁用/重置密码 |
| **机构/职位/菜单/角色/字典** | 完整 CRUD |
| **多公司兼职** | UserDept.IsPrimary 区分主/兼职部门，支持跨公司查询 |

### 种子用户（admin/Admin@123）

| UserId | UserName | 公司 | 状态 |
|---|---|---|---|
| 1 | admin | 章鱼科技 | 启用 |
| 2 | zhangsan | 章鱼科技(主)/海星科技(兼) | 启用 |
| 3 | lisi | 章鱼科技 | 启用 |
| 4 | wangwu | 章鱼科技 | **禁用** |
| 5 | editor | 章鱼科技 | 启用 |
| 6 | zhaoliu | 海星科技 | 启用 |

> 种子变更：删除 `src/OctopusUMC.Api/octopus_umc.db` 后重启

### 阶段三：扩展功能（待开发）

访问日志、操作日志、在线用户（SignalR）、公告管理、文件管理、任务调度（Sundial）、服务监控、系统配置、导入导出（Magicodes.IE）、限流（AspNetCoreRateLimit）

---

## OctopusOA 功能清单

> 完整 API 清单、审批流设计、职员管理、各功能详细数据表见 `.claude/rules/oa-features.md`

### 数据持久化（SQLite via EF Core，已完成）

```
OctopusOA.Api/Persistence/
├── Entities.cs       20 个实体类
├── OaDbContext.cs    DbSet + Fluent 配置 + List<string>→JSON ValueConverter
└── OaDbSeeder.cs     幂等 Seed（仅在 SyncUsers 为空时插入）
```

**数据库**：开发用 `octopus_oa.db`（启动时 EnsureCreated + Seed）；测试用 `Data Source=:memory:;Cache=Shared`

**关键表**（20 个，`oa_*` 前缀）：
```
用户/部门：  oa_sync_user, oa_dept, oa_user_dept
审批：       oa_workflow_template, oa_workflow_node, oa_approval, oa_approval_record
职员：       oa_employee, oa_employee_education/_work/_family/_emergency
公告：       oa_notice, oa_notice_read
考勤：       oa_attendance_rule, oa_user_shift, oa_attendance, oa_attendance_fix
会议室：     oa_meeting_room, oa_meeting_booking
```

**关键设计**：
- 所有 Service 是 **Scoped**，跟随 OaDbContext
- `oa_attendance` 对 `(UmcUserId, Date)` 建唯一索引
- JSON 列：`oa_sync_user.OaRoles` 用 `ValueConverter<List<string>, string>`
- 补卡联动：`ApprovalService.Approve()` 在 `template.TemplateCode == "attendance_fix"` 时，通过 `IServiceProvider.GetRequiredService<AttendanceService>()` 调用 `HandleApprovalFix(approval)`

### OA 角色

| OA 角色 | oaRole | 权限范围 |
|---------|--------|----------|
| OA 管理员 | oa_admin | 全部菜单 + 流程模板管理 |
| 普通员工 | oa_user | 首页 + 我的工作 + 个人中心 |
| 部门主管 | oa_manager | oa_user + 本部门审批 |

### OA 功能模块（已完成）

| 模块 | 状态 |
|---|---|
| SSO 登录 / Back-Channel Logout | ✅ |
| 用户同步（HMAC-SHA256 Webhook） | ✅ |
| 可定制审批流（leave / expense / attendance_fix） | ✅ |
| 职员管理（入职 H5 流程） | ✅ |
| 通讯录（部门树 + 用户卡片） | ✅ |
| 公告通知（含已读状态） | ✅ |
| 考勤打卡（多班次 + 日历） | ✅ |
| 会议室预订（冲突检测） | ✅ |
| 待办中心（聚合首页） | ✅ |

### OA 全局技术约定

**统一 HTTP 工厂** `src/utils/http.ts`：
- `createAuthedHttp(baseURL)` — 带 JWT + 自动时间格式化
- `createPublicHttp(baseURL)` — 无 JWT（H5 用）
- 响应拦截器：字段名以 `Time`/`At`/`Date` 结尾的 ISO UTC 字符串自动转北京时间

**弹出层组件（Dialog / Sheet / Select）**：
- OA.Web 中为**自定义实现**，不使用 shadcn-vue 安装版本（reka-ui 模块级单例状态会在约 15 次 SPA 导航后冻结 UI）
- 实现：`provide/inject` + `<Teleport to="body">` + `v-if`
- 组件位置：`src/components/ui/dialog/`、`sheet/`、`select/`

**测试用户隔离**：`TestAuthHandler` 支持 `X-Test-UserId` header 切换测试用户

---

## OctopusPLM 功能清单

### 后端（已完成，22 个测试）

- **商品状态机**：`draft → pending_review → approved → active → discontinued`（+ `rejected`）
- **类目树**：多级树形，含属性模板（SPU属性 / SKU维度）
- **属性库**：全局属性，含枚举值管理
- **SKU 矩阵**：按维度属性自动生成 SKU 组合
- **审核流**：提交/审核通过/驳回/上架/下架 + 审核历史记录
- **统计接口**：`/api/product/stats` 按状态统计商品数

### 前端（已完成）

- **UI 栈**：shadcn-vue + Tailwind CSS v4 + TanStack Table + lucide-vue-next
- **商品列表**：状态栏筛选 + 状态相关操作按钮（审核/上架/下架）
- **商品新增/编辑**：原生 `<select>` 类目选择（扁平化缩进）+ 动态属性 + SKU 矩阵 + 审核历史时间线
- **类目管理**：左树 CRUD + 右侧属性库 CRUD

---

## OctopusCRM 功能清单

### 后端（已完成，31 个集成测试）

- **客户管理**：客户 CRUD + 联系人管理 + 关键词搜索
- **询盘管理**：询盘创建/分配/关闭，关联客户
- **报价管理**：报价单 CRUD + 明细 + 提交审批（HMAC 回调） + 确认流程
- **合同管理**：从确认报价创建合同 + 提交审批 + 激活/发货/完成/终止状态机
- **回款管理**：添加回款 + 确认 + 提交审批（金额≥ 10 万需审批）
- **统计接口**：`/api/stats/summary` 汇总客户数/询盘数/合同数/合同金额

**数据库**：开发用 `octopus_crm.db`（启动时 EnsureCreated + Seed）；测试用 `Data Source=:memory:;Cache=Shared`

**HMAC 签名验证**：`X-Crm-Signature: sha256=<HMAC-SHA256(rawBody, crm-shared-secret-dev)>`，Controller 读原始请求体（`EnableBuffering()`）避免序列化差异

### 前端（已完成）

**UI 架构**（与 OA/PLM 统一三栏布局）：

```
src/components/app/
├── AppShell.vue    # 三栏容器（ModuleRail + SubMenu + TopBar + slot）
├── ModuleRail.vue  # 56px 模块导航栏（UMC/OA/PLM/CRM 互跳）
├── SubMenu.vue     # 200px 二级菜单（工作台/客户/询盘/报价/合同）
├── TopBar.vue      # 顶栏（面包屑 + 搜索 + 通知铃 + 用户头像/登出）
└── PageHeader.vue  # 页面标题组件

src/components/ui/
└── avatar.vue      # 头像组件

src/lib/
└── utils.ts        # cn() 工具函数
```

- **工作台**：4 张统计卡（客户数/询盘数/合同数/合同金额）+ 快捷入口；PageHeader 组件 + `p-5` 内容区
- **客户管理**：TanStack Table + 搜索 + 新建对话框；PageHeader 含操作按钮插槽
- **询盘/报价/合同**：列表页，状态 Badge（CSS 变量色，与 OA/PLM 统一）
- **ModuleRail 互通**：OA/PLM 侧边栏已启用 CRM 跳转入口（`:5176`）

**Badge 颜色规范**（与 OA/PLM 一致，禁止写死 Tailwind 颜色）：
- 活跃/成功/通过：`bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]`
- 警告/进行中：`bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]`
- 信息/待处理：`bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]`
- 危险/拒绝：`bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]`
- 主色（A 级/已发货）：`bg-primary-soft text-[var(--primary-soft-fg)]`
- 中性/停用：`bg-muted text-muted-foreground`

**跨系统种子数据关联**（`CrmDbSeeder.cs`）：

| CRM 数据 | 关联系统 | 关联字段 | 示例 |
|---|---|---|---|
| 询盘/报价/合同 明细 | PLM | `PlmProductId` | iPhone 15 Pro (PLM ProductId=1), 小米 14 (PLM ProductId=2) |
| 报价/合同 | OA | `OaApprovalId` | OA 审批 Id=1,2 |
| 询盘负责人/创建人 | UMC | `AssignedTo`/`CreatedBy` | zhangsan (UmcUserId=2), lisi (UmcUserId=3) |

种子客户（5 个）：华为(A级) / 阿里巴巴(B级) / 字节跳动(C级) / 苏宁易购(A级，链接 PLM) / 京东(B级，链接 PLM)

---

## OctopusWMS 功能清单

### 后端（已完成，23 个集成测试）

- **仓库管理**：仓库 CRUD + 自动生成 WH-XXXXXX 编码 + 启/停用
- **库存管理**：库存查询（按仓库/关键词分页）+ 摘要统计 + 低库存预警 + 手动调整
- **入库管理**：入库单 CRUD（IN-yyyyMMdd-XXXX 编码）+ 收货确认（自动更新库存）
- **出库管理**：出库单 CRUD（OUT-yyyyMMdd-XXXX 编码）+ 发货确认（自动扣减库存）
- **盘点管理**：盘点任务（按现有库存生成条目）+ 提交盘点结果
- **统计接口**：`/api/stats/summary`（仓库数/今日入库/待出库/低库存预警/总库存条目）

**数据库**：开发用 `octopus_wms.db`；测试用 `Data Source=:memory:;Cache=Shared`

**关键表**（10 个，`wms_` 前缀）：
```
用户：         wms_sync_user
仓库：         wms_warehouse, wms_location
库存：         wms_inventory（唯一索引 WarehouseId+PlmProductId）
入库：         wms_inbound_order, wms_inbound_item
出库：         wms_outbound_order, wms_outbound_item
盘点：         wms_stocktake, wms_stocktake_item
```

**跨系统种子数据关联**（`WmsDbSeeder.cs`）：

| WMS 数据 | 关联系统 | 示例 |
|---|---|---|
| 出库单 | CRM | `CrmContractId=1,2` |
| 入库单 | MES | `MesPurchaseOrderId=1,2` |
| 库存/入出库明细 | PLM | `PlmProductId=1,2` |

---

## OctopusMES 功能清单

### 后端（已完成，20 个集成测试）

- **供应商管理**：供应商 CRUD + 自动生成 SUP-XXXXXX 编码 + A/B/C 等级
- **采购订单**：采购单 CRUD（PO-yyyyMMdd-XXXX 编码）+ 提交审批 + 批准/驳回 + 金额自动汇总
- **工单管理**：工单 CRUD（WO-yyyyMMdd-XXXX 编码）+ 开工/完工 + 工序进度更新
- **统计接口**：`/api/stats/summary`（供应商数/进行中采购/进行中工单/已完成工单/采购总金额）

**数据库**：开发用 `octopus_mes.db`；测试用 `Data Source=:memory:;Cache=Shared`

**关键表**（6 个，`mes_` 前缀）：
```
用户：         mes_sync_user
采购：         mes_supplier, mes_purchase_order, mes_purchase_item
生产：         mes_work_order, mes_work_order_process
```

**跨系统种子数据关联**（`MesDbSeeder.cs`）：

| MES 数据 | 关联系统 | 示例 |
|---|---|---|
| 采购订单 | OA | `OaApprovalId=3` |
| 采购订单 | WMS | `WmsInboundOrderId=1,2` |
| 工单/采购明细 | PLM | `PlmProductId=1,2` |

---

## 开发流程（六步闸门制）

> 完整 Step 细节、通过标准、验证命令见 `.claude/rules/dev-workflow.md`

```
Step 1 前端样板  →  Step 2 后端骨架  →  Step 3 接口确认
                                              ↓
Step 6 功能验收  ←  Step 5 SSO打通  ←  Step 4 前后端联调
```

**当前状态**：UMC/OA 已完成（79+6 个测试），PLM 独立完成（22 个测试），CRM 独立完成（31 个测试），WMS 独立完成（23 个测试），MES 独立完成（20 个测试）。

---

## TDD 规范

```csharp
// 命名：被测方法_场景_预期结果
[Fact]
public async Task RegisterUser_WithDuplicateEmail_Returns409Conflict() { }

[Fact]
public async Task Login_WithCorrectCredentials_SetsCookieAndReturns200() { }
```

**禁止 Mock 数据库**：集成测试连真实 SQLite（`Data Source=:memory:;Cache=Shared`）。

**关键路径必测**：

| 场景 | 类型 |
|---|---|
| 注册 → 登录 → 获取用户信息 | API 集成测试 |
| OIDC 授权码流完整握手 | API 集成测试 |
| Back-Channel Logout 同步注销 | API 集成测试 |
| 无权限用户访问受保护接口 | API 集成测试 |

---

## 编码规范

1. **Controller 轻量**：方法体 ≤ 10 行，只调用 UseCase
2. **统一响应**：`{ "code": 200, "msg": "操作成功", "data": {...} }`
3. **密码安全**：`BCrypt.HashPassword`，work factor ≥ 12
4. **Cookie 安全**：`HttpOnly = true`，`Secure = true`（生产必须）
5. **PKCE 强制**：所有浏览器端 Public Client 必须使用 PKCE
6. **CORS 白名单**：禁止 `AllowAnyOrigin` + `AllowCredentials` 组合
7. **迁移手动执行**：生产环境禁止启动时自动迁移
8. **前端 HTTP 工具函数** `unwrap<T>()`：
   ```typescript
   function unwrap<T>(res: AxiosResponse<ApiResponse<T>>): T {
     if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
     return res.data.data
   }
   // 401 拦截器：跳过 /account/me 探测请求，防死循环
   const isSessionProbe = url.includes('/account/me')
   if (!alreadyOnLogin && !isSessionProbe) window.location.href = '/login'
   ```

---

## 常见坑点速查

| 问题 | 原因 | 解决 |
|---|---|---|
| CORS 预检 401 | `UseCors` 在 `UseAuthentication` 之后 | 调整中间件顺序 |
| OIDC discovery 404 | UMC.Api 不可从浏览器访问 | 确认 UMC 地址浏览器可达 |
| Token audience 不匹配 | OA `Audience` ≠ UMC `client_id` | 两端必须完全一致 |
| redirect_uri_mismatch | URL 末尾斜杠/大小写不一致 | 精确匹配，包括协议和端口 |
| Back-Channel Logout 未触发 | OpenIddict 未注册 logout endpoint | 确认权限中包含 `Endpoints.Logout` |
| EF 迁移找不到 DbContext | 缺少 `IDesignTimeDbContextFactory` | 在 Infrastructure 中实现工厂类 |
| 登录弹窗成功但 `/me` 仍 401 | 后端返回 HTTP 200 + `{"code":401}` 业务码 | `http.ts` 中用 `unwrap()` 检查 `code !== 200` |
| 登录页 `/account/me` 死循环 | 401 拦截器无条件跳 `/login` | 跳转前判断 `url.includes('/account/me')` 则跳过 |
| SQLite 种子数据未更新 | 旧 `.db` 文件预先存在不会重建 | 删除 `octopus_*.db` 后重启 API |
| EF Core LINQ 常量捕获错误 | 在 `.Select()` 内使用 Dictionary | 先 `.ToListAsync()` 再内存 `.Select()` |
| C# Guid 格式切片错误 | `$"SKU-{Guid.NewGuid():N[..6]}"` 语法错误 | 改为 `Guid.NewGuid().ToString("N")[..6]` |
| SPA 导航后 UI 冻结 | reka-ui Dialog/Sheet/Select 模块级单例 | 使用 OA.Web 自定义组件（`src/components/ui/`） |
| Aspire DCP TLS EOF | 机器有 `HTTPS_PROXY` | launchSettings.json 设 `HTTPS_PROXY=""` |
| CRM API 端口占用（5004） | `appsettings.json` 的 `"Urls"` 字段与 DCP 代理冲突 | 删除 `appsettings.json` 中的 `"Urls"` 字段，端口由 Aspire 控制 |
| CRM 登录无反应（CORS） | UMC CORS 白名单缺少 `:5176` | `Program.cs` 的 `WithOrigins` 加入 `http://localhost:5176` |
| CRM/PLM API 一律 401 | OpenIddict access token 不含 audience，`ValidateAudience = true` 导致拒绝 | `TokenValidationParameters` 中设 `ValidateAudience = false`（PLM 同理） |
| 前端列表接口 404 | `createAuthedHttp('/api/xxx')` 后追加 `/list`，后端根路由无此路径 | 列表用 `http.get('')`，无需 `/list` 后缀 |

---

## 与 Claude 协作方式

提问时注明系统 + 模块 + 具体任务：

```
"OctopusPLM：给商品列表页加批量删除功能（TDD）"
"OctopusOA：补卡审批联动考勤更新逻辑"
"OctopusUMC，Phase 5：集成 SignalR 在线用户模块"
```

**补充参考文档**：
- `.claude/rules/oa-features.md` — OA 功能详细规格 + 完整 API 清单
- `.claude/rules/dev-workflow.md` — 六步闸门制完整细节 + Gate 通过标准
- `.claude/rules/implementation-plan.md` — 分阶段实施计划（Phase 0-5）
- `.claude/rules/ui-spec.md` — shadcn-vue 迁移规范（PLM/OA 前端）
- `.claude/rules/code-style.md` — TypeScript + C# 编码风格规范
- `.claude/rules/testing.md` — 测试规范
- `.claude/rules/api-conventions.md` — API 约定 + OpenIddict 配置
