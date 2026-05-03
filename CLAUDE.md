# OctopusUMC & OctopusOA — Claude Code Vibe Coding 指南

## 项目定位

| 系统 | 定位 | 角色 |
|---|---|---|
| **OctopusUMC** | 统一用户中心（身份提供商 IdP） | 认证主体，管理全平台用户、权限、组织 |
| **OctopusOA** | 办公自动化系统 | OctopusUMC 的第一个 SSO 接入方 |

**核心约束**：
- 开发全程采用 **TDD（测试驱动开发）**，先写测试再写实现
- OA 用户退出时必须**同步注销 UMC 会话**（Back-Channel Logout）
- 生产环境标准交付：所有关键路径有集成测试覆盖

---

## 目录结构

```
/
├── OctopusUMC/
│   ├── OctopusUMC.sln
│   ├── src/
│   │   ├── OctopusUMC.Core/              # 领域层：实体、接口、用例、领域事件
│   │   ├── OctopusUMC.Infrastructure/    # 基础设施：EF Core、OpenIddict、仓储、外部服务
│   │   └── OctopusUMC.Api/               # 表现层：Controllers、Middlewares、Program.cs
│   ├── tests/
│   │   ├── OctopusUMC.Core.Tests/        # 领域逻辑单元测试
│   │   ├── OctopusUMC.Infrastructure.Tests/ # 仓储集成测试（真实 MySQL）
│   │   └── OctopusUMC.Api.Tests/         # API 集成测试（WebApplicationFactory）
│   └── web/
│       └── OctopusUMC.Web/               # Vue 3 + Vite（UMC 管理后台）
│
├── OctopusOA/
│   ├── src/
│   │   └── OctopusOA.Api/                # OA 后端 API（含 Persistence/ 目录 + SQLite）
│   │       ├── Persistence/              # EF Core：Entities.cs / OaDbContext.cs / OaDbSeeder.cs
│   │       ├── Services/                 # 业务服务（Scoped）
│   │       ├── Controllers/
│   │       ├── DTOs/
│   │       └── octopus_oa.db             # 开发用 SQLite 文件（启动时自动创建+种子）
│   ├── tests/
│   │   └── OctopusOA.Api.Tests/
│   └── web/
│       └── OctopusOA.Web/                # Vue 3 + Vite（OA 前端）
│
├── aspire/
│   └── OctopusAspire.AppHost/        # .NET Aspire 一键启动编排
│       ├── OctopusAspire.AppHost.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       └── Properties/
│           └── launchSettings.json
│
├── docker-compose.yml
├── CLAUDE.md
├── TODO.md
└── DECISIONS.md
```

---

## 技术栈

| 层次 | 技术 | 说明 |
|---|---|---|
| 后端运行时 | .NET 8 | 面向 .NET 10 演进 |
| 认证框架 | OpenIddict | OIDC/OAuth2 服务端，MIT 协议 |
| ORM | EF Core 8 + Pomelo | MySQL 8.0 驱动 |
| 实时通信 | SignalR | 在线用户、公告推送 |
| 任务调度 | Sundial | 分布式作业调度 |
| 文件存储 | 本地 / 阿里云 OSS / 腾讯 COS | 策略模式可扩展 |
| 导入导出 | Magicodes.IE | Excel/PDF 导入导出 |
| 限流 | AspNetCoreRateLimit | 接口访问频率控制 |
| API 文档 | Swagger + Knife4jUI | 双皮肤支持 |
| 日志 | Serilog | 控制台 + 文件 + 结构化 |
| 后端测试 | xUnit + WebApplicationFactory | 集成测试连真实 MySQL |
| 前端框架 | Vue 3 + Vite + TypeScript | Composition API |
| UI 模板 | vue-pure-admin（精简版 pure-admin-thin） | 两套系统统一基座 |
| UI 组件库 | Element Plus（pure-admin 内置） | |
| CSS 方案 | UnoCSS（pure-admin 内置） | 原子化 CSS |
| 状态管理 | Pinia（pure-admin 内置） | |
| OIDC 客户端 | oidc-client-ts | OA.Web 使用 |
| HTTP 客户端 | Axios（pure-admin 内置封装） | |
| 前端测试 | Vitest + @vue/test-utils | |
| 包管理器 | npm | |
| 数据库 | MySQL 8.0（生产）/ SQLite（开发） | UMC 和 OA 当前都用 SQLite 本地持久化；生产迁 MySQL 通过切换连接串 |
| 容器化 | Docker + docker compose | |

---

## 默认端口

| 服务 | 地址 | 备注 |
|---|---|---|
| OctopusUMC.Api | http://localhost:5001 | Aspire 编排时固定端口（原 7001） |
| OctopusUMC.Web | http://localhost:5173 | Vite dev server |
| OctopusOA.Api | http://localhost:5002 | Aspire 编排时固定端口（原 7002） |
| OctopusOA.Web | http://localhost:5174 | Vite dev server |
| Aspire Dashboard | http://localhost:15256 | 服务监控 + 日志聚合面板 |
| MySQL | 3306 | |

> **说明**：通过 Aspire 一键启动时使用 HTTP（非 HTTPS），由 `launchSettings.json` 中的 `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` 允许。独立启动时仍可使用 HTTPS（7001/7002）。

---

## 一键启动（.NET Aspire）

### 启动命令

```bash
cd aspire/OctopusAspire.AppHost
dotnet run
# Aspire Dashboard 自动在浏览器打开：http://localhost:15256
```

启动顺序：umc-api → umc-web（等待 umc-api 就绪）；oa-api → oa-web（等待 oa-api 就绪）。

### 项目配置要点

**OctopusAspire.AppHost.csproj**（关键配置）：
- `TargetFramework`：必须用 `net8.0`（不能用 net10.0，否则 DCP TLS 客户端兼容性问题）
- `Aspire.Hosting.AppHost` + `Aspire.Hosting.JavaScript`：核心包，不需要 workload
- `Aspire.Hosting.Orchestration.osx-arm64` + `Aspire.Dashboard.Sdk.osx-arm64`：macOS Apple Silicon 运行时（DCP + Dashboard 二进制）
- `<IsAspireProjectResource>true</IsAspireProjectResource>`：ProjectReference 上必须加此 MSBuild 元数据，触发源码生成器生成 `Projects.OctopusUMC_Api` / `Projects.OctopusOA_Api` 类型

**Program.cs**（端点注册方式）：
```csharp
// AddProject<T> 会自动从 launchSettings.json 注册 http 端点
// 不能再调用 WithHttpEndpoint(name: "http")，否则报 "endpoint already exists"
// 必须用 WithEndpoint("http", e => e.Port = N) 来覆盖端口
var umcApi = builder.AddProject<Projects.OctopusUMC_Api>("umc-api")
    .WithEndpoint("http", e => e.Port = 5001);

// AddViteApp（Aspire 13.x），不是 AddNpmApp
builder.AddViteApp("umc-web", "../../OctopusUMC/web/OctopusUMC.Web")
    .WithEndpoint("http", e => e.Port = 5173)
    .WithEnvironment("PORT", "5173")   // PORT 环境变量，Vite 读取此变量确定监听端口
    .WaitFor(umcApi);
```

**Properties/launchSettings.json**（必须存在）：
- `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`：允许 HTTP 传输
- `DOTNET_RESOURCE_SERVICE_ENDPOINT_URL`：DCP 资源服务地址
- `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL`：Dashboard OTLP 收集地址
- `HTTPS_PROXY=""`、`HTTP_PROXY=""`：**清空代理**（见下方坑点）
- `NO_PROXY` 含 `[::1]`（带方括号）
- `PATH` 含 `/usr/local/bin`：确保 `node`/`npm` 可被找到

### macOS 常见坑点

| 问题 | 原因 | 解决 |
|---|---|---|
| `Projects.OctopusUMC_Api` 类型找不到（CS0234） | ProjectReference 缺少 `IsAspireProjectResource` | 在 .csproj 的 ProjectReference 上加 `<IsAspireProjectResource>true</IsAspireProjectResource>` |
| `AddNpmApp` 找不到（CS1061） | Aspire 13.x 改名 | 改为 `AddViteApp(name, path)` |
| `Endpoint with name 'http' already exists` | `AddProject<T>` 已自动注册 http 端点 | 改为 `.WithEndpoint("http", e => e.Port = N)` |
| DCP TLS EOF（`Received an unexpected EOF`） | 机器设了 `HTTPS_PROXY`，.NET HttpClient 把 `https://[::1]:PORT` 路由到代理 | 在 launchSettings.json 中设 `HTTPS_PROXY=""`、`HTTP_PROXY=""` |
| `node`/`npm` 找不到 | Aspire 启动子进程时 PATH 不含 `/usr/local/bin` | launchSettings.json 中设 `PATH` 含 `/usr/local/bin` |
| 端口已被占用（20228/15256） | 上次 Aspire/DCP 进程残留 | `kill $(lsof -ti:20228 -ti:15256)` 清理后重启 |
| Dashboard 登录需要 token | Aspire 开发模式默认启用 | 从启动日志中找 `Login to the dashboard at ... with token ...` |

---

## 架构原则

### 整洁架构（后端）

```
依赖方向（严格单向）：
Api  →  Infrastructure  →  Core
```

- **Core**：零外部依赖。含实体、值对象、领域接口（`IUserRepository` 等）、用例（UseCase）、领域事件、领域异常
- **Infrastructure**：实现 Core 接口。含 DbContext、仓储、OpenIddict、OSS、邮件、SignalR Hub 等
- **Api**：HTTP 编排层。Controller ≤ 10 行，只调用 UseCase，不写业务逻辑

### TypeScript 严格要求

- `tsconfig.json` 开启 `"strict": true`，禁止 `any`，禁止 `@ts-ignore`
- **前端类型必须与后端 C# DTO 一一镜像**：字段名（camelCase）、类型、可选性完全对应
- 所有类型定义集中放在 `src/api/{模块}/types.ts`，不在组件内内联定义接口
- 全局通用类型（`ApiResponse<T>`、`PagedResult<T>`、`PageQuery`）放 `src/api/types.ts`
- 详细规则见 `.claude/rules/code-style.md`

### 前端分层（vue-pure-admin 约定）

```
views（页面）→ stores（状态）→ api（请求层）→ 后端
```

pure-admin-thin 的目录约定：

| 目录 | 职责 |
|---|---|
| `src/views/` | 页面级组件，对应路由，只做布局和协调 |
| `src/api/` | 封装所有后端请求（pure-admin 用 `src/api/` 而非 `services/`） |
| `src/store/` | Pinia stores，模块化拆分 |
| `src/router/` | 静态路由 + 动态路由（权限路由由后端返回） |
| `src/components/` | 可复用业务组件 |
| `src/utils/` | 工具函数 |
| `src/directives/` | 自定义指令（如 `v-auth` 按钮权限） |

**pure-admin 内置能力（直接复用，不要重复造轮子）**：
- `usePermission()`：按钮权限判断 hook
- `useNav()`：导航操作
- `http`：封装好的 Axios 实例（带 Token 拦截、错误处理）
- 动态路由：后端返回路由表，前端自动注册
- 多主题 / 暗色模式：开箱即用

### TDD 流程

```
1. 写失败的测试（Red）
2. 写最小实现让测试通过（Green）
3. 重构代码，测试保持通过（Refactor）
```

每个功能模块必须在实现前先建立测试文件。

---

## OctopusUMC 功能清单

### 阶段一：核心框架（Phase 1-2）

| 模块 | 功能描述 |
|---|---|
| **OIDC 身份提供商** | 授权码流 + PKCE、客户端凭据流、刷新令牌流，作为全平台 IdP |
| **用户认证** | 注册、登录（Cookie 会话）、登出、找回密码 |
| **SSO** | 为 OA 等外部系统提供 SSO 登录，支持 Back-Channel Logout 同步注销 |

### 阶段二：权限体系（Phase 3，优先实现）

> **核心原则**：先搭权限骨架，所有后续功能基于此权限体系构建。

| 模块 | 功能描述 |
|---|---|
| **菜单权限** | 配置系统菜单树（目录/菜单/按钮三级），绑定操作权限标识 |
| **角色管理** | 角色绑定菜单权限，控制用户可访问的功能范围 |
| **用户权限** | 用户绑定角色，支持多角色叠加，权限取并集 |
| **部门权限** | 按机构/部门划定数据可见范围 |
| **数据权限** | 五种数据范围：全部数据、本部门及子部门、本部门、仅本人、自定义 |

### 阶段三：用户管理后台（Phase 4）

> **目标**：实现市面上流行 RBAC 管理后台的完整功能集。

| 模块 | 功能描述 |
|---|---|
| **用户管理** | 企业用户 + 系统管理员维护，绑定职务/机构/角色/数据权限，支持启用/禁用/重置密码 |
| **机构管理** | 公司组织架构，多层级树形结构，支持拖拽排序 |
| **职位管理** | 职务标签维护，可作为用户属性参与权限判断 |
| **字典管理** | 系统固定数据维护（如性别、状态、业务类型等），支持字典缓存 |
| **主控面板** | 工作台首页、数据分析看板、统计报表展示 |

### 阶段四：扩展功能（Phase 5）

| 模块 | 技术方案 | 功能描述 |
|---|---|---|
| **访问日志** | EF Core 记录 | 用户登录/退出日志查看与管理 |
| **操作日志** | AOP 拦截器 + EF Core | 正常操作 + 异常信息记录与查询 |
| **服务监控** | `System.Diagnostics` | CPU、内存、磁盘、网络实时数据 |
| **在线用户** | SignalR | 当前在线用户列表，支持强制下线 |
| **公告管理** | SignalR | 通知公告发布，实时推送到在线用户 |
| **文件管理** | 策略模式 | 本地存储 / 阿里云 OSS / 腾讯 COS，支持扩展 |
| **任务调度** | Sundial | 分布式作业调度，可视化管理 Cron 任务 |
| **系统配置** | EF Core + 缓存 | 系统运行参数维护，变更实时生效 |
| **邮件短信** | MailKit + 短信 SDK | 发送邮件/短信，支持模板 |
| **导入导出** | Magicodes.IE | Excel 导入导出，H5 模板生成 PDF |
| **限流控制** | AspNetCoreRateLimit | 按 IP/客户端/接口配置访问频率 |
| **开放授权** | OAuth 2.0 | 微信等第三方登录接入 |
| **APIJSON** | 腾讯 APIJSON 协议 | 后端零代码查询接口 |
| **数据库视图** | 自定义查询构建 | 可视化 SQL 查询 + 表实体视图维护 |
| **系统接口** | Swagger + Knife4jUI | 双皮肤 API 文档 |

---

## OctopusOA 功能清单

### 数据持久化（SQLite via EF Core）

OA 后端使用 **Microsoft.EntityFrameworkCore.Sqlite 8.0.15** 持久化。

**目录结构**：
```
OctopusOA.Api/Persistence/
├── Entities.cs       20 个实体类（SyncUser / OaDept / Approval / Employee / Notice / Attendance / Meeting …）
├── OaDbContext.cs    DbSet + Fluent 配置 + List<string>→JSON ValueConverter
└── OaDbSeeder.cs     静态 Seed 方法（幂等：仅在 SyncUsers 为空时插入种子）
```

**文件数据库**：
- 开发：`OctopusOA.Api/octopus_oa.db`（启动时 `EnsureCreated` + `OaDbSeeder.Seed`）
- 测试：共享内存连接 `Data Source=:memory:;Cache=Shared`（OATestFactory 中）
- 种子变更：删除 `octopus_oa.db` 后重启 API 即可

**关键表**（20 个，`oa_*` 前缀，主键都是自增）：
```
用户/部门：  oa_sync_user, oa_dept, oa_user_dept
审批：       oa_workflow_template, oa_workflow_node, oa_approval, oa_approval_record
职员：       oa_employee, oa_employee_education / _work / _family / _emergency
公告：       oa_notice, oa_notice_read
考勤：       oa_attendance_rule, oa_user_shift, oa_attendance, oa_attendance_fix
会议室：     oa_meeting_room, oa_meeting_booking
```

**唯一索引**：`oa_attendance` 对 `(UmcUserId, Date)` 建唯一索引。

**JSON 列**（用 `ValueConverter<List<string>, string>`）：`oa_sync_user.OaRoles`。`OaMeetingRoom.Equipment` 和 `OaMeetingBooking.Attendees` 是手动序列化的 string。

**依赖注入**：所有 Service 是 **Scoped**，跟随 `OaDbContext`。

**补卡审批联动**：`ApprovalService.Approve()` 在审批最终通过且 `template.TemplateCode == "attendance_fix"` 时，通过 `IServiceProvider.GetRequiredService<AttendanceService>()` 直接调用 `HandleApprovalFix(approval)`，共享同一 scope 的 DbContext。

### SSO 与用户同步

| 模块 | 功能描述 |
|---|---|
| **SSO 登录** | 通过 OctopusUMC OIDC 授权码流 + PKCE 实现单点登录 |
| **单点登出** | 退出时调用 UMC Back-Channel Logout 接口，同步注销 UMC 会话 |
| **用户信息** | 展示从 UMC Token Claims 中解析的用户名、角色等信息 |
| **用户同步** | UMC 用户变更通过 Webhook（HMAC-SHA256 签名）推送到 OA SyncUsers 缓存 |
| **首次登录自动注册** | SSO 首次登录时自动在 OA SyncUsers 创建用户记录 |

### OA 本地权限体系（方案二：UMC 管骨架，OA 本地扩展）

**权限来源合并规则**：
```
UMC Token Claims → roles: ["admin", "common"]      ← 全局角色
OA 本地          → oaRoles: ["oa_admin", "oa_user"] ← OA 角色（控制 OA 菜单/功能）
```

| OA 角色 | oaRole | 权限范围 |
|---------|--------|----------|
| OA 管理员 | oa_admin | 全部菜单 + 流程模板管理 + 查看全部审批 |
| 普通员工 | oa_user | 首页 + 我的工作（发起/我的/待审批）+ 个人中心 |
| 部门主管 | oa_manager | oa_user 权限 + 查看本部门全部审批 |

**OA 菜单树**：
```
OA 系统
├── 首页（工作台 + 4 数据卡 + 快捷入口 + 聚合待办）  /home
├── 公告中心                /notice/list
├── 通讯录                  /contact
├── 我的工作
│   ├── 发起申请            /approval/apply
│   ├── 我的申请            /approval/mine
│   └── 待我审批            /approval/pending
├── 考勤
│   ├── 我的考勤（日历+列表） /attendance/mine
│   ├── 考勤统计（oa_admin） /attendance/stats
│   ├── 班次管理（oa_admin） /attendance/shift
│   └── 默认规则（oa_admin） /attendance/rule
├── 会议室
│   ├── 预订日历            /meeting/calendar
│   ├── 我的预订            /meeting/mine
│   └── 会议室管理（oa_admin）/meeting/room
├── 审批管理（oa_admin）
│   ├── 流程模板            /approval/template
│   └── 全部审批            /approval/all
├── 职员管理（oa_admin）    /employee/list
├── 用户管理（oa_admin）    /admin/user
└── 个人中心                /profile
```

**侧边栏权限过滤**：
- 所有人可见：首页、公告、通讯录、我的工作、我的考勤、会议室预订/我的预订、个人中心
- `oa_admin` 额外可见：审批管理、考勤统计/班次/规则、会议室管理、职员管理、用户管理
- 通过 `useOaUserStore().isOaAdmin` 判断

### 可定制审批流引擎

**核心架构**：
```
WorkflowTemplate (流程模板)
  └── WorkflowNode[] (审批节点，有序)
        └── 定义每个节点由谁审批

Approval (审批实例 = 一次具体的申请)
  └── ApprovalRecord[] (审批操作记录)
```

**审批人类型**（可扩展）：

| approverType | 含义 | approverValue 示例 |
|---|---|---|
| `role` | 指定 OA 角色 | `oa_manager` |
| `user` | 指定用户 | `2`（UMC UserId） |
| `dept_leader` | 申请人所在部门的主管 | 留空（运行时解析） |
| `self_select` | 申请人自选审批人 | 留空（提交时指定） |

**FormSchema**：每个模板通过 JSON 定义表单字段，前端动态渲染：
```json
{"fields":[{"key":"leaveType","label":"请假类型","type":"select","options":[...],"required":true},...]}
```

**审批状态流转**：
```
draft → pending → (逐节点审批) → approved
                               → rejected（任一节点驳回）
       → cancelled（申请人撤回，仅 pending 时可撤）
```

**种子模板**：
- 请假审批（leave）：直属主管审批 → HR 确认（2 节点）
- 报销审批（expense）：直属主管审批 → 财务审核 → 总经理审批（3 节点）

**数据表**：

| 表 | 说明 |
|---|---|
| `oa_workflow_template` | 流程模板（TemplateId, TemplateName, TemplateCode, FormSchema, Status） |
| `oa_workflow_node` | 审批节点（NodeId, TemplateId, NodeName, NodeOrder, ApproverType, ApproverValue） |
| `oa_approval` | 审批实例（ApprovalId, TemplateId, Title, ApplicantId, CurrentNodeOrder, Status, FormData） |
| `oa_approval_record` | 审批记录（RecordId, ApprovalId, NodeId, ApproverId, Action, Comment） |

### OA API 接口清单

**审批模板管理（oa_admin）**：
```
GET    /api/approval/template/list         模板列表
GET    /api/approval/template/{id}         模板详情（含节点）
POST   /api/approval/template              创建模板
PUT    /api/approval/template              修改模板
DELETE /api/approval/template/{id}         删除模板
POST   /api/approval/template/{id}/nodes   设置模板节点（整体替换）
```

**审批操作**：
```
GET    /api/approval/templates             可用模板列表（发起时选择）
POST   /api/approval/submit                提交审批申请
GET    /api/approval/mine                  我的申请列表
GET    /api/approval/pending               待我审批列表
GET    /api/approval/{id}                  审批详情（含流程记录）
PUT    /api/approval/{id}/approve          通过
PUT    /api/approval/{id}/reject           驳回
PUT    /api/approval/{id}/cancel           撤回（仅申请人）
GET    /api/approval/all                   全部审批（oa_admin）
```

**基础接口**：
```
GET    /api/me                             当前用户信息（从 JWT Claims）
POST   /api/users/sync                     接收 UMC 用户同步（HMAC-SHA256 签名）
GET    /api/users/sync                     已同步用户列表
POST   /api/auth/backchannel-logout        接收 UMC 登出通知
```

### 职员管理（入职档案）

**核心流程**：HR 创建临时档案 → 发 H5 链接 → 候选人自助填写 → HR 审核 → 确认入职（自动创建 UMC 账号）

**状态流转（五状态）**：
```
temp(临时)          HR 创建档案，系统生成 H5 token
   ↓
pending(待入职)     候选人通过 H5 填写完信息，等待 HR 审核
   ├→ rejected(已拒绝)   HR 拒绝 / 候选人放弃
   ↓
active(在职)        HR 确认入职 → 自动创建 UMC 账号 + OA 同步
   ↓
resigned(离职)      HR 操作离职 → 禁用 UMC 账号
```

**数据表**：

| 表 | 说明 |
|---|---|
| `Employee` | 职员档案主表（HR 填写 + H5 填写 + 系统字段） |
| `EmployeeEducation` | 教育经历（可多条） |
| `EmployeeWorkHistory` | 工作经历（可多条） |
| `EmployeeFamily` | 家庭成员（可多条） |
| `EmployeeEmergencyContact` | 紧急联系人（至少1人） |

**HR 填写字段**（从简历获取）：姓名、性别、手机号、出生日期、邮箱、民族、应聘岗位/部门、学历、院校、专业、期望薪资、工作年限、简历附件、HR 备注

**H5 入职人员填写**（6 个模块）：
- 个人信息：证件照、身份证号/正反面、政治面貌、婚姻状况、籍贯、现居住/户籍地址
- 教育经历：学校、学历、专业、起止时间、学位（可多条）
- 工作经历：公司、职位、起止时间、离职原因（可多条）
- 家庭成员：姓名、关系、工作单位、电话（可多条）
- 紧急联系人：姓名、关系、电话（至少1人）
- 银行卡信息：开户银行、账号、支行

**职员管理 API**：
```
POST   /api/employee                    创建临时档案（返回 h5Token）
GET    /api/employee/list               档案列表（按状态/姓名筛选）
GET    /api/employee/{id}               档案详情（含子表）
PUT    /api/employee/{id}               修改 HR 填写部分
PUT    /api/employee/{id}/confirm       确认入职（→ active）
PUT    /api/employee/{id}/reject        拒绝入职（→ rejected）
PUT    /api/employee/{id}/resign        办理离职（→ resigned）
DELETE /api/employee/{id}               删除档案（仅 temp/rejected）
```

**H5 端 API**（无需登录，token 验证）：
```
GET    /api/h5/onboard/{token}          获取 H5 预填数据
POST   /api/h5/onboard/{token}          提交入职信息（→ pending）
```

### OA 用户管理

**OA 用户管理 API**：
```
GET    /api/oa/user/list                已同步用户列表（含 oaRoles）
PUT    /api/oa/user/{id}/roles          修改用户 OA 角色
GET    /api/oa/role/list                OA 角色列表
```

### 审批流程图组件

审批详情页使用 `WorkflowGraph.vue` 纯 CSS 流程图组件替代 el-steps：
```
[申请人] → [直属主管审批 ✓] → [财务审核 ✓] → [总经理审批 ●] → [进行中]
```
节点状态：绿色(已通过)、红色(已驳回)、橙色(审批中)、灰色(未到达)

### SSO 登录流程（完整链路）

```
OA 点击登录
  → oidc-client-ts signinRedirect()
  → UMC /connect/authorize（未登录）
  → 重定向到 UMC 前端 http://localhost:5173/login?returnUrl=/connect/authorize?...
  → 用户输入 admin/Admin@123 登录
  → UMC 前端检测 returnUrl 以 /connect/ 开头
  → window.location.href 跳回 UMC API /connect/authorize（已有 Cookie）
  → OpenIddict 颁发授权码
  → 重定向到 OA /callback
  → oidc-client-ts signinRedirectCallback() 换取 access_token
  → 进入 OA 首页
```

### OA 常用功能（五步走，已完成）

详细实施方案见项目根目录 [`ROADMAP.md`](./ROADMAP.md)。

#### 通讯录（基于 UMC 部门同步）

**数据表**：
| 表 | 说明 |
|---|---|
| `OaDept` | 从 UMC 同步的部门缓存（deptId, parentId, deptName, orderNum, status） |
| `OaUserDept` | 用户-部门关联（UmcUserId, DeptId, PostId, PostName, IsPrimary） |

**API**：
```
GET  /api/contact/dept/tree              部门树（含每节点用户数）
GET  /api/contact/users?deptId=&keyword= 用户列表（部门过滤 + 搜索）
GET  /api/contact/user/{umcUserId}       用户详情（含跨公司任职）
POST /api/contact/dept/sync              UMC 部门推送（HMAC 签名）
```

**前端**：左树右卡片 + 详情抽屉（拨号/发邮件/复制）

#### 公告通知（UMC 发布 → OA 接收）

**数据表**：
| 表 | 说明 |
|---|---|
| `OaNotice` | 镜像 UMC 公告（含类型 1=通知/2=公告/3=紧急、priority） |
| `OaNoticeRead` | 用户已读记录（每用户独立） |

**API**：
```
POST /api/notice/sync                    接收 UMC 推送（HMAC）
GET  /api/notice/list?type=&status=      列表（含已读状态）
GET  /api/notice/{id}                    详情（自动标记已读）
GET  /api/notice/unread/count            未读数
GET  /api/notice/latest                  首页最新 5 条
```

**前端**：公告列表（类型/已读筛选） + 详情 + 首页卡片 + 侧边栏未读徽标

#### 考勤打卡（含日历视图 + 多班次）

**数据表**：
| 表 | 说明 |
|---|---|
| `OaAttendanceRule` | 班次（Code/Name/WorkStartTime/WorkEndTime/LateThresholdMin/EarlyLeaveThresholdMin/IpWhiteList/IsDefault） |
| `OaUserShift` | 用户 → 班次关联（UmcUserId, ShiftId, AssignedAt） |
| `OaAttendance` | 每日打卡（UserId + Date 唯一，含 CheckIn/Out Time/Status/Ip） |
| `OaAttendanceFix` | 补卡记录（关联 ApprovalId） |

**4 种子班次**：标准班（09:00-18:00）/ 早班（07:00-16:00）/ 晚班（14:00-23:00）/ 弹性班（11:00-20:00）

**补卡流程**：员工发起「补卡审批」（template_code=`attendance_fix`）→ 走审批流 → `ApprovalService.OnApproved` 事件触发 → AttendanceService 自动更新考勤 + 创建 Fix 记录

**API**：
```
POST /api/attendance/check-in            上班打卡
POST /api/attendance/check-out           下班打卡
GET  /api/attendance/today               今日状态
GET  /api/attendance/mine?month=         月度考勤
GET  /api/attendance/stats?month=        统计
GET  /api/attendance/abnormal?month=     异常列表
GET  /api/attendance/rule                默认规则
PUT  /api/attendance/rule                修改默认规则
GET  /api/attendance/shift/list          班次列表
POST /api/attendance/shift               新建班次
PUT  /api/attendance/shift               修改
DELETE /api/attendance/shift/{id}        删除（默认/已分配不可删）
PUT  /api/attendance/shift/{id}/default  设为默认
GET  /api/attendance/user-shift/list     所有用户的班次分配
PUT  /api/attendance/user-shift          分配用户班次
```

**前端**：我的考勤（**日历/列表 Tab 切换** + 打卡卡片） + 考勤统计（员工+异常） + 班次管理（CRUD + 员工分配） + 默认规则

#### 待办中心（聚合首页）

**数据来源**：Approval + OaNotice + OaAttendance + OaMeetingBooking（无新表）

**API**：
```
GET /api/dashboard/summary               首页 4 张卡数据 + 今日考勤快照
GET /api/dashboard/todos?type=           聚合待办（all / approval / notice / attendance / meeting）
```

**前端首页**：欢迎卡 + 4 数据卡（待审批/未读公告/今日考勤状态/今日会议数）+ 5 快捷入口 + 聚合待办列表（按类型切换）

#### 会议室预订

**数据表**：
| 表 | 说明 |
|---|---|
| `OaMeetingRoom` | 会议室（Name, Capacity, Location, Equipment[JSON], Status） |
| `OaMeetingBooking` | 预订（RoomId, Title, UmcUserId, StartTime, EndTime, Attendees[JSON], Status: confirmed/cancelled） |

**冲突检测**：同房间 `status=confirmed` 且时段重叠 → 返回 409

**API**：
```
GET/POST/PUT/DELETE /api/meeting/room       会议室 CRUD
GET  /api/meeting/room/{id}/calendar        单房间日历（day/week）
POST /api/meeting/booking                   预订（冲突检测）
PUT  /api/meeting/booking/{id}/cancel       取消（仅预订人 + 未开始）
GET  /api/meeting/booking/mine              我的预订
GET  /api/meeting/booking/today             今日参与（主办+被邀）
GET  /api/meeting/calendar                  总览日历（所有房间）
```

**前端**：预订日历（周视图：会议室×7天矩阵，空格一键预订）+ 我的预订 + 会议室管理（CRUD）

### 全局技术约定

**统一 HTTP 工厂** `src/utils/http.ts`：
- `createAuthedHttp(baseURL)` — 带 JWT + 自动时间格式化
- `createPublicHttp(baseURL)` — 无 JWT（H5 用）+ 自动时间格式化
- 响应拦截器：所有字段名以 `Time`/`At`/`Date` 结尾且值为 ISO UTC 的字符串，自动转为 `yyyy-MM-dd HH:mm:ss` 北京时间

**时间处理**：
- 后端统一 `DateTime.UtcNow` 存储
- 前端展示由 axios 拦截器自动转换，无需单独格式化
- `src/utils/datetime.ts` 提供 `formatBeijingTime` / `formatBeijingShort` 辅助函数

**测试用户隔离**：
- `TestAuthHandler` 支持 `X-Test-UserId` header 切换测试用户
- 各测试用独立用户 ID 避免 Singleton 数据干扰

**弹出层组件（Dialog / Sheet / Select）**：
- OA.Web 中这三类组件为**自定义实现**，不使用 shadcn-vue 安装版本（底层 reka-ui）
- 原因：reka-ui 的 `DismissableLayer` / `FocusScope` / `useBodyScrollLock` 持有模块级单例状态，约 15 次 SPA 导航后导致 UI 完全冻结
- 实现模式：`provide/inject` 传递 `{ open, close }` 上下文 + Vue 原生 `<Teleport to="body">` + `v-if`（Dialog/Sheet）；Select 用 `position: absolute` 下拉，无 Teleport
- 组件位置：`src/components/ui/dialog/`、`src/components/ui/sheet/`、`src/components/ui/select/`
- 消费代码（views）接口不变，零修改
- `router.afterEach` 中保留防御性 body 样式重置（pointerEvents / overflow / paddingRight / marginRight + `#app` aria-hidden/inert 清理）

### 其他 OA 功能（未来扩展）

| 模块 | 功能描述 |
|---|---|
| **文件上传** | OSS 预签名上传 |
| **即时消息** | 站内信 / @我的消息 |
| **任务协作** | 任务看板 / 分派跟踪 |
| **文档中心** | 知识库 + 版本管理 |

---

## 开发流程（六步闸门制）

> **铁律**：每一步都有明确的通过标准（Gate），未全部通过禁止进入下一步。

```
Step 1 前端样板  →  Step 2 后端骨架  →  Step 3 接口确认
                                              ↓
Step 6 功能验收  ←  Step 5 SSO打通  ←  Step 4 前后端联调
```

---

### Step 1：前端样板（仅 UI，无后端依赖）

**目标**：两个项目的完整前端壳子跑通，全部使用 Mock 数据，不依赖任何后端接口。

#### 1.1 Mock 方案

使用 `msw`（Mock Service Worker）拦截请求，返回硬编码假数据：

```bash
npm install -D msw
```

在 `src/mock/` 目录下按模块组织 handler，开发环境自动启用，生产构建自动剔除。

```typescript
// src/mock/system/user.ts
import { http, HttpResponse } from "msw";
import type { ApiResponse, PagedResult } from "@/api/types";
import type { UserResponse } from "@/api/system/types";

export const userHandlers = [
  http.get("/api/system/user/list", () => {
    return HttpResponse.json<ApiResponse<PagedResult<UserResponse>>>({
      code: 200,
      msg: "ok",
      data: { rows: [...mockUsers], total: 2 }
    });
  })
];
```

#### 1.2 UMC.Web 需完成的页面

| 页面 | 路由 | Mock 数据 |
|---|---|---|
| 登录页 | `/login` | 任意账号密码直接登录成功 |
| 工作台 | `/dashboard/workbench` | 静态数字卡片、公告列表 |
| 用户管理 | `/system/user` | 用户列表、新增/编辑弹窗 |
| 机构管理 | `/system/dept` | 树形结构，支持展开/折叠 |
| 职位管理 | `/system/post` | 简单列表 |
| 菜单管理 | `/system/menu` | 树形表格，三级结构 |
| 角色管理 | `/system/role` | 列表 + 菜单权限树弹窗 |
| 字典管理 | `/system/dict` | 类型列表 + 右侧数据面板 |
| 个人中心 | `/profile` | 资料表单、修改密码 Tab |

#### 1.3 OA.Web 需完成的页面

| 页面 | 路由 | Mock 数据 |
|---|---|---|
| 登录/回调页 | `/login`、`/callback` | Mock OIDC 流程（点击登录直接跳回） |
| 首页 | `/` | 显示 Mock 用户名、公告列表 |
| 个人中心 | `/profile` | 从 Mock Token Claims 展示用户信息 |

#### Step 1 通过标准（Gate 1）

- [ ] `npm run dev` 两个项目均无报错启动
- [ ] `npx tsc --noEmit` 零 TypeScript 类型错误
- [ ] 所有页面可正常导航，无白屏、无控制台报错
- [ ] Mock 数据正确渲染（表格有数据、树形结构正常展开）
- [ ] 登录/登出流程 UI 完整（不需要真实认证）
- [ ] **确认通过后方可进入 Step 2**

---

### Step 2：后端骨架（内存模拟，不连数据库）

**目标**：两个项目后端 API 跑通，全部用硬编码内存数据响应，不连 MySQL，不接 OpenIddict。

#### 2.1 持久化方式（SQLite）

使用 **SQLite 文件数据库**（开发）+ **SQLite 内存模式**（测试），不连 MySQL，不接 OpenIddict。

```bash
# 开发模式：SQLite 文件（可用 DB Browser 等工具直接查看表结构）
Data Source=octopus_umc.db

# 测试模式（TestWebApplicationFactory）：SQLite 内存 + 持久化连接
Data Source=:memory:;Cache=Shared
```

**种子数据结构（`DbSeeder.cs`）：**

```
公司A ── 章鱼科技有限公司 (DeptId=1)
         ├── 总裁办  (DeptId=2)  →  admin(主)
         ├── 技术部  (DeptId=3)  →  zhangsan(主) + lisi
         ├── 市场部  (DeptId=4)  →  wangwu(禁用)
         └── 行政部  (DeptId=5)  →  editor

公司B ── 海星科技有限公司 (DeptId=9)
         ├── 技术部  (DeptId=10) →  zhangsan(兼职副部门) + zhaoliu(主)
         └── 市场部  (DeptId=11) →  (暂无人员)

跨公司兼职示例：
  zhangsan(UserId=2)
    sys_user_dept: { DeptId=3,  PostId=3(技术总监), IsPrimary=true  }  ← A公司技术部（主）
    sys_user_dept: { DeptId=10, PostId=4(工程师),   IsPrimary=false }  ← B公司技术部（兼职）
```

**种子用户列表：**

| UserId | UserName | NickName | 所属公司 | 主部门 | 状态 |
|---|---|---|---|---|---|
| 1 | admin | 超级管理员 | 章鱼科技 | 总裁办 | 启用 |
| 2 | zhangsan | 张三 | 章鱼科技(主) / 海星科技(兼) | 技术部 / 技术部 | 启用 |
| 3 | lisi | 李四 | 章鱼科技 | 技术部 | 启用 |
| 4 | wangwu | 王五 | 章鱼科技 | 市场部 | **禁用** |
| 5 | editor | 编辑员 | 章鱼科技 | 行政部 | 启用 |
| 6 | zhaoliu | 赵六 | 海星科技 | 技术部 | 启用 |

> **种子数据变更时**：删除 `src/OctopusUMC.Api/octopus_umc.db` 后重新启动 API，数据库会自动重建并重新植入。

#### 2.2 UMC.Api 需完成的接口

**用户管理**
```
GET    /api/system/user/list             分页列表（支持按用户名/手机号/状态筛选）
                                         ?deptId=N 按部门过滤（基于 sys_user_dept 关联表，
                                         支持多公司场景：同一用户可出现在多家公司的查询结果中）
GET    /api/system/user/{id}             详情（deptId/deptName 始终返回主部门 IsPrimary=true）
GET    /api/system/user/{userId}/depts   用户所属的全部部门（含兼职/多公司），返回 IsPrimary 标志
POST   /api/system/user                  新增
PUT    /api/system/user                  修改
DELETE /api/system/user/{ids}            删除（批量）
PUT    /api/system/user/status           启用/禁用
PUT    /api/system/user/resetPwd         重置密码
```

**机构管理**
```
GET    /api/system/dept/tree         树形结构（含子节点）
POST   /api/system/dept              新增
PUT    /api/system/dept              修改
DELETE /api/system/dept/{id}         删除
```

**职位管理**
```
GET    /api/system/post/list         列表
POST   /api/system/post              新增
PUT    /api/system/post              修改
DELETE /api/system/post/{ids}        删除
```

**菜单管理**
```
GET    /api/system/menu/tree         完整菜单树
GET    /api/system/menu/list         列表（管理用）
POST   /api/system/menu              新增
PUT    /api/system/menu              修改
DELETE /api/system/menu/{id}         删除
```

**角色管理**
```
GET    /api/system/role/list         分页列表
POST   /api/system/role              新增
PUT    /api/system/role              修改
DELETE /api/system/role/{ids}        删除
POST   /api/system/role/menu         角色绑定菜单（权限分配）
POST   /api/system/role/dept         角色绑定数据权限范围
```

**字典管理**
```
GET    /api/system/dict/type/list              类型列表
POST/PUT/DELETE /api/system/dict/type          类型增删改
GET    /api/system/dict/data/type/{dictType}   按类型查数据（带缓存）
POST/PUT/DELETE /api/system/dict/data          数据增删改
```

**账户接口（UMC 自身登录）**
```
POST   /api/account/login            内存校验，返回假 Cookie
POST   /api/account/logout           清除 Cookie
GET    /api/account/me               返回内存中的用户信息
```

#### 2.3 OA.Api 需完成的接口

```
GET    /api/me                       返回当前用户信息（从假 Token 解析）
POST   /api/users/sync               接收 UMC 推送的用户同步数据
POST   /api/auth/backchannel-logout  接收 UMC 的登出通知
```

#### Step 2 通过标准（Gate 2）

- [ ] 所有接口在 Swagger UI 可见，路径与上方清单一致
- [ ] 每个接口手动调用返回合理的 Mock 数据（非 500）
- [ ] 增删改查逻辑在内存中闭环（新增后查询能看到）
- [ ] 统一响应格式 `{ code, msg, data }` 所有接口一致
- [ ] **确认通过后方可进入 Step 3**

#### Step 2 验证命令

**运行全量测试（静默，只看结果）：**
```bash
cd OctopusUMC
export PATH="$PATH:/usr/local/share/dotnet"
dotnet test
# 预期：已通过! - 失败: 0，通过: 79，总计: 79

cd ../OctopusOA
dotnet test
# 预期：已通过! - 失败: 0，通过: 6，总计: 6
```

**运行业务流程日志（可读性输出，验证完整链路）：**
```bash
cd OctopusUMC
# 全部 5 条 Flow 日志
dotnet test --filter "Flow" --logger "console;verbosity=detailed"

# 单独运行某条 Flow
dotnet test --filter "Flow5" --logger "console;verbosity=detailed"
```

输出 5 条完整业务链路日志：

| 流程 | 步骤 | 覆盖场景 |
|---|---|---|
| **Flow 1** 用户管理链路 | 10步 | 管理员登录 → 建部门/职位/角色 → 新增用户 → 验证关联 → 新用户登录验权 → 按部门过滤（数据权限模拟） |
| **Flow 2** 角色权限链路 | 6步 | 读菜单树 → 建角色 → 绑菜单 → 验证绑定结果 → 五种数据权限范围逐一验证 |
| **Flow 3** 字典管理链路 | 5步 | 读类型列表 → 读字典数据 → 建类型 → 新增数据条目 → 按编码查询验证 |
| **Flow 4** 用户状态链路 | 9步 | 建用户 → 正常登录 → 禁用 → 禁用后登录被拒（403）→ 重新启用 → 重置密码 → 新密码登录 → 旧密码失效（401） |
| **Flow 5** 多公司数据权限链路 | 7步 | 部门树含两家公司 → 查 zhangsan 跨公司任职记录 → A 公司部门查询 → B 公司部门查询 → 空部门查询 → 跨公司兼职交集统计 → 新增员工验证数据隔离 |

**测试文件对应关系：**

| 测试文件 | 测试数 | 覆盖模块 |
|---|---|---|
| `AccountControllerTests.cs` | 6 | 登录/登出/权限返回 |
| `UserControllerTests.cs` | 9 | 用户 CRUD + 状态/密码操作 |
| `DeptControllerTests.cs` | 7 | 部门 CRUD + 树形结构 + 删除保护 |
| `PostControllerTests.cs` | 10 | 职位 CRUD + 编码唯一性 + 批量删除 |
| `RoleControllerTests.cs` | 13 | 角色 CRUD + 绑定菜单 + 五种数据权限范围 |
| `UserDataPermissionTests.cs` | 16 | 按部门过滤用户列表 + 角色绑定 + 菜单权限差异 |
| `BusinessFlowTests.cs` | 4 | 端到端业务流程 Flow 1–4（含详细日志输出） |
| `MultiCompanyTests.cs` | 6 | 多公司场景：跨公司兼职 + `?deptId` 数据权限过滤 + 数据隔离 |

**注意事项：**
- `TestWebApplicationFactory` 已关闭 Serilog 控制台输出，`dotnet test` 输出保持干净
- 需要查看每条请求日志时改用 `--logger "console;verbosity=detailed"`
- 数据库：测试使用 **SQLite 内存模式**（`Data Source=:memory:`），持久化连接防止测试间数据丢失
- 种子数据变更后，`octopus_umc.db`（开发用的 SQLite 文件）需删除重建：`rm src/OctopusUMC.Api/octopus_umc.db`
- 状态变更测试（创建/删除临时资源）均在结束后清理，避免污染种子数据

---

### Step 3：接口层确认（Swagger + DTO 对齐）

**目标**：逐一核对接口契约，确保后端 DTO 与前端 TypeScript 类型完全对齐，流程符合实际业务逻辑。

#### 3.1 Swagger 配置

```csharp
// 启用 XML 注释
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OctopusUMC API", Version = "v1" });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});
```

每个接口必须有 XML 注释说明入参和返回值含义。

#### 3.2 DTO 对齐检查清单

对每个模块执行以下检查：

```
后端 C# DTO  ←→  Swagger JSON Schema  ←→  前端 TypeScript interface
```

- [ ] 字段名（camelCase 序列化后）与前端 TypeScript 字段名完全一致
- [ ] 可空字段在 DTO 中标 `?`，TypeScript 中对应 `T | null`
- [ ] 枚举类型：后端序列化为数字还是字符串，前端对应 union type
- [ ] 分页接口返回 `{ rows: T[], total: number }` 结构一致
- [ ] 日期字段统一为 ISO 8601 字符串

#### 3.3 业务流程核查

逐一在 Swagger 中模拟完整业务链路：

| 流程 | 步骤 |
|---|---|
| 新增用户 | 查部门树 → 查职位列表 → 查角色列表 → 提交新增 → 查列表验证 |
| 角色授权 | 查菜单树 → 勾选权限 → 提交绑定 → 查角色验证菜单已绑定 |
| 数据权限 | 查部门树 → 配置角色数据范围 → 验证范围类型正确保存 |
| 字典使用 | 新增字典类型 → 新增字典数据 → 按类型查询验证返回 |

#### Step 3 通过标准（Gate 3）

- [x] Swagger 可正常访问，所有接口有文档注释（XML 注释 + `GenerateDocumentationFile` 已启用）
- [x] 每个接口的 Request/Response Schema 与前端 TypeScript 类型零差异（`npx tsc --noEmit` 零错误）
- [x] 上表业务流程全部由集成测试覆盖（73 个测试全部通过，BusinessFlowTests 涵盖 4 条端到端链路）
- [x] 发现的 DTO 字段差异已同步修正（见下方 Step 3 对齐记录）
- [x] **Step 3 已通过，可进入 Step 4**

#### Step 3 对齐修正记录

**新增前端类型（`src/api/types.ts`）：**
- `LoginRequest` — 对应后端 `LoginRequest`（userName, password, rememberMe?）
- `LoginResponse` — 对应后端 `LoginResponse`（userId, userName, nickName, avatar, roles[], permissions[]）

**新增前端类型（`src/api/system/types.ts`）：**
- `UpdateDictTypeRequest` — 字典类型修改（dictId + 字段）
- `UpdateDictDataRequest` — 字典数据修改（dictCode + 字段）
- `ResetPasswordRequest` — 重置密码（userId, newPassword）
- `UpdateStatusRequest` — 启用/禁用（userId, status）
- `RoleMenuRequest` — 角色绑定菜单（roleId, menuIds[]）
- `RoleDeptRequest` — 角色绑定数据权限（roleId, dataScope, deptIds[]）
- `CreateOidcClientRequest` / `UpdateOidcClientRequest` — OIDC 客户端管理

**架构修正：**
- `src/utils/http.ts`：移除 Bearer Token 认证头，改用 `withCredentials: true`（Cookie 认证）
- `src/store/modules/user.ts`：重写为基于 `LoginResponse` 的 Cookie 认证模式，移除 localStorage token 逻辑
- `src/mock/handlers/account.ts`：Mock 响应格式对齐 `LoginResponse`（移除 token 字段）
- `src/router/index.ts`：路由守卫改用 Pinia store 判断登录状态

**新增 API 请求函数文件：**
```
src/api/account.ts          — login / logout / getMe
src/api/system/user.ts      — 用户 CRUD + 状态/密码/角色绑定
src/api/system/role.ts      — 角色 CRUD + 菜单/部门绑定
src/api/system/menu.ts      — 菜单 CRUD + 角色菜单查询
src/api/system/dept.ts      — 部门 CRUD + 树形查询
src/api/system/post.ts      — 职位 CRUD
src/api/system/dict.ts      — 字典类型/数据 CRUD
src/api/system/notice.ts    — 公告 CRUD
src/api/monitor/online.ts   — 在线用户 + 强制下线
src/api/monitor/operlog.ts  — 操作日志 + 登录日志 + 服务器信息
```

**Step 3 验证命令：**
```bash
# 后端零错误构建
cd OctopusUMC && dotnet build
# 73/73 测试通过
dotnet test
# 前端零 TypeScript 错误
cd web/OctopusUMC.Web && npx tsc --noEmit
```

---

### Step 4：前后端联调（替换 Mock，接真实接口）

**目标**：前端 Mock 全部替换为真实后端调用，权限体系在前后端联动下正确生效。

#### 4.1 替换顺序

1. 移除 MSW Mock，开启真实 Axios 请求
2. 登录接口联调（UMC.Web → `/api/account/login`）
3. 动态菜单路由联调（登录后调 `/api/system/menu/tree`，前端动态注册路由）
4. 按模块逐一联调：用户 → 机构 → 职位 → 菜单 → 角色 → 字典

#### 4.2 权限联调重点

**菜单权限**：
- 不同角色登录后侧边栏显示不同菜单
- 直接访问无权限路由返回 403 页面

**按钮权限**：
- `v-auth="'system:user:add'"` 指令控制按钮显示/隐藏
- 即使绕过前端直接调 API，后端 `[HasPermission]` 也应返回 403

**数据权限**：
- A 用户（本部门范围）只看到本部门数据
- B 用户（全部数据范围）可看到所有数据

#### 4.3 此阶段接入真实数据库

Step 4 开始正式替换内存数据为 EF Core + MySQL：
```bash
dotnet ef migrations add InitialCreate --project ../OctopusUMC.Infrastructure
dotnet ef database update
```

#### Step 4 通过标准（Gate 4）

- [ ] 前端零 Mock 请求，全部走真实接口
- [ ] 登录/登出流程正常，Cookie 正确设置和清除
- [ ] 动态菜单路由按角色正确渲染
- [ ] 菜单权限：不同角色看到不同菜单，无权限路由被拦截
- [ ] 按钮权限：无权限用户看不到对应操作按钮，直接调 API 也返回 403
- [ ] 数据权限：各数据范围过滤结果正确
- [ ] 所有 CRUD 操作在真实数据库中持久化
- [ ] **确认通过后方可进入 Step 5**

---

### Step 5：SSO 打通与用户数据同步

**目标**：OA 通过 UMC OIDC 实现 SSO，用户数据在两系统间正确同步，退出时会话联动注销。

#### 5.1 用户数据架构（核心设计）

```
UMC（主库）                     OA（同步缓存）
─────────────────               ────────────────────
Users 表（权威数据源）    →同步→  SyncUsers 表（只读缓存）
  - 用户名、邮件、手机            - UMC 用户 ID（外键引用）
  - 密码哈希                      - 用户名、邮件、头像
  - 状态、角色                     - 最后同步时间
                                  - OA 本地角色/权限
```

**规则**：
- OA 的 `SyncUsers` 表**不存密码**，不参与认证，只做展示和 OA 内部授权
- UMC 用户变更时主动推送给 OA，OA 更新 `SyncUsers` 缓存
- OA 不能修改用户基础信息（用户名、邮箱），只能修改 OA 本地权限

#### 5.2 用户首次 SSO 登录（自动注册）

```
用户通过 OIDC 登录 OA
    ↓
OA.Api 收到 access_token
    ↓
解析 token claims（sub = UMC userId）
    ↓
查询 SyncUsers 是否存在该 userId
    ├─ 不存在 → 调用 UMC /api/users/{id} 获取完整用户信息
    │           → 创建 SyncUsers 记录
    └─ 存在   → 正常放行
```

#### 5.3 UMC 用户变更同步到 OA

UMC 修改用户信息时，主动推送 Webhook 给 OA：

```csharp
// UMC：用户更新后触发同步
// Api/Services/UserSyncService.cs
public async Task NotifyOAUserUpdatedAsync(long userId)
{
    var user = await _userRepository.GetByIdAsync(userId);
    var payload = new UserSyncPayload
    {
        UserId = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        Status = user.Status,
        SyncAt = DateTime.UtcNow
    };
    // POST 到 OA /api/users/sync（携带共享密钥签名）
    await _httpClient.PostAsJsonAsync($"{_oaBaseUrl}/api/users/sync", payload);
}
```

```csharp
// OA：接收同步推送
// Api/Controllers/UserSyncController.cs
[HttpPost("/api/users/sync")]
[AllowAnonymous] // 通过 Header 签名验证，非 JWT
public async Task<IActionResult> SyncUser([FromBody] UserSyncPayload payload)
{
    // 验证 X-Sync-Signature Header
    // 更新或插入 SyncUsers 表
    // 如果 Status = 禁用，强制下线该用户的 OA 会话
}
```

**触发同步的 UMC 操作**：
- 修改用户基础信息（用户名、邮件、手机、头像）
- 修改用户状态（启用/禁用）
- 删除用户（OA 需清除缓存并强制下线）

#### 5.4 单点登出（Back-Channel Logout）

```
用户在 OA 点击退出
    ↓
OA.Web: userManager.signoutRedirect()
    ↓
浏览器跳转 UMC /connect/logout
    ↓
UMC 清除自身 Cookie 会话
UMC 向 OA POST /api/auth/backchannel-logout（携带 logout_token）
    ↓
OA 验证 logout_token → 撤销该用户的 OA Token/会话
    ↓
重定向回 OA 首页（未登录状态）
```

#### 5.5 access_token 过期处理

OA.Web 中配置 silent refresh，token 即将过期时自动后台刷新：

```typescript
// src/services/oidcService.ts
export const userManager = new UserManager({
  // ...其他配置
  automaticSilentRenew: true,           // 自动静默刷新
  silentRequestTimeoutInSeconds: 10,
  accessTokenExpiringNotificationTimeInSeconds: 60, // 提前 60 秒刷新
});

// 监听 token 刷新失败（网络断开等），引导用户重新登录
userManager.events.addSilentRenewError(() => {
  oidcService.login();
});
```

#### 5.6 同步安全机制

UMC 推送到 OA 的 Webhook 使用共享密钥 HMAC-SHA256 签名：

```
Header: X-Sync-Signature: sha256=<HMAC-SHA256(body, sharedSecret)>
```

OA 收到请求后先验签，防止伪造同步请求。

#### Step 5 通过标准（Gate 5）

- [ ] OA.Web 点击登录 → 跳转 UMC 登录页 → 登录后跳回 OA 显示用户名
- [ ] OA 首次登录自动在 `SyncUsers` 创建用户记录
- [ ] UMC 修改用户信息 → OA `SyncUsers` 自动更新（≤ 3 秒）
- [ ] UMC 禁用用户 → OA 该用户会话失效，下次请求返回 401
- [ ] OA 退出 → UMC Cookie 会话已清除（访问 UMC 需重新登录）
- [ ] UMC 退出 → OA 会话通过 Back-Channel Logout 联动注销
- [ ] access_token 过期时 OA 自动静默刷新，用户无感知
- [ ] Webhook 签名验证：伪造请求返回 401
- [ ] **确认通过后方可进入 Step 6**

---

### Step 6：功能验收

**目标**：两个系统完整功能的端到端验收，达到生产环境交付标准。

#### 6.1 UMC 验收清单

**权限体系**
- [ ] 菜单权限：Admin 看全部菜单，普通用户按角色看子集
- [ ] 按钮权限：增删改按钮按权限标识显示/隐藏
- [ ] 数据权限：五种范围（全部/本部门及子部门/本部门/仅本人/自定义）各自过滤正确

**用户管理**
- [ ] 用户 CRUD 完整（新增/编辑/删除/批量删除/启用禁用/重置密码）
- [ ] 用户绑定机构、职位、角色正常保存
- [ ] 用户变更后同步推送到 OA

**机构 / 职位 / 菜单 / 角色 / 字典**
- [ ] 各模块增删改查闭环

#### 6.2 OA 验收清单

- [ ] SSO 登录 / 登出完整闭环
- [ ] 用户信息从 UMC Token Claims 正确展示
- [ ] SyncUsers 缓存与 UMC 主库一致
- [ ] OA 本地权限配置（OA 角色绑定）不影响 UMC 数据

#### 6.3 集成验收清单

- [ ] TDD 测试：`dotnet test` 全部通过，零失败
- [ ] TypeScript：`npx tsc --noEmit` 零类型错误
- [ ] 关键接口有 Swagger 文档和入参校验
- [ ] 日志：关键操作有 Serilog 日志输出
- [ ] 异常处理：接口异常返回统一格式，不暴露堆栈信息

---

## 分阶段实施计划

### Phase 0：脚手架搭建

**目标**：四个项目全部初始化，能跑起来。

```bash
# UMC 解决方案（含测试项目）
mkdir -p OctopusUMC/src OctopusUMC/tests OctopusUMC/web
cd OctopusUMC
dotnet new sln -n OctopusUMC

dotnet new classlib  -n OctopusUMC.Core           -o src/OctopusUMC.Core
dotnet new classlib  -n OctopusUMC.Infrastructure  -o src/OctopusUMC.Infrastructure
dotnet new webapi    -n OctopusUMC.Api             -o src/OctopusUMC.Api
dotnet new xunit     -n OctopusUMC.Core.Tests      -o tests/OctopusUMC.Core.Tests
dotnet new xunit     -n OctopusUMC.Infrastructure.Tests -o tests/OctopusUMC.Infrastructure.Tests
dotnet new xunit     -n OctopusUMC.Api.Tests       -o tests/OctopusUMC.Api.Tests

# 添加到解决方案
dotnet sln add src/**/*.csproj tests/**/*.csproj

# 项目引用
dotnet add src/OctopusUMC.Infrastructure reference src/OctopusUMC.Core
dotnet add src/OctopusUMC.Api reference src/OctopusUMC.Infrastructure
dotnet add src/OctopusUMC.Api reference src/OctopusUMC.Core

# OA（相同结构）
mkdir -p OctopusOA/src OctopusOA/tests OctopusOA/web
dotnet new webapi -n OctopusOA.Api  -o OctopusOA/src/OctopusOA.Api
dotnet new xunit  -n OctopusOA.Api.Tests -o OctopusOA/tests/OctopusOA.Api.Tests

# 前端：基于 pure-admin-thin 克隆，不用 create vite
# UMC.Web
cd OctopusUMC/web
git clone https://github.com/pure-admin/pure-admin-thin OctopusUMC.Web --depth=1
cd OctopusUMC.Web && rm -rf .git && npm install

# OA.Web
cd ../../OctopusOA/web
git clone https://github.com/pure-admin/pure-admin-thin OctopusOA.Web --depth=1
cd OctopusOA.Web && rm -rf .git && npm install
```

克隆后初始化：
1. 修改 `package.json` 的 `name`：`octopus-umc-web` / `octopus-oa-web`
2. 修改 `public/platform-config.json`：`Title` 改为系统名称
3. 修改 `src/config/index.ts`：配置后端 API BaseURL

**验收**：四项目均能启动，`dotnet test` 0 失败，`npm run dev` 显示 pure-admin 默认界面。

---

### Phase 1：OIDC 身份核心

**目标**：UMC 成为可用的 OIDC 身份提供商，用户能注册/登录。

**TDD 顺序**：先写 AccountController 测试 → 写 UseCase → 写 Repository → 写 Controller

#### 1.1 领域实体（Core）

```csharp
// src/OctopusUMC.Core/Domain/Entities/
User     { UserId, UserName, NickName, Email, PasswordHash, PhoneNumber, Sex, Avatar, Status, CreateTime }
Role     { RoleId, RoleName, RoleKey, RoleSort, DataScope, Status, CreateTime }
Dept     { DeptId, ParentId, DeptName, OrderNum, Status, CreateTime }
Post     { PostId, PostName, PostCode, PostSort, Status, CreateTime }  // 职位
Menu     { MenuId, ParentId, MenuName, MenuType, Path, Component, Permission, Icon, OrderNum, Status }
Dict     { DictId/DictCode, DictType, DictLabel, DictValue, DictSort, Status }

// 多对多关联表（支持多公司/跨部门场景）
UserDept { UserId, DeptId, PostId?, IsPrimary }  // 一人可同时属于多家公司的多个部门
UserRole { UserId, RoleId }
RoleMenu { RoleId, MenuId }
RoleDept { RoleId, DeptId }                      // 角色数据权限范围（自定义部门）
```

> **多公司数据模型说明**：`UserDept` 的 `IsPrimary` 字段区分主部门与兼职部门。`?deptId` 数据权限查询基于 `UserDept` 关联表（而非 `User` 本身的单一 `DeptId` 字段），因此一名员工能同时出现在多家公司的部门查询结果中。详见 `DECISIONS.md` 关联表设计决策。

#### 1.2 核心 NuGet 包

```bash
# Infrastructure
dotnet add package Pomelo.EntityFrameworkCore.MySql
dotnet add package OpenIddict.EntityFrameworkCore
dotnet add package BCrypt.Net-Next
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File

# Api
dotnet add package OpenIddict.AspNetCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package AspNetCoreRateLimit
dotnet add package Swashbuckle.AspNetCore
```

#### 1.3 OpenIddict 配置

```csharp
builder.Services.AddOpenIddict()
    .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>())
    .AddServer(o =>
    {
        o.SetAuthorizationEndpointUris("/connect/authorize")
         .SetTokenEndpointUris("/connect/token")
         .SetUserinfoEndpointUris("/connect/userinfo")
         .SetLogoutEndpointUris("/connect/logout");

        o.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        o.AllowClientCredentialsFlow();
        o.AllowRefreshTokenFlow();

        // 生产环境替换为真实证书
        o.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();

        o.UseAspNetCore()
         .EnableAuthorizationEndpointPassthrough()
         .EnableTokenEndpointPassthrough()
         .EnableLogoutEndpointPassthrough()
         .EnableUserinfoEndpointPassthrough();
    })
    .AddValidation(o => { o.UseLocalServer(); o.UseAspNetCore(); });
```

#### 1.4 账户接口（Cookie 认证）

```
POST /api/account/register   → 注册，BCrypt 哈希密码，返回 201
POST /api/account/login      → 登录，颁发 HttpOnly Cookie（非 JWT）
POST /api/account/logout     → 登出，清除 Cookie
GET  /api/account/me         → 当前用户信息
```

#### 1.5 中间件注册顺序（严格遵守）

```csharp
app.UseCors("OctopusPolicy");      // 必须第一
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

#### 1.6 UMC.Web 前端（登录页）

pure-admin-thin 已内置 Element Plus、Pinia、Vue Router、Axios，**不需要单独安装**。

额外安装：
```bash
# 仅需补充测试库
npm install -D vitest @vue/test-utils jsdom

# OA.Web 额外需要 OIDC 客户端
npm install oidc-client-ts
```

按 pure-admin 约定开发：
- 登录页：覆盖 `src/views/login/` 下的页面，对接 `/api/account/login`
- API 层：`src/api/system/user.ts`、`src/api/account.ts` 等
- Store：复用 `src/store/modules/user.ts`，补充 `permissions`、`menus` 字段
- 动态路由：后端 `/api/system/menu/tree` 返回路由表，前端调用 pure-admin 的 `addPathMatch` 注册

**验收**：注册 → 登录 → Cookie 会话 → `/api/account/me` 返回用户信息。

---

### Phase 2：SSO 与单点登出

**目标**：OA 通过 UMC OIDC 登录，退出时同步注销 UMC 会话。

#### 2.1 注册 OA 客户端（UMC 种子数据）

```csharp
await manager.CreateAsync(new OpenIddictApplicationDescriptor
{
    ClientId = "octopus-oa-web",
    ClientType = OpenIddictConstants.ClientTypes.Public,
    DisplayName = "OctopusOA Web",
    RedirectUris          = { new Uri("http://localhost:5174/callback") },
    PostLogoutRedirectUris = { new Uri("http://localhost:5174") },
    // Back-Channel Logout 端点（OA 提供，UMC 调用）
    // 在 Permissions 中注册 BackChannelLogout
    Permissions = { /* 全部必要权限，参见 api-conventions.md */ }
});
```

#### 2.2 Back-Channel Logout（单点登出关键）

**流程**：
```
用户在 OA.Web 点击退出
    │
    ▼
OA.Web: userManager.signoutRedirect()
    │
    ▼
浏览器跳转 UMC /connect/logout
    │
    ▼
UMC 清除自身 Cookie 会话
UMC 向 OA.Api 发 POST /backchannel-logout（携带 logout_token）
    │
    ▼
OA.Api 验证 logout_token，撤销对应用户的 Token/会话
    │
    ▼
重定向到 post_logout_redirect_uri（OA 首页）
```

**OA.Api 实现**：
```csharp
[HttpPost("/backchannel-logout")]
[AllowAnonymous]
public async Task<IActionResult> BackChannelLogout()
{
    // 从 Form 读取 logout_token
    // 验证 token 签名和 iss/aud
    // 撤销对应 sub 的所有活跃 Token
    // 清除服务端会话（如 Redis）
    return Ok();
}
```

#### 2.3 OA.Api JWT 验证

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = "https://localhost:7001/";
        o.Audience = "octopus-oa-web";
        o.RequireHttpsMetadata = false; // 仅开发
    });
```

#### 2.4 OA.Web OIDC 客户端

```typescript
// src/services/oidcService.ts
export const userManager = new UserManager({
  authority: 'https://localhost:7001',
  client_id: 'octopus-oa-web',
  redirect_uri: 'http://localhost:5174/callback',
  post_logout_redirect_uri: 'http://localhost:5174',
  response_type: 'code',
  scope: 'openid profile email roles offline_access',
  userStore: new WebStorageStateStore({ store: window.localStorage }),
})
```

**验收**：
- OA 登录 → 跳转 UMC 登录页 → 跳回 OA 显示用户名
- OA 退出 → UMC Cookie 会话被清除 → 再次访问 UMC 需重新登录

---

### Phase 3：权限体系（优先于用户管理 UI）

**目标**：搭建完整的 RBAC + 数据权限骨架，所有后续功能基于此构建。

#### 3.1 权限模型

```
用户 (User)
 ├─ 角色 (Role)           → 菜单权限（功能范围）
 │   └─ 菜单 (Menu)       → 按钮权限标识（操作粒度）
 └─ 数据权限范围
     ├─ 全部数据
     ├─ 本部门及子部门数据
     ├─ 本部门数据
     ├─ 仅本人数据
     └─ 自定义部门数据
```

#### 3.2 菜单权限接口

```
GET  /api/system/menu/tree         → 当前用户可见菜单树
GET  /api/system/menu/list         → 菜单列表（管理员）
POST /api/system/menu              → 新增菜单
PUT  /api/system/menu/{id}         → 修改菜单
DELETE /api/system/menu/{id}       → 删除菜单
GET  /api/system/menu/role/{roleId} → 角色已绑定菜单
```

#### 3.3 角色管理接口

```
GET    /api/system/role/list       → 角色列表（分页）
POST   /api/system/role            → 新增角色
PUT    /api/system/role            → 修改角色
DELETE /api/system/role/{ids}      → 删除角色
POST   /api/system/role/menu       → 角色绑定菜单权限
POST   /api/system/role/dept       → 角色绑定数据权限（部门范围）
GET    /api/system/role/{id}/users → 角色下的用户列表
```

#### 3.4 权限校验实现

```csharp
// 菜单/按钮权限标识校验（如 "system:user:add"）
[HasPermission("system:user:add")]
public async Task<IActionResult> CreateUser(...)

// 数据权限过滤（在 Repository 层注入 SQL 过滤条件）
// 根据当前用户的数据权限范围，自动追加 WHERE 条件
```

#### 3.5 前端权限集成

```typescript
// store/auth.ts
interface AuthState {
  user: UserInfo | null
  permissions: string[]   // ["system:user:add", "system:role:edit", ...]
  roles: string[]         // ["admin", "editor"]
  menus: MenuTree[]       // 当前用户可见的菜单树
}

// 路由动态生成：根据 menus 动态注册路由
// 按钮显示控制：v-permission="'system:user:add'"
```

**验收**：
- 不同角色登录后看到不同菜单
- 无权限用户访问受保护接口返回 403
- 数据权限过滤生效（不同部门用户看到不同数据范围）

---

### Phase 4：用户管理完整后台

**目标**：实现完整的 RBAC 管理后台 UI，达到市面主流后台管理系统水准。

#### 4.1 用户管理

```
GET    /api/system/user/list       → 用户列表（分页 + 条件筛选）
GET    /api/system/user/{id}       → 用户详情
POST   /api/system/user            → 新增用户
PUT    /api/system/user            → 修改用户
DELETE /api/system/user/{ids}      → 删除用户（批量）
PUT    /api/system/user/status     → 启用/禁用用户
PUT    /api/system/user/resetPwd   → 重置密码
GET    /api/system/user/authRole/{userId} → 用户已绑定角色
PUT    /api/system/user/authRole   → 用户绑定角色

GET    /api/system/user/profile    → 当前用户个人资料
PUT    /api/system/user/profile    → 修改个人资料
PUT    /api/system/user/profile/avatar  → 更新头像
PUT    /api/system/user/profile/updatePwd → 修改密码

GET    /api/system/user/export     → 导出用户列表（Excel）
POST   /api/system/user/import     → 批量导入用户
GET    /api/system/user/importTemplate → 下载导入模板
```

#### 4.2 机构管理

```
GET    /api/system/dept/tree       → 部门树（当前用户数据权限范围内）
GET    /api/system/dept/list       → 部门列表
POST   /api/system/dept            → 新增部门
PUT    /api/system/dept            → 修改部门
DELETE /api/system/dept/{id}       → 删除部门
GET    /api/system/dept/{id}       → 部门详情
GET    /api/system/dept/exclude/{id} → 排除指定节点的树（编辑时用）
```

#### 4.3 职位管理

```
GET    /api/system/post/list       → 职位列表
POST   /api/system/post            → 新增职位
PUT    /api/system/post            → 修改职位
DELETE /api/system/post/{ids}      → 删除职位
GET    /api/system/post/{id}       → 职位详情
```

#### 4.4 字典管理

```
# 字典类型
GET    /api/system/dict/type/list       → 列表
POST   /api/system/dict/type            → 新增
PUT    /api/system/dict/type            → 修改
DELETE /api/system/dict/type/{ids}      → 删除

# 字典数据
GET    /api/system/dict/data/type/{dictType} → 按类型查字典数据（前端下拉用）
GET    /api/system/dict/data/list       → 列表
POST   /api/system/dict/data            → 新增
PUT    /api/system/dict/data            → 修改
DELETE /api/system/dict/data/{ids}      → 删除
```

#### 4.5 前端页面清单（pure-admin 目录约定）

```
src/views/
├── welcome/                  # pure-admin 默认首页，改造为工作台
├── dashboard/
│   ├── workbench/index.vue   # 工作台（快捷入口、待办、公告）
│   ├── analysis/index.vue    # 数据分析看板
│   └── statistics/index.vue  # 统计报表
├── system/
│   ├── user/
│   │   ├── index.vue         # 用户列表（搜索/分页/导入导出）
│   │   ├── form.vue          # 新增/编辑抽屉
│   │   └── role.vue          # 分配角色弹窗
│   ├── dept/
│   │   └── index.vue         # 部门树形管理
│   ├── role/
│   │   ├── index.vue         # 角色列表
│   │   ├── menu.vue          # 菜单权限树（ElTree）
│   │   └── scope.vue         # 数据权限配置
│   ├── menu/
│   │   └── index.vue         # 菜单管理（树形表格）
│   ├── post/
│   │   └── index.vue         # 职位管理
│   └── dict/
│       ├── index.vue         # 字典类型列表
│       └── data.vue          # 字典数据管理（右侧面板）
└── profile/
    └── index.vue             # 个人中心（资料/修改密码/操作日志 Tab）

src/api/                      # 对应后端接口，按模块拆分
├── account.ts                # /api/account/*
├── system/
│   ├── user.ts               # /api/system/user/*
│   ├── role.ts               # /api/system/role/*
│   ├── menu.ts               # /api/system/menu/*
│   ├── dept.ts               # /api/system/dept/*
│   ├── post.ts               # /api/system/post/*
│   └── dict.ts               # /api/system/dict/*
└── monitor/
    ├── online.ts
    ├── operlog.ts
    └── server.ts
```

---

### Phase 5：扩展功能

各模块按独立特性开发，互不阻塞，可并行推进。

#### 5.1 访问日志 & 操作日志

```
# 访问日志
POST /api/monitor/logininfor/list   → 查询列表
DELETE /api/monitor/logininfor/{ids} → 删除
DELETE /api/monitor/logininfor/clean → 清空

# 操作日志（AOP 拦截器自动写入）
GET  /api/monitor/operlog/list      → 查询列表
DELETE /api/monitor/operlog/{ids}   → 删除
```

AOP 实现：使用 ASP.NET Core Action Filter 拦截带 `[Log("操作描述")]` 标注的接口，异步写日志。

#### 5.2 在线用户（SignalR）

```csharp
// OnlineUserHub：连接时记录 / 断开时移除
// 强制下线：服务端主动发送 "ForceLogout" 事件
```

```
GET    /api/monitor/online/list     → 在线用户列表
DELETE /api/monitor/online/{token}  → 强制下线
```

#### 5.3 公告管理（SignalR 实时推送）

```
GET    /api/system/notice/list      → 公告列表
POST   /api/system/notice           → 发布公告（发布后 Hub 推送给所有在线用户）
PUT    /api/system/notice           → 修改公告
DELETE /api/system/notice/{ids}     → 删除公告
```

#### 5.4 文件管理

策略模式，实现 `IStorageProvider` 接口：
- `LocalStorageProvider`
- `AliyunOssStorageProvider`
- `TencentCosStorageProvider`

```
POST   /api/common/upload           → 上传文件
POST   /api/common/uploadOss        → 上传到 OSS（返回 URL）
GET    /api/system/oss/list         → 文件列表
DELETE /api/system/oss/{ids}        → 删除文件
```

#### 5.5 任务调度（Sundial）

```bash
dotnet add package Sundial
```

```
GET    /api/monitor/job/list        → 任务列表
POST   /api/monitor/job             → 新增任务
PUT    /api/monitor/job             → 修改任务
DELETE /api/monitor/job/{jobId}     → 删除任务
PUT    /api/monitor/job/run         → 立即执行
PUT    /api/monitor/job/pause       → 暂停
PUT    /api/monitor/job/resume      → 恢复
GET    /api/monitor/jobLog/list     → 执行日志
```

#### 5.6 服务监控

```
GET    /api/monitor/server          → CPU、内存、磁盘、JVM（.NET Runtime）信息
```

使用 `System.Diagnostics.Process` + `PerformanceCounter`（Windows）或 `/proc` 文件解析（Linux）。

#### 5.7 系统配置

```
GET    /api/system/config/list      → 配置列表
POST   /api/system/config           → 新增配置
PUT    /api/system/config           → 修改配置
DELETE /api/system/config/{ids}     → 删除配置
GET    /api/system/config/key/{configKey} → 按键查配置值（带缓存）
PUT    /api/system/config/refreshCache → 刷新缓存
```

#### 5.8 导入导出

```bash
dotnet add package Magicodes.IE.Excel
dotnet add package Magicodes.IE.Pdf
```

通用模式：`GET /api/{module}/export` + `POST /api/{module}/import`

#### 5.9 限流控制

```bash
dotnet add package AspNetCoreRateLimit
```

在 `appsettings.json` 中配置：按 IP / ClientId / 接口路径分别设置速率。

#### 5.10 开放授权（微信等 OAuth 2.0）

在 OpenIddict 中注册外部身份提供商，处理微信 OAuth 回调，创建或绑定本地账户。

---

## TDD 规范

### 后端测试结构

```csharp
// 命名：被测方法_场景_预期结果
[Fact]
public async Task RegisterUser_WithDuplicateEmail_Returns409Conflict() { }

[Fact]
public async Task Login_WithCorrectCredentials_SetsCookieAndReturns200() { }

[Fact]
public async Task GetMenuTree_WithEditorRole_ExcludesAdminMenus() { }

[Fact]
public async Task BackChannelLogout_WithValidLogoutToken_RevokesOASession() { }
```

**禁止 Mock 数据库**：集成测试连真实 MySQL（用 Docker 起隔离测试库）。

### 前端测试覆盖

- `authService.ts`：mock axios，验证请求参数
- `auth.ts` store：验证登录/登出后状态变化
- `Login.vue`：表单校验、提交逻辑
- `Callback.vue`：OIDC 回调处理和路由跳转

### 关键路径必测场景

| 场景 | 测试类型 |
|---|---|
| 注册 → 登录 → 获取用户信息 | API 集成测试 |
| OIDC 授权码流完整握手 | API 集成测试 |
| Back-Channel Logout 同步注销 | API 集成测试 |
| 无权限用户访问受保护接口 | API 集成测试 |
| 数据权限过滤（仅本人数据） | 仓储集成测试 |
| 菜单权限按角色过滤 | 单元测试 |

---

## 编码规范

1. **Controller 轻量**：方法体 ≤ 10 行，只调用 UseCase，返回统一响应格式
2. **统一响应**：`{ "code": 200, "msg": "操作成功", "data": {...} }`
3. **密码安全**：`BCrypt.HashPassword`，work factor ≥ 12
4. **Cookie 安全**：`HttpOnly = true`，`Secure = true`，生产必须
5. **PKCE 强制**：所有浏览器端 Public Client 必须使用 PKCE
6. **CORS 白名单**：禁止 `AllowAnyOrigin` + `AllowCredentials` 组合
7. **迁移手动执行**：生产环境禁止启动时自动迁移
8. **敏感配置**：连接字符串等放 `appsettings.Development.json` 或环境变量
9. **前端 HTTP 工具函数**：`src/utils/http.ts` 必须实现 `unwrap<T>()` 检查业务码，并在 401 拦截器中对 `/account/me`（bootstrap 探测）和已在 `/login` 时跳过重定向，防止死循环：
   ```typescript
   function unwrap<T>(res: AxiosResponse<ApiResponse<T>>): T {
     if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
     return res.data.data
   }
   // 401 拦截器：
   const isSessionProbe = url.includes('/account/me')
   if (!alreadyOnLogin && !isSessionProbe) window.location.href = '/login'
   ```

---

## 常见坑点速查

| 问题 | 原因 | 解决 |
|---|---|---|
| CORS 预检 401 | `UseCors` 在 `UseAuthentication` 之后 | 调整中间件顺序 |
| OIDC discovery 404 | UMC.Api 不可从浏览器直接访问 | 确认 UMC 地址是浏览器可达的 |
| Token audience 不匹配 | OA `Audience` ≠ UMC `client_id` | 两端必须完全一致 |
| redirect_uri_mismatch | URL 末尾斜杠、大小写不一致 | 精确匹配，包括协议和端口 |
| Back-Channel Logout 未触发 | OpenIddict 未注册 logout endpoint | 确认权限中包含 `Endpoints.Logout` |
| 菜单权限不生效 | 前端路由未根据 menus 动态注册 | 登录后重新生成动态路由 |
| 数据权限未过滤 | Repository 未注入当前用户上下文 | 通过 `ICurrentUser` 服务获取当前用户信息 |
| EF 迁移找不到 DbContext | 缺少 `IDesignTimeDbContextFactory` | 在 Infrastructure 中实现工厂类 |
| MySQL varchar 索引过长 | OpenIddict 字段超过 767 bytes | MySQL 8.0 默认支持，确认 `innodb_large_prefix = ON` |
| 登录弹窗成功但 `/me` 仍 401 | 后端返回 HTTP 200 + `{"code":401}` 业务错误码，`post()` 未抛出异常 | `http.ts` 中用 `unwrap()` 检查 `res.data.code !== 200` 并 throw |
| 登录页 `/account/me` 死循环 401 | 401 拦截器无条件跳转 `/login`，触发全页刷新 → bootstrap 再次调 `fetchMe()` | 跳转前判断 `url.includes('/account/me')` 或 `pathname === '/login'` 则跳过 |
| SQLite 种子数据未更新 | 旧 `octopus_umc.db` 文件预先存在，不会重建 | 删除 `src/OctopusUMC.Api/octopus_umc.db` 后重启 API |
| Aspire DCP TLS EOF | 机器有 `HTTPS_PROXY`，.NET HttpClient 将 `[::1]` 路由到代理 | launchSettings.json 中设 `HTTPS_PROXY=""` `HTTP_PROXY=""` |
| Aspire `http endpoint already exists` | `AddProject<T>` 已自动注册 http 端点 | 用 `.WithEndpoint("http", e => e.Port = N)` 覆盖而非 `WithHttpEndpoint` |

---

## 与 Claude 协作方式

提问时注明阶段 + 组件 + 具体任务：

```
"Phase 3，UMC.Api：用 TDD 实现角色绑定菜单权限的接口"
"Phase 4，UMC.Web：实现用户管理页面的批量导入功能"
"Phase 2，OA.Api：实现 Back-Channel Logout 端点"
"Phase 5，UMC.Api：集成 Sundial 任务调度，添加定时清理过期日志的任务"
```
