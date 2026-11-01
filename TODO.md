# OctopusUMC & OctopusOA 实现进度清单

> 六步闸门制：每步全部通过后方可进入下一步。

---

## 全平台进度快照（2026-05-11）

**9 个后端子系统 + 自愈 Schema + Aspire 一键编排，全部跑通**

| 系统 | 端口 | 测试 | 状态 |
|---|---|---|---|
| UMC（OIDC IdP） | 5001 | 79+ | ✅ 含 P16 新增 octopus-hrm-web / octopus-oms-web / octopus-customer-portal / octopus-supplier-portal 客户端 |
| OA（含 P17 BPM 扩展） | 5002 | 95 | ✅ SkipCondition / CcUsersJson / ApprovalDelegate 上线 |
| PLM | 5003 | 27 | ✅ 含服装行业字段（StyleCode / SeasonCode / Series） |
| CRM | 5004 | 31 | ✅ |
| WMS（P11 多货主 / FEFO） | 5005 | 23+ | ✅ |
| MES（P12 OEE / 工序报工） | 5006 | 20+ | ✅ |
| FIN（P13 复式记账） | 5007 | — | ✅ AR/AP webhook 已挂上 CRM/HRM |
| HRM（P14） | 5008 | 11 | ✅ 算薪 → FIN 凭证联动 |
| OMS（P15 订单中台） | 5009 | 10 | ✅ 6 渠道种子（淘宝/京东/拼多多/抖音/Shopify/Amazon） |

**全平台测试基线**：≥ 391 个（含 9 个系统的集成测试）。

### 已完成的 P 系列里程碑

- ✅ P5（H5 移动端）— 已完成：UMC.H5（5179）+ OA.H5（5180）独立 Vite 项目，含完整功能页
- ✅ P6（跨系统 Webhook 链）— OA → CRM → WMS → MES 全打通
- ✅ P7（OA SLA 催办）— `WorkflowNode.SlaHours` + `ApprovalSlaWorker` 30 分钟扫描
- ✅ P8（限流 + 操作日志 AOP）— `[OperationLog]` + RateLimiter 全平台
- ✅ P9（SignalR 统一通知中心）— `/hubs/notification` + `POST /api/internal/notify`
- ✅ P10（PLM 升级）— 跨系统标准化 API + 类目模型版本
- ✅ P11（WMS 多货主 / 库区 / FEFO / 三段式调拨）
- ✅ P12（MES OEE / 工序报工 / 服装排程）
- ✅ P13（OctopusFIN 财务）
- ✅ P14（OctopusHRM 人力资源）
- ✅ P15（OctopusOMS 订单中台）
- ✅ P16（UMC 新增 4 个 OIDC 客户端 + customer/supplier 角色，门户前端待建）
- ✅ P17（OA BPM 扩展：条件分支 / 转交 / 抄送）

### 2026-05-11 修复

- ✅ **SQLite Schema 模型驱动自愈**（DECISIONS ADR-007）— OA / PLM / MES 启动时自动 ALTER TABLE ADD COLUMN 补齐缺失的列，CREATE TABLE IF NOT EXISTS 补齐缺失的表；解决了"实体加字段后旧 db 启动崩溃 → 进而拖垮 Aspire 内其它子系统"的连锁故障。
- ✅ **UMC 健康端点拆分**：`/health/live` vs `/health/ready`（Seeder 完成前 503，避免被认作"已就绪"导致下游误连）。

### 待落地（按优先级）

- ✅ P5 H5 — OA H5 全功能（首页/审批/考勤/公告/通讯录/会议室/个人中心）+ UMC H5 完成
- ⬜ P16 门户前端 — Customer / Supplier Portal Vite 项目
- ⬜ P18+ MySQL 迁移路径（EF Migrations 启用、自愈守卫加 `Sqlite` 判断）
- ⬜ Redis 缓存 / 真实电商平台对接 / BPM 拖拽设计器

---

## ✅ Step 1：前端样板（仅 UI，Mock 数据）
- [x] 安装配置 MSW（Mock Service Worker）
- [x] UMC.Web：登录页、工作台、用户/机构/职位/菜单/角色/字典/个人中心 9 个页面
- [x] OA.Web：登录回调页、首页、个人中心 3 个页面
- [x] 所有页面 Mock 数据正确渲染，无控制台报错
- [x] `npx tsc --noEmit` 零类型错误
- **Gate 1 ✅ 已通过**

## ✅ Step 2：后端骨架（SQLite 持久化，不连 MySQL）
- [x] 数据库：SQLite（开发用文件模式 `octopus_umc.db`，测试用内存模式 `:memory:`）
- [x] UMC.Api：EF Core + DbSeeder 种子数据（6用户/11部门含两家公司/3角色/菜单树/4职位/字典等）
- [x] UMC.Api：多对多关联表（`sys_user_dept`/`sys_user_role`/`sys_role_menu`/`sys_role_dept`）
- [x] UMC.Api：用户/机构/职位/菜单/角色/字典全部 CRUD 接口
- [x] UMC.Api：账户登录/登出/me 接口（BCrypt 校验 + Cookie 认证）
- [x] UMC.Api：`GET /api/system/user/{userId}/depts` 查询用户跨公司任职记录
- [x] OA.Api：/api/me、/api/users/sync、/api/auth/backchannel-logout 接口
- [x] Swagger 可访问，所有接口调用返回正常数据
- [x] `dotnet test` UMC **79/79** 通过，OA 6/6 通过
- **Gate 2 ✅ 已通过（2026-04-05）**

**测试覆盖（UMC 79 个）：**

| 测试文件 | 测试数 | 覆盖重点 |
|---|---|---|
| `AccountControllerTests.cs` | 6 | 登录/登出/权限 |
| `UserControllerTests.cs` | 9 | 用户 CRUD + 状态/密码 |
| `DeptControllerTests.cs` | 7 | 部门 CRUD + 树形 + 删除保护 |
| `PostControllerTests.cs` | 10 | 职位 CRUD + 编码唯一性 + 批量删除 |
| `RoleControllerTests.cs` | 13 | 角色 CRUD + 绑定菜单 + 五种数据权限范围 |
| `UserDataPermissionTests.cs` | 16 | 按部门过滤 + 角色绑定 + 菜单权限差异 |
| `BusinessFlowTests.cs` | 4 | Flow 1–4 端到端链路（带可读日志） |
| `MultiCompanyTests.cs` | 6 | Flow 5 多公司：跨公司兼职 + `?deptId` 数据权限 + 数据隔离 |

**多公司场景测试要求（`MultiCompanyTests.cs`）：**
- `?deptId=3`（A公司技术部）→ 返回 [zhangsan, lisi]，zhaoliu 不出现
- `?deptId=10`（B公司技术部）→ 返回 [zhangsan, zhaoliu]，lisi 不出现
- `?deptId=11`（B公司市场部）→ 返回 []（空）
- zhangsan 在 B 公司查询结果中的 `deptId` 仍显示主部门（章鱼技术部=3），不变成 10
- `GET /user/2/depts` 返回两条记录：DeptId=3(IsPrimary=true) + DeptId=10(IsPrimary=false)
- B 公司新员工不出现在 A 公司查询（数据隔离）

## ✅ Step 3：接口层确认（Swagger + DTO 对齐）
- [x] Swagger 可正常访问，所有接口有 XML 文档注释（`GenerateDocumentationFile` 已启用）
- [x] 后端 DTO 与前端 TypeScript 类型零差异（`npx tsc --noEmit` 零错误）
- [x] 新增缺失的前端类型（LoginRequest/LoginResponse/UpdateDictTypeRequest 等共 9 项）
- [x] 前端 http.ts 修正为 Cookie 认证（`withCredentials: true`，去除 Bearer Token 头）
- [x] 用户 store 重写为基于 `LoginResponse` 的 Cookie 认证模式
- [x] Mock handler 响应格式对齐 `LoginResponse`（去除 token 字段）
- [x] 创建全部 API 请求函数文件（account / user / role / menu / dept / post / dict / notice / online / operlog）
- [x] 73 → 79 测试全部通过
- **Gate 3 ✅ 已通过（2026-04-05）**

## ✅ Step 4：前后端联调（替换 Mock，接真实接口 + MySQL）

**基础配置（已完成）：**
- [x] `vite.config.ts`：Vite proxy 将 `/api/*` 代理到 `http://localhost:5001`
- [x] `.env.development`：`VITE_ENABLE_MOCK=false`（关闭 MSW，使用真实后端）
- [x] `main.ts`：条件启动 MSW + `fetchMe()` 在路由前恢复 Cookie 会话
- [x] 修复全部接口 URL 不一致：7 处 `/list` 后缀缺失、dict 编辑改用 PUT
- [x] 后端补齐缺失端点：`DELETE /monitor/operlog/{ids}`、`DELETE /monitor/logininfor/{ids}`
- [x] 后端 `OnlineUserResponse` 补充 `tokenId`、`deptName` 字段
- [x] Mock handlers 同步更新为 `/list` URL（mock 模式可随时切回）
- [x] 用户表单：部门多选（el-tree-select multiple）、职位多选（el-select 标签模式）
- [x] 后端 DTO：`DeptId/PostId` → `DeptIds/PostIds` 数组，支持多部门多职位

**联调验收（待人工测试）：**
- [ ] 启动后端 + 前端，登录 admin/Admin@123 成功
- [ ] 逐模块联调：用户 → 机构 → 职位 → 菜单 → 角色 → 字典
- [ ] 执行 EF 迁移接入 MySQL（生产就绪时）
- **Gate 4 通过后进入 Step 5** ✋

## ✅ Step 5：SSO 打通与用户数据同步
- [x] OpenIddict 配置 + OA 客户端注册（octopus-oa-web, Public, PKCE）
- [x] OIDC 端点：/connect/authorize, /connect/token, /connect/userinfo, /connect/logout
- [x] OIDC 发现文档：/.well-known/openid-configuration
- [x] OA.Web OIDC 客户端配置（oidc-client-ts UserManager）
- [x] OA 首次 SSO 登录自动创建 SyncUsers 记录（MeController auto-register）
- [x] UMC 用户变更 Webhook 推送（UserSyncService + HMAC-SHA256 签名）
- [x] OA 接收同步并验证签名（X-Sync-Signature header）
- [x] Back-Channel Logout 联动注销（BackchannelLogoutController）
- [x] access_token 自动静默刷新（automaticSilentRenew）
- [x] OA.Api JWT Bearer 真实验证（Authority = UMC）
- **Gate 5 ✅ 已通过（2026-04-06）**

**测试覆盖：**
- UMC：79/79 通过
- OA：6/6 通过（含 HMAC 签名验证失败测试）

**验证命令：**
```bash
# OIDC 发现文档
curl http://localhost:5001/.well-known/openid-configuration

# SSO 流程
# 1. 打开 http://localhost:5174 → 点击登录
# 2. 跳转 UMC 登录页 → admin / Admin@123
# 3. 登录后自动跳回 OA 显示用户信息
```

## ✅ OA 审批流引擎（可定制审批流）
- [x] 流程模板 + 节点（WorkflowTemplate / WorkflowNode）
- [x] 审批实例 + 记录（Approval / ApprovalRecord）
- [x] ApprovalService 引擎：提交 / 审批 / 驳回 / 撤回 / 审批人解析
- [x] 4 种审批人类型：role / user / dept_leader / self_select
- [x] FormSchema JSON 动态表单定义
- [x] 种子模板：请假审批（2 节点）、报销审批（3 节点）
- [x] 种子审批数据：pending / approved / rejected 各一条
- [x] 模板管理 API：CRUD + 节点整体替换 + 编码自动生成（WF-yyyyMMdd-seq）
- [x] 审批操作 API：提交 / 我的 / 待审 / 详情 / 通过 / 驳回 / 撤回 / 全部
- [x] **前端页面**：发起申请（动态表单）/ 我的申请 / 待我审批 / 流程模板管理 / 全部审批
- [x] **WorkflowGraph 流程图组件**：纯 CSS 节点卡片 + 箭头 + 状态色（替代 el-steps）
- [x] **DynamicForm 动态表单组件**：根据 FormSchema JSON 渲染 select/date/number/textarea

## ✅ OA 用户权限体系
- [x] OaUserController：用户列表 / 角色分配 / 角色列表
- [x] 3 种 OA 角色：oa_admin / oa_user / oa_manager
- [x] useOaUserStore：从 /api/me 获取 oaRoles，提供 isOaAdmin / hasOaRole()
- [x] Layout 侧边栏权限过滤：审批管理 + 职员管理 + 用户管理 仅 oa_admin 可见
- [x] 用户管理页面：查看同步用户 + 分配 OA 角色（多选标签）

## ✅ OA 职员管理（入职档案）
- [x] Employee 主表 + 4 个子表（教育/工作/家庭/紧急联系人）
- [x] 五状态流转：temp → pending → active → resigned / rejected
- [x] EmployeeService：创建 / H5 提交 / 确认入职 / 拒绝 / 离职 / 删除
- [x] EmployeeController：HR 端 8 个接口
- [x] H5OnboardController：H5 端 2 个接口（AllowAnonymous + token 验证）
- [x] 种子数据：3 条档案（temp / pending / rejected）
- [x] **HR 端前端**：职员列表（搜索/筛选/新建弹窗/H5 链接复制）+ 档案详情（全量信息 + 状态操作）
- [x] **H5 端前端**：5 步分步表单（个人信息→教育→工作→家庭&紧急→银行卡），移动端适配
- [x] H5 独立路由：/h5/onboard/{token}（无需登录）

## ✅ SSO 登录修复
- [x] UMC 后端：/connect/authorize 未登录时重定向到 UMC 前端登录页（非 API）
- [x] UMC 前端：登录成功后检测 returnUrl 参数，以 /connect/ 开头则跳回 UMC API 完成 OIDC 流程

## ✅ OA 常用功能五步走（2026-04-18 ~ 2026-04-19）

详细方案见项目根目录 [`ROADMAP.md`](./ROADMAP.md)。

### ✅ Step 1：通讯录
- [x] OaDept / OaUserDept 本地缓存（11 部门 + 4 关联）
- [x] UMC DeptSyncService（HMAC 签名推送），DeptController CRUD 后触发
- [x] ContactController：部门树 / 用户列表（部门筛选+关键字搜索）/ 用户详情
- [x] DeptSyncController 接收推送（HMAC 验证）
- [x] 前端：通讯录主页（左树右卡片 + 详情抽屉 + 拨号/复制）

### ✅ Step 2：公告通知
- [x] OaNotice / OaNoticeRead 镜像表
- [x] UMC NoticeSyncService + NoticeController 增删改后推送
- [x] OA NoticeController：列表/详情（自动已读）/未读数/最新 5 条
- [x] NoticeSyncController 接收推送（HMAC 验证）
- [x] 前端：公告列表 + 详情 + 首页最新公告卡片 + 侧边栏未读徽标

### ✅ Step 3：考勤打卡 + 日历 + 多班次
- [x] OaAttendanceRule（重命名为班次）+ OaAttendance + OaAttendanceFix
- [x] 4 种子班次：标准班 / 早班 / 晚班 / 弹性班 + OaUserShift 关联
- [x] AttendanceService：按用户查班次、打卡状态计算、月度/异常统计、补卡联动
- [x] 补卡流程：审批通过后通过 `ApprovalService.OnApproved` 事件钩子自动更新考勤
- [x] 补卡审批模板（attendance_fix）+ 2 审批节点
- [x] AttendanceController：打卡 / 今日 / 月度 / 规则 / 统计 / 异常 / 班次 CRUD / 用户分配
- [x] 前端：我的考勤（**日历/列表**切换 + 今日打卡卡片）
- [x] 前端：考勤统计（员工+异常 Tab）、默认规则配置、班次管理（CRUD + 员工分配）
- [x] 日历视图：周一起始，6 种状态色，今日高亮，缺勤红背景，已补卡橙边框，点击详情弹窗

### ✅ Step 4：待办中心
- [x] DashboardService：聚合 Approval + Notice + Attendance + Meeting
- [x] DashboardController：`/summary` + `/todos?type=`
- [x] 前端首页重构：
  - 4 张数据卡片（待审批 / 未读公告 / 今日考勤 / 今日会议）
  - 5 个快捷入口（发起申请/打卡/订会议室/通讯录/公告中心）
  - 聚合待办列表（按类型 Tab 切换）

### ✅ Step 5：会议室预订
- [x] OaMeetingRoom + OaMeetingBooking（3 间会议室 + 3 条种子预订）
- [x] MeetingService：CRUD + 冲突检测 + 取消校验 + 日历查询
- [x] MeetingController：11 个接口（含单室日历 + 总览日历 + 今日参与）
- [x] 前端：预订日历（周视图，会议室×7天矩阵，空格一键预订）
- [x] 前端：我的预订（取消功能）+ 会议室管理（CRUD）
- [x] Dashboard 集成：今日会议卡片 + 聚合到待办列表

### 全局增强
- [x] `src/utils/http.ts` 统一 axios 工厂（createAuthedHttp / createPublicHttp）
- [x] 响应拦截器自动将 UTC ISO 时间转为北京时间 `yyyy-MM-dd HH:mm:ss`
- [x] `src/utils/datetime.ts` 提供辅助函数（formatBeijingTime / formatBeijingShort）
- [x] TestAuthHandler 支持 `X-Test-UserId` header 切换测试用户（隔离测试数据）
- [x] 菜单徽标紧贴标题（小型红点）

## ✅ OA 持久化迁移（InMemory → SQLite，2026-04-19）
- [x] NuGet：`Microsoft.EntityFrameworkCore` 8.0.15 + `.Sqlite` + `.Design`
- [x] 新建 `OctopusOA.Api/Persistence/`：Entities.cs / OaDbContext.cs / OaDbSeeder.cs
- [x] 所有 20 个实体迁到 `Persistence` 命名空间，含 JSON ValueConverter
- [x] 7 个 Service 改注入 `OaDbContext`，所有写操作 `SaveChanges()`
- [x] 5 个 Controller（Approval/Employee/H5Onboard/OaUser/Template）改注入 `OaDbContext`
- [x] 3 个 Controller（UserSync/Me/BackchannelLogout）改注入 `OaDbContext`
- [x] DI 生命周期：Singleton → Scoped
- [x] Program.cs：`AddDbContext` + 启动时 `EnsureCreated` + `OaDbSeeder.Seed`
- [x] 补卡联动改造：ApprovalService 的事件订阅替换为 `IServiceProvider.GetRequiredService<AttendanceService>()`
- [x] 删除 `InMemory/` 目录
- [x] OATestFactory 改为共享 SQLite 内存连接（`:memory:;Cache=Shared`）
- [x] `dotnet test` 90/90 通过，无 API 契约变化，前端零修改
- [x] 开发库文件：`octopus_oa.db`（启动自动创建 + 种子）

**OA 测试覆盖（90 个）：**

| 测试文件 | 测试数 | 覆盖重点 |
|---|---|---|
| `UserSyncControllerTests.cs` | 6 | 用户同步 HMAC 签名 + BackchannelLogout + /me |
| `ApprovalFlowTests.cs` | 10 | 4 条 Flow（请假通过/驳回/撤回 + 报销全流程）+ 种子数据验证 |
| `TemplateControllerTests.cs` | 7 | 模板 CRUD + 节点替换 + 删除保护 |
| `EmployeeFlowTests.cs` | 9 | 创建→H5填写→确认入职 / 拒绝 / 删除 / token验证 / 种子数据 |
| `ContactFlowTests.cs` | 11 | 部门树 / 用户筛选 / 关键字搜索 / 部门同步 HMAC |
| `NoticeFlowTests.cs` | 12 | 公告列表 / 已读追踪 / 最新 / 同步 HMAC / 删除动作 |
| `AttendanceFlowTests.cs` | 16 | 打卡/今日/月度/规则/统计/异常/班次CRUD/用户分配/补卡联动 |
| `DashboardTests.cs` | 6 | summary 聚合 / todos 按类型过滤 / 跨用户隔离 |
| `MeetingFlowTests.cs` | 12 | 会议室 CRUD / 预订/冲突 409/时间校验/取消权限/日历 |

**运行测试：**
```bash
cd OctopusOA
export PATH="$PATH:/usr/local/share/dotnet"
# 全量
dotnet test
# 预期：已通过! - 失败: 0，通过: 90

# 各 Flow 可读日志
dotnet test --filter "Flow" --logger "console;verbosity=detailed"
```

## ⬜ Step 6：功能验收
- [ ] UMC 权限三层（菜单/按钮/数据）全部验收
- [ ] UMC 各模块 CRUD 验收
- [ ] 多公司场景验收：A/B 两家公司数据互不干扰
- [x] OA SSO 完整闭环验收（含登录修复）
- [x] OA 审批流前端页面（发起/我的/待审/模板管理/全部审批）
- [x] OA 职员管理前后端（HR 端 + H5 入职表单）
- [x] OA 用户权限体系（角色过滤 + 用户管理页面）
- [x] OA 常用功能五步走（通讯录 / 公告 / 考勤 / 待办 / 会议室）
- [ ] SyncUsers 缓存一致性验收
- [x] `dotnet test` 全部通过（当前 UMC 79 + OA 90 = **169 总计**）
- [x] `npx tsc --noEmit` 零错误
- [ ] 异常接口返回统一格式，不暴露堆栈

## Phase 0：脚手架搭建
- [x] UMC：创建解决方案（Core / Infrastructure / Api + 三个测试项目）
- [x] OA：创建 Api + Tests 项目
- [x] 初始化 OctopusUMC.Web（Vue 3 + Vite + TS，pure-admin-thin）
- [x] 初始化 OctopusOA.Web（Vue 3 + Vite + TS，pure-admin-thin）
- [x] `dotnet test` 0 失败，四个项目均能启动

## ✅ Phase 1：OIDC 身份核心
- [x] ApplicationDbContext + OpenIddict EF Core 表集成
- [x] 配置 OpenIddict Server（授权码流 + PKCE、刷新令牌）
- [x] AuthorizationController（/connect/authorize, /connect/token, /connect/userinfo, /connect/logout）
- [x] OIDC 发现文档（/.well-known/openid-configuration）
- [x] 开发证书 + DisableTransportSecurityRequirement（HTTP 开发模式）

## ✅ Phase 2：SSO 与单点登出
- [x] OpenIddict 种入 octopus-oa-web 客户端（Public, PKCE）
- [x] OA.Api JWT Bearer 真实验证（Authority = UMC）
- [x] OA.Web oidc-client-ts UserManager 配置
- [x] OA.Web Callback.vue 回调处理
- [x] UMC UserSyncService（HMAC-SHA256 Webhook 推送）
- [x] OA UserSyncController（签名验证）
- [x] OA BackchannelLogoutController（logout_token 解析）
- [x] OA MeController（JWT Claims + 首次登录自动注册）

## Phase 3：权限体系（优先）

> **后端 API + 测试已完成**，前端页面已搭建。HasPermission 特性/中间件待实现。

- [x] [TDD] 菜单权限：树形查询、角色绑定菜单
- [x] [TDD] 角色管理：CRUD、绑定菜单、绑定数据权限
- [x] [TDD] 数据权限：五种范围的 Repository 过滤实现
- [x] [TDD] 权限标识校验：HasPermission 特性 + Cookie Claims（93/93 通过）
- [x] UMC.Web：前端权限集成（动态路由，LoginResponse 返回 permissions）
- [x] UMC.Web：菜单管理页面（树形表格）
- [x] UMC.Web：角色管理页面（菜单权限树、数据权限配置）
- [ ] 验收：不同角色看到不同菜单，数据权限范围过滤生效

## Phase 4：用户管理完整后台

> **后端 CRUD API + 测试全部完成**。导入导出、UMC.Web 主控面板待实现。

- [x] [TDD] 用户管理 CRUD API + 导入导出（ClosedXML Excel）
- [x] [TDD] 机构管理 API（多公司树形结构、多层级）
- [x] [TDD] 职位管理 API
- [x] [TDD] 字典管理 API（类型 + 数据，带缓存）
- [x] UMC.Web：用户管理页面（搜索/分页/批量操作/分配角色）
- [x] UMC.Web：机构管理页面（支持展示两家公司）
- [x] UMC.Web：职位管理页面
- [x] UMC.Web：字典管理页面
- [x] UMC.Web：主控面板（工作台实时数据 + 操作日志/访问日志双 Tab + SignalR 公告推送）
- [x] UMC.Web：个人中心（资料/修改密码）
- [ ] 验收：RBAC 管理后台核心功能完整可用

## ✅ Phase 5：扩展功能（2026-05-04）

### ✅ AOP 操作日志
- [x] `LogAttribute` — 方法级标注 `[Log("操作描述")]`
- [x] `OperLogFilter` — IActionFilter 全局注册，拦截 `[Log]` 标注的 mutation 写入 `OperLogs` 表
- [x] `OperLog` 实体 + `DbSeeder` 5 条种子数据
- [x] `MonitorController.GetOperLogs` + `DeleteOperLogs` + `CleanOperLogs`
- [x] UserController / DeptController / RoleController / PostController / DictController / NoticeController 全部加 `[Log]`
- [x] 6 个新测试（Phase5MonitorTests.cs），85/85 全部通过

### ✅ SignalR 在线用户
- [x] `OnlineUserService` — ConcurrentDictionary 线程安全内存存储
- [x] `OnlineUserHub` — [Authorize] + OnConnectedAsync（写入用户IP/部门）+ OnDisconnectedAsync
- [x] `MonitorController.GetOnlineUsers` + `ForceLogout`（TryRemove → 404 / SendAsync "ForceLogout"）
- [x] Program.cs：`AddSignalR()` + `AddSingleton<OnlineUserService>` + `MapHub<>("/hubs/online")`
- [x] **前端**：`npm install @microsoft/signalr`
- [x] `src/utils/hubService.ts` — HubConnectionBuilder + withCredentials + withAutomaticReconnect
- [x] `views/monitor/online/index.vue` — 移除 Mock 警告条，改为 SignalR 连接状态指示器

### ✅ UMC.Web 主控面板
- [x] `GET /api/monitor/dashboard` 返回实时统计：在线用户数 / 今日登录数 / 系统公告数 / 在职用户数
- [x] 工作台 4 张数据卡（实时数据，替换硬编码）
- [x] 操作日志/访问日志双 Tab（可切换）
- [x] 系统公告侧栏（最新 4 条）
- [x] SignalR `UserConnected` / `UserDisconnected` 事件联动刷新在线人数

### ✅ 公告管理 SignalR 实时推送
- [x] `NoticeController.Create/Update` 在发布状态下广播 `NewNotice` 事件
- [x] 工作台监听 `NewNotice` — 公告列表实时追加，公告计数器 +1

### ✅ 导入导出（ClosedXML）
- [x] `GET /api/system/user/export` — 按当前筛选条件导出 Excel（.xlsx）
- [x] `GET /api/system/user/importTemplate` — 下载导入模板
- [x] `POST /api/system/user/import` — 批量导入（跳过重名行，返回成功/失败明细）
- [x] UMC.Web 用户管理页：导出按钮 + 导入弹窗（含模板下载 + 文件选择 + 结果预览）

### ✅ HasPermission 权限中间件（Phase 3 补全）
- [x] `HasPermissionAttribute` — IAuthorizationFilter 读取 Cookie Claims
- [x] 权限写入 Cookie（登录时嵌入 `"permission"` Claim，零 DB 开销）
- [x] `TestAuthHandler` — 测试环境默认 admin，`X-Test-UserId` 切换用户，`X-Test-Anonymous` 模拟匿名
- [x] 8 个权限测试（Phase3PermissionTests.cs），93/93 全部通过

### ✅ 系统配置（带缓存）
- [x] `ConfigController` + `IMemoryCache`（CacheKeyPrefix `sys_config:`，10 分钟 TTL）
- [x] `GET /key/{configKey}` — 先读缓存，miss 时查 DB 并写缓存
- [x] `PUT /refreshCache` — 清空所有键的缓存
- [x] Update / Delete 均失效对应缓存条目
- [x] UMC.Web：`tool/config/index.vue` — CRUD + 刷新缓存按钮（已存在）

### ✅ 文件管理（本地存储）
- [x] `FileController` — 实际 `IFormFile` 上传，GUID 文件名，存储在 `uploads/` 目录
- [x] `GET /download/{storedName}` — Path.GetFileName 防路径穿越
- [x] `POST /api/common/upload` — 通用上传别名
- [x] UMC.Web：`tool/file/index.vue` — 上传按钮（FormData POST）+ 文件列表 + 批量删除
- [x] 文件列表按 `fileSuffix` 过滤（修正旧版 `service` 过滤）

### ✅ 任务调度（Cronos BackgroundService）
- [x] `JobSchedulerService` — BackgroundService，30s tick，`CronExpression.Parse` 计算下次运行
- [x] `JobLog` 实体 + `ApplicationDbContext.JobLogs` — 记录每次执行结果
- [x] 内置任务：`cleanOldLogs`（清理 90 天前操作日志）
- [x] `JobController` — CRUD + `PUT /run/{id}`（立即执行）+ `PUT /pause/{id}` / `PUT /resume/{id}`
- [x] `GET /log/list` + `DELETE /log/clean`
- [x] UMC.Web：`tool/job/index.vue` — 任务列表 Tab + 执行日志 Tab，含执行/暂停/恢复/编辑/删除

### ✅ 限流控制（ASP.NET Core 内置 RateLimiter）
- [x] `builder.Services.AddRateLimiter()` — 固定窗口 `"login"`（10次/分钟，防暴力破解）+ 滑动窗口 `"api"`（200次/分钟）
- [x] `[EnableRateLimiting("login")]` 标注 `AccountController.Login`
- [x] `app.UseRateLimiter()` 中间件注册，拒绝返回 429

### ✅ SMTP 邮件发送（2026-05-09）
- [x] `IEmailService` 接口 + `SmtpEmailService`（MailKit 4.16，StartTLS/Plain 两模式）
- [x] `EmailSettings`：Host / Port / UseSsl / UserName / Password / FromAddress / FromName（`appsettings.json` 配置）
- [x] `POST /api/account/forgot-password`：按邮箱查用户，生成 30 分钟 token 写 IMemoryCache，发送重置链接
- [x] `POST /api/account/reset-password`：验证 token，更新 PasswordHash，token 一次性失效
- [x] 4 个测试覆盖：已知邮箱发邮件 / 未知邮箱 404 / 有效 token 改密可登录 / 无效 token 400

### ✅ 微信 OAuth 2.0（PC 扫码登录，2026-05-09）
- [x] `IWechatOAuthService` 接口 + `WechatOAuthService`（HttpClient 直接调用微信 API，无第三方 SDK）
- [x] `WechatSettings`：AppId / AppSecret / CallbackBaseUrl / FrontendBaseUrl（`appsettings.json` 配置）
- [x] `WechatExternalLogin` 实体（`sys_wechat_login` 表，OpenId 唯一索引）
- [x] `GET /api/account/wechat/authorize`：生成 state（防 CSRF）→ 返回微信扫码 URL
- [x] `GET /api/account/wechat/callback`：换 token → 获取用户信息 → 首次自动建账号 → 签发 Cookie → 跳转前端
- [x] `POST /api/account/wechat/bind`（已登录）：绑定微信到现有账户
- [x] `DELETE /api/account/wechat/unbind`（已登录）：解绑微信
- [x] 5 个测试覆盖：获取授权 URL / state 防 CSRF / 缺失 code / 新用户首次登录 / 已有绑定用户登录
- [x] 配置填入后（AppId + AppSecret）即可直接使用，其余代码无需修改

### 待实现
- [ ] 短信发送（需短信服务商账号，如阿里云 SMS / 腾讯 SMS）

## ✅ Phase 6：DevOps（2026-05-04）
- [x] 六个项目各自编写 Dockerfile（.NET 10 多阶段构建：SDK → aspnet runtime）
- [x] 六个前端 Dockerfile（Node 22 构建 + nginx:stable-alpine 托管）+ nginx.conf（SPA fallback + /api 反向代理）
- [x] docker-compose.yml（12 个服务，端口 5001-5006 / 5173-5178，healthcheck + 依赖链）
- [x] GitHub Actions CI 流水线（.github/workflows/ci.yml）：后端 dotnet test × 6 + 前端 tsc + build × 6 + compose 语法校验
- [x] 全局异常处理中间件（`Middleware/GlobalExceptionMiddleware.cs`，全部 6 个 API 统一接入）
- [x] `/health` 健康检查端点（全部 6 个 API，docker-compose healthcheck 使用）
- [ ] 生产证书替换（OpenIddict 签名/加密证书）

**CI 矩阵**（`.github/workflows/ci.yml`）：
| Job | 内容 |
|---|---|
| `backend (OctopusUMC)` | `dotnet test OctopusUMC/OctopusUMC.sln` |
| `backend (OctopusOA)` | `dotnet test OctopusOA/OctopusOA.sln` |
| `backend (OctopusPLM)` | `dotnet test OctopusPLM/OctopusPLM.sln` |
| `backend (OctopusCRM)` | `dotnet test OctopusCRM/OctopusCRM.sln` |
| `backend (OctopusWMS)` | `dotnet test OctopusWMS/tests/...` |
| `backend (OctopusMES)` | `dotnet test OctopusMES/tests/...` |
| `frontend × 6` | `npm ci && tsc --noEmit && npm run build` |
| `docker-lint` | `docker compose config --quiet` |

**总测试数（283 个，全部通过）**：UMC 93 + OA 90 + PLM 22 + CRM 31 + WMS 23 + MES 20 = **279 个测试**

---

## 全链路 ERP 扩展（CRM → WMS → MES）

> 详细方案见 `ROADMAP.md` 二至八章。

### ✅ 已完成基础（UMC + OA + PLM）
- [x] UMC：统一身份/SSO/RBAC（79 个测试）
- [x] OA：审批流引擎 + 考勤/公告/会议/通讯录（90 个测试）
- [x] PLM：商品/类目/SKU/渠道映射/1688导入（22 个测试）

### ✅ P1：OctopusCRM 客户关系管理

- [x] 项目骨架 + Persistence（10 张表，CrmDbContext + CrmDbSeeder）
- [x] CustomerService / InquiryService / QuoteService / ContractService / PaymentService / StatsService
- [x] 所有 Controller + ApprovalCallbackController（HMAC）+ UserSyncController + MeController
- [x] `dotnet test` 31/31 通过（CustomerFlowTests / SalesPipelineFlowTests / ContractPaymentTests / StatsTests）
- [x] 前端（shadcn-vue，端口 5176）：客户/询盘/报价/合同/回款 全部页面
- [x] Aspire 集成：crm-api(5004) + crm-web(5176)

### ✅ P2：OctopusWMS 仓储管理

- [x] 项目骨架 + Persistence（10 张表，wms_ 前缀）
- [x] 仓库/库存/入库/出库/盘点 全部 Service + Controller
- [x] `dotnet test` 23/23 通过
- [x] 前端（shadcn-vue，端口 5177）：库存看板 + 入出库管理
- [x] Aspire 集成：wms-api(5005) + wms-web(5177)

### ✅ P3：OctopusMES 生产与采购

- [x] 项目骨架 + Persistence（6 张表，mes_ 前缀）
- [x] 供应商/采购订单/生产工单 全部 Service + Controller
- [x] `dotnet test` 20/20 通过
- [x] 前端（shadcn-vue，端口 5178）：工单 + 采购 + 供应商管理
- [x] Aspire 集成：mes-api(5006) + mes-web(5178)

### ✅ P4：全链路 BI 看板（2026-05-04）

- [x] 销售漏斗（CRM pipeline：询盘→报价→合同，含转化率）
- [x] 时效仪表盘（7 大指标 vs SLA，RAG 红绿黄状态色）
- [x] 合同全链路时间轴（询盘→报价→审批→合同→发货→收款，含逾期标注）
- [x] 审批积压（CRM 侧：报价/合同/回款三类待审批数量 + 超时计数）
- [x] OTD 趋势折线图（6 个月月度趋势，SVG 折线 + 95% SLA 参考线）
- [x] 后端：`GET /api/stats/bi/efficiency|otd-trend|approval-backlog|contract-timeline/{id}|contracts`

---

## 下一阶段规划（2026-05-09 Review 产出）

> 背景：六大模块全部完成（284 个测试），全链路业务流已贯通。Review 发现三类缺口：
> ① 跨系统 Webhook 调用链只有接收方，缺发送方；② OA SLA 催办未实现；③ 移动端空白。
> 以下按优先级排列，P0 最紧。

### ✅ P5：H5 移动端（OA + UMC）— 已完成

#### UMC H5（仅 SSO 登录页 + 个人资料）

- [x] 新建 `OctopusUMC/web/OctopusUMC.H5/`（Vite + Vue 3 + Tailwind v4）
- [x] Cookie 登录页：用户名/密码 + 行内错误提示
- [x] 首页工作台：模块入口宫格（OA/WMS/MES/CRM 跳转）
- [x] 个人资料页：昵称/手机/邮箱编辑 + 修改密码
- [x] Aspire 集成：端口 5179

#### OA H5（员工日常高频场景）

- [x] 新建 `OctopusOA/web/OctopusOA.H5/`（Vite + Vue 3 + Tailwind v4 + oidc-client-ts）
- [x] 底部 TabBar 框架：首页 / 审批 / 考勤 / 我的（4 Tab）
- [x] 考勤打卡页：今日状态卡片 + [上班打卡] / [下班打卡] + 本月日历摘要 + 最近记录
- [x] 审批列表页（待审批/我的申请 Segment）+ 发起申请弹层（模板宫格 + 标题输入）
- [x] 审批详情页：申请信息 + 流程时间轴 + 通过/驳回底部弹层（含填写意见）
- [x] 首页工作台：4 张统计卡 + 快捷入口宫格 + 待办聚合列表
- [x] 公告列表 + 详情（铃铛入口，未读蓝点标记）
- [x] 通讯录：部门侧边栏 + 用户列表 + 详情底部弹层（拨号/邮件）
- [x] 会议室预订：我的预订列表 + 新建预订底部表单（冲突提示 409）
- [x] 个人中心：资料展示 + 账号设置跳转 UMC H5 + 退出登录（OIDC signoutRedirect）
- [x] OIDC SSO 登录/回调（新建 `octopus-oa-h5` 客户端，端口 5180/callback）
- [x] Aspire 集成：端口 5180
- [x] UMC CORS 放行 5179/5180；OA CORS 放行 5179/5180

**技术约定（H5 通用）**：
- 375px 基准宽度，`viewport-fit=cover` 支持刘海屏
- 点击区域 ≥ 44px（iOS 规范）
- 底部固定元素留 `env(safe-area-inset-bottom)` 安全区
- 禁止使用 reka-ui（同 OA.Web 规避 SPA 冻结问题）
- Dialog/Sheet/Select 使用 `provide/inject` + `<Teleport to="body">` 自定义实现

---

### ✅ P6：跨系统 Webhook 调用链（打通全链路 7 步）— 已完成

> 当前各系统通过 `PlmProductId`/`CrmContractId` 等字段关联种子数据，但运行时跨系统实时联动缺少发送方。

#### 缺口清单

| 事件 | 接收方 | 发送方现状 | 优先级 |
|---|---|---|---|
| OA 审批通过/驳回 → CRM | `CRM ApprovalCallbackController` ✅ | OA 无主动 POST | ★★★ |
| CRM 合同生成 → WMS 库存预留 | ❌ WMS 无此端点 | CRM 无调用 | ★★★ |
| MES 工单完工 → WMS 入库 | ❌ WMS 无此端点 | MES 无调用 | ★★ |
| WMS 出库完成 → CRM 回填发货日 | ❌ CRM 无此 Webhook | WMS 无调用 | ★★ |
| CRM 合同 → WMS 缺料 → MES 工单 | ❌ MES 无此端点 | WMS 无调用 | ★ |

#### 实现方案（直接 HTTP，不引入消息队列）

```csharp
// 统一 WebhookSender 工具类（每个系统各有一个，复用 UserSyncService 模式）
public class WebhookSender(IHttpClientFactory factory, IConfiguration config, ILogger<WebhookSender> log)
{
    public async Task PostAsync(string targetBaseUrl, string path, object payload, string secret)
    {
        var json = JsonSerializer.Serialize(payload);
        var sig  = ComputeHmacSha256(json, secret);
        var req  = new HttpRequestMessage(HttpMethod.Post, $"{targetBaseUrl}{path}");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Headers.Add("X-Signature", $"sha256={sig}");
        try { await factory.CreateClient().SendAsync(req); }
        catch (Exception ex) { log.LogError(ex, "Webhook 发送失败 {Path}", path); }
    }
}
```

#### 实施步骤

- [ ] OA `ApprovalService.Approve()` / `Reject()` 末尾：扫描关联的 CRM/MES 回调 URL，POST 结果
- [ ] WMS 新增 `POST /api/stock/reserve`（CRM 合同生成时调用，预留库存）
- [ ] WMS 新增 `POST /api/inbound/receive-from-mes`（MES 完工时调用，自动入库）
- [ ] WMS `OutboundService.ConfirmAsync()` 末尾：POST CRM `/api/contract/{id}/ship`（回填 ActualDeliveryDate）
- [ ] CRM `ContractService.ExecuteAsync()` 末尾：POST WMS `/api/stock/reserve`（库存预留）
- [ ] MES `WorkOrderService.CompleteAsync()` 末尾：POST WMS `/api/inbound/receive-from-mes`
- [ ] 每条 Webhook 补充集成测试（用 `WireMock.Net` mock 目标端点）
- [ ] `appsettings.json` 各系统新增 `Webhooks:{ CrmBaseUrl, WmsBaseUrl, MesBaseUrl, OaBaseUrl }` 配置节

---

### ✅ P7：OA 审批 SLA 催办 — 已完成

> ROADMAP 有完整设计但未实现：`SlaHours` 字段 + BackgroundService 扫描 + 站内消息推送

- [ ] `WorkflowNode` 实体新增 `public int? SlaHours { get; set; }`
- [ ] `Approval` 实体新增 `public DateTime? CurrentNodeDeadline { get; set; }`
- [ ] `ApprovalService.AdvanceNode()` 写入 `CurrentNodeDeadline = UtcNow + SlaHours`
- [ ] 新增 `ApprovalSlaWorker : BackgroundService`，每 30 分钟扫描：
  - `CurrentNodeDeadline < UtcNow + 4h` → 发预警通知
  - `CurrentNodeDeadline < UtcNow` → 标记超时，发催办通知
- [ ] `NoticeService.SendAsync()` 推站内消息给当前节点审批人
- [ ] `Program.cs` 注册 `builder.Services.AddHostedService<ApprovalSlaWorker>()`
- [ ] OA 前端：审批详情页节点超时标红 + 剩余时效倒计时
- [ ] OA 前端：待审批列表显示超时徽标 🔴
- [ ] 种子模板中为各节点设置合理 SlaHours（请假：48h，报销：24h，补卡：4h）
- [ ] 补充测试：`SlaWorkerTests.cs`（超时触发通知 + 未超时不触发）

---

### 🔶 P8：生产就绪 — 限流/操作日志已完成，MySQL/Redis 待做

#### 数据库：SQLite → MySQL

- [ ] 各系统创建 EF Core Migration（当前是 `EnsureCreated`，无迁移历史）
  ```bash
  cd OctopusXXX/src/OctopusXXX.Api
  dotnet ef migrations add InitialCreate --project . --startup-project .
  dotnet ef migrations script | review → 手动执行
  ```
- [ ] `appsettings.Production.json` 切换 MySQL 连接串（6 个系统各一份）
- [ ] 测试保持用 SQLite `:memory:`，生产用 MySQL（双轨，无需改测试）
- [ ] docker-compose.yml 补充 MySQL 服务（已有 `octopus-db` 占位，补全配置）

#### 缓存层：Redis

- [ ] `docker-compose.yml` 添加 `redis:7-alpine` 服务
- [ ] 各系统 `Program.cs` 注册 `AddStackExchangeRedisCache`
- [ ] 优先缓存（TTL 5 分钟）：UMC 菜单树、UMC 权限 Claims、OA 部门树、PLM 类目树
- [ ] 缓存失效：对应数据 CRUD 时主动 `cache.Remove(key)`

#### 安全加固

- [ ] 各系统统一异常处理中间件（不暴露堆栈，返回 `{ code: 500, msg: "服务器内部错误" }`）
- [ ] CRM / WMS / MES 补充限流（复制 UMC 的 `AddRateLimiter` 配置）
- [ ] OpenIddict 生产证书：替换 `AddDevelopmentEncryptionCertificate` 为 `.pfx` 文件
- [ ] 生产环境强制 HTTPS（`UseHttpsRedirection` + `UseHsts`）
- [ ] CRM / WMS / MES 补充操作日志 AOP（参考 UMC `OperationLogAttribute`）

---

### ✅ P9：统一通知中心 — 已完成（OA `NotificationHub` + `/api/internal/notify`）

> 当前：OA 公告独立、UMC SignalR 独立，各系统没有跨系统实时推送能力。

- [ ] UMC `NoticeController` 新增内网接口：`POST /api/internal/notify`（HMAC 签名，不对外）
- [ ] UMC `OnlineUserHub` 扩展：收到内网通知后 `Clients.User(userId).SendAsync("Notification", payload)`
- [ ] CRM / WMS / MES 在关键事件（合同审批通过、库存预警触发、工单完工）后 POST UMC 通知接口
- [ ] 前端（所有子系统）：TopBar 铃铛统一显示未读数，点击展开跨系统通知列表
- [ ] 通知类型定义：`approval_result` / `stock_alert` / `workorder_done` / `notice` / `mention`

---

## 二期规划（P10–P17，对标金蝶云星空 + 聚水潭）

> 详细技术方案见 `ROADMAP.md` 第十四至二十一章。
> 触发原因：用户反馈 PLM 列表展示弱、缺图标、跨系统接口少；MES 无时效统计；WMS 太基础不可用。

### ✅ P10：PLM 升级（列表强化 + 类目模板进阶 + 跨系统标准化 API）— 已完成

**14.1 商品列表展示强化**
- [ ] `ProductResponse` 扩展字段：`thumbnailUrl` / `skuCount` / `priceRange` / `totalStock` / `salesQty30d` / `channelBindings` / `statusIcon` / `lifecycleStage`
- [ ] `totalStock` 实现：PLM 调 WMS `POST /api/inventory/by-products`（批量）
- [ ] `salesQty30d` 实现：PLM 调 CRM `POST /api/contract/sales-qty?productIds=`
- [ ] 商品列表前端改造：缩略图列 + lucide 状态图标（FileEdit/Clock/CheckCircle2/XCircle）
- [ ] 排序：按销量/更新时间/价格/库存
- [ ] 高级筛选抽屉：类目树 + 状态多选 + 价格区间 + 渠道 + 创建人
- [ ] 行级快速操作菜单（提交审核/克隆/复制 SKU/上下架）
- [ ] 列表性能：1000 商品虚拟列表（vue-virtual-scroller）

**14.2 类目模板进阶**
- [ ] 属性模板可视化编辑器（左侧属性库 → 拖到右侧 SPU/SKU 槽位 → SKU 矩阵预览）
- [ ] 版本对比页（左右两栏 diff，新增/删除/修改高亮）
- [ ] 版本回滚 + 二次确认 + 审计日志
- [ ] `POST /api/category/{id}/template/apply-all`：批量应用变更到子类目商品
- [ ] 类目模板继承机制（子类目继承父类目，可覆盖）

**14.3 跨系统标准化 API**
- [ ] `POST /api/product/by-codes`：CRM/WMS/MES 通用批量查询
- [ ] `POST /api/product/by-ids`：按 ID 批量
- [ ] `GET /api/product/{id}/cross-system`：跨系统数据聚合（PLM + WMS + CRM + MES）
- [ ] PLM 事件订阅推送：`product.created/updated/published/discontinued/sku.price_changed`
- [ ] 各下游系统注册订阅意向（写到 PLM `plm_subscriber` 表）
- [ ] 集成测试：5 条事件链路 ≥ 5 个测试

---

### ✅ P11：WMS 升级（聚水潭风格执行级 WMS）— 已完成

**15.1 多货主多仓 + 库区/库位**
- [ ] 新增 `wms_owner` 货主表，`wms_warehouse` 加 `OwnerId`
- [ ] 新增 `wms_zone` 库区表（拣货区/存储区/退货区/质检区/不良品区）
- [ ] `wms_location` 扩展：`ZoneId` / `BinType` / `AbcClass` / `MaxQty` / `IsLocked`
- [ ] `wms_inventory` 改造：唯一索引 (Warehouse, Location, Product, BatchNo, Owner)
- [ ] ABC 分类自动计算：定时任务按 30 天出库频次更新

**15.2 批次/序列号管理**
- [ ] 新增 `wms_batch` 批次表（生产日期/有效期/QC 状态）
- [ ] 新增 `wms_serial` 序列号表（电子产品强制扫码）
- [ ] FEFO 出库策略：按 `ExpireDate ASC` 自动选批次
- [ ] PDA H5 扫码登记序列号

**15.3 入库流程升级**
- [ ] `wms_inbound_qc` 入库质检单表
- [ ] PDA H5 扫码上架（库位扫码 → 商品扫码 → 数量录入）
- [ ] 收货差异处理：实收 ≠ 应收触发 OA 审批（templateCode `INBOUND_DIFF_APPROVAL`）

**15.4 出库流程升级（波次拣货）**
- [ ] 新增 `wms_wave` 波次单 + `wms_pick_task` 拣货任务 + `wms_package` 包裹
- [ ] 4 种波次策略：单品单订/多品分播/同库位归并/急单优先
- [ ] PDA 拣货界面（任务列表 → 库位导航 → 扫码确认）
- [ ] 复核打包流程：扫描包裹 → 称重 → 打面单
- [ ] 路径优化算法（贪心，按库位 X/Y 坐标排序）

**15.5 三方物流接入**
- [ ] 新增 `wms_logistics_provider` / `wms_logistics_account` / `wms_waybill` / `wms_logistics_track`
- [ ] 顺丰开放平台接入（下单/取消/电子面单/路由）
- [ ] 快递鸟 KdNiao 统一对接中通/韵达/圆通
- [ ] 面单打印（PDF/PNG，前端调用浏览器打印）
- [ ] 路由轨迹定时拉取（4h 一次 BackgroundService）
- [ ] 异常告警（超 3 天卡件/退回件）

**15.6 库内移库 + 调拨 + 盘点升级**
- [ ] 库内移库（PDA 扫描原位 → 目标位）
- [ ] 跨仓调拨单（出库 + 在途 + 入库三段式）
- [ ] 循环盘点定时任务（A 类月/B 类季/C 类半年）
- [ ] 抽盘 + 盲盘模式
- [ ] 差异自动生成调整单 → OA 审批

---

### ✅ P12：MES 升级（时效统计 + OEE + 节拍 + 瓶颈）— 已完成

**16.1 工单时效统计**
- [ ] `MesWorkOrder` 扩展：`PlannedStart/EndTime`、`ActualStart/EndTime`、`ScheduleVariance`、`LeadTimeHr`
- [ ] `GET /api/stats/wo-efficiency`：按期完工率/平均交期/排程依从度/瓶颈/工人效率
- [ ] `GET /api/stats/wo-trend?months=6`：6 月趋势

**16.2 工序级管理（报工系统）**
- [ ] `MesWorkOrderProcess` 改造：`StandardCycleSec`、`ActualCycleSec`、`QualifiedQty`、`DefectQty`、`ReworkQty`、`OperatorId`、`EquipmentId`、`DowntimeMinutes`、`DowntimeReason`
- [ ] PDA H5 扫码报工（开工/暂停/完工 + 数量）
- [ ] 工序流转看板（生产现场大屏，实时在制品）
- [ ] 异常报工自动创建处理单

**16.3 设备 OEE**
- [ ] 新增 `mes_equipment` 设备档案 + `mes_equipment_log` 运行日志
- [ ] 每日跑批 `mes_equipment_oee_daily`：可用率 × 表现率 × 质量率
- [ ] OEE 看板（仪表盘 + 趋势）

**16.4 瓶颈与产能分析**
- [ ] `GET /api/stats/capacity-load?date=`：设备/工序负载率
- [ ] `GET /api/stats/bottleneck`：瓶颈识别
- [ ] `GET /api/stats/operator-ranking`：工人产量/合格率排名
- [ ] 不良品 Pareto 图（按缺陷类型）
- [ ] 节拍偏差折线（标准 vs 实际，超标标红）

---

### ✅ P13：OctopusFIN（财务）— 已完成（端口 5007）

- [ ] 项目骨架 `OctopusFIN/src/OctopusFIN.Api`（端口 5007/5181）
- [ ] 数据模型 12 表：`fin_account` / `fin_voucher` / `fin_voucher_entry` / `fin_period` / `fin_ar_invoice` / `fin_ar_receipt` / `fin_ap_invoice` / `fin_ap_payment` / `fin_expense_claim` / `fin_bank_account` / `fin_bank_statement` / `fin_journal`
- [ ] 阶段 1：科目树 + 凭证录入 + 月结
- [ ] 阶段 2：CRM 发货 → AR 自动凭证 / MES 入库 → AP 自动凭证
- [ ] 阶段 3：OA 报销审批通过 → 费用凭证 + 出纳付款单
- [ ] 阶段 4：三大报表（资产负债 / 利润 / 现金流量）
- [ ] Aspire 集成 + 测试 ≥ 30 个

---

### ✅ P14：OctopusHRM（人力资源）— 已完成（端口 5008，11 测试）

- [ ] 项目骨架（端口 5008/5182）
- [ ] 员工档案升级（从 OA Employee 迁移 + 合同/社保/薪资历史）
- [ ] 薪酬模块：薪资项配置 + 月度算薪（拉 OA 考勤）+ 工资条
- [ ] 算薪完成 → 推 FIN 生成薪资凭证
- [ ] 绩效模块：KPI/OKR 设置 + 评分 + 360 评估
- [ ] 招聘模块：JD 发布 + 投递 + 面试 + Offer
- [ ] Offer 接受 → 自动创建 OA Employee（H5 入职流程联通）
- [ ] 假期模块：余额管理 + 与 OA 考勤打通
- [ ] 培训模块：课程 + 报名 + 证书

---

### ✅ P15：OctopusOMS（订单中台，聚水潭风格）— 已完成（端口 5009，10 测试）

- [ ] 项目骨架（端口 5009/5183）
- [ ] 数据模型：`oms_channel` / `oms_shop` / `oms_order` / `oms_order_item` / `oms_sku_mapping` / `oms_inventory_sync_rule` / `oms_after_sale`
- [ ] 渠道接入（按优先级）：
  - [ ] 淘宝/天猫（顶级开放平台）
  - [ ] 京东（JOS）
  - [ ] 拼多多
  - [ ] 抖音小店
  - [ ] 独立站 Shopify（Webhook）
  - [ ] Amazon SP-API（跨境）
- [ ] 订单合并/拆单/预售/赠品规则
- [ ] 库存分配规则引擎（按比例/按优先级/按销量预测）
- [ ] 售后管理：退/换/补/退款
- [ ] 黑名单/风控

---

### 🔶 P16：客户 + 供应商门户 — 后端就绪，前端待建

> UMC 已新增 4 个 OIDC 客户端（`octopus-hrm-web` / `octopus-oms-web` / `octopus-customer-portal` / `octopus-supplier-portal`）+ `customer` / `supplier` 两个角色。
> 前端 Vite 项目（端口 5184 / 5185）尚未实装。

**客户门户（OctopusPortal.Customer）**
- [ ] 独立 H5 + PWA（端口 5184）
- [ ] UMC 新建 OIDC 客户端 `octopus-customer-portal`，新增 `customer` 角色
- [ ] 我的询盘 / 报价进度
- [ ] 我的合同 / 下载 PDF
- [ ] 物流追踪
- [ ] 对账中心
- [ ] 在线客服（接 OctopusOMS 客服工单）

**供应商门户（OctopusPortal.Supplier）**
- [ ] 独立 H5（端口 5185）
- [ ] UMC 新增 `supplier` 角色
- [ ] 询价响应（在线报价）
- [ ] 我的订单（备货状态）
- [ ] 发货登记
- [ ] 对账中心
- [ ] 资质上传

**API 网关（OctopusGateway）**
- [ ] 新建 .NET YARP 反向代理项目
- [ ] 统一限流 / 鉴权 / 日志
- [ ] 外网请求强制走网关

---

### ✅ P17：低代码 BPM（OA 工作流升级）— 引擎层已完成，设计器待建

> 已完成：`WorkflowNode.SkipCondition`（表达式跳过：`amount < 1000`）+ `CcUsersJson`（抄送）+ `ApprovalDelegate` 转交表 + `ApprovalService.Delegate / EvaluateCondition / NotifyCcUsers`。5 个 BpmExtensionTests 全绿。
> 待做：拖拽流程设计器（前端）+ 表单设计器升级（字段联动 / 公式 / 明细表 / 关联下拉）。

- [ ] 引入 Elsa Workflows 或自研 BPMN 简化引擎
- [ ] 流程设计器（拖拽节点：开始/审批/抄送/分支/并行/结束）
- [ ] 条件分支（表单字段表达式：`金额 > 100000`）
- [ ] 委托/转交/加签节点
- [ ] 自由跳转（驳回到指定节点而非起点）
- [ ] 表单设计器升级：
  - [ ] 字段联动（A 变 → B 显示/必填切换）
  - [ ] 公式字段（`总金额 = 数量 × 单价`）
  - [ ] 明细表（一对多子表）
  - [ ] 关联数据下拉（实时查接口）
- [ ] 流程模板库（请假/报销/采购/合同/调岗/离职/资产领用/入职…）

---

## 长期愿景（12 个月+）

### L1：多租户 SaaS 化
- [ ] 所有表加 `TenantId`，中间件自动注入过滤
- [ ] JWT 携带 TenantId，路由级数据隔离
- [ ] 计费引擎（用户数/存储/调用量）
- [ ] 双轨发布：开源版（基础功能）+ 商业版（高级功能 + SLA）

### L2：移动原生 App（Capacitor 套壳）
- [ ] iOS / Android 双端壳工程（基于 OA H5）
- [ ] 推送通知（极光/腾讯信鸽）
- [ ] 离线打卡 + 网络恢复同步
- [ ] 条码 / 二维码扫描
- [ ] GPS 围栏定位打卡

### L3：AI 赋能
- [ ] 智能审批助手（LLM 摘要 + 类型分类）
- [ ] 销售助理（RAG 检索历史合同推荐报价）
- [ ] 库存预测（时序模型，自动建议采购量）
- [ ] 客户意向分析（沟通记录 → LLM 抽取意向标签）
- [ ] 智能客服（RAG + LLM 自动应答）

### L4：国际化 + 跨境
- [ ] i18n（中/英/日/西）
- [ ] 多币种 + 实时汇率（FIN）
- [ ] 多税制（增值税 / GST / VAT）
- [ ] 用户级时区
- [ ] 海外仓 + 报关报检（OMS 跨境扩展）
- [x] 前端：`CRM.Web /bi` 路由，SubMenu 新增"分析"组，纯 CSS/SVG 图表无三方依赖
