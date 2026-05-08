# OctopusUMC & OctopusOA 实现进度清单

> 六步闸门制：每步全部通过后方可进入下一步。

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

### 待实现
- [ ] 邮件短信发送
- [ ] 开放授权（微信 OAuth 2.0）

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
- [x] 前端：`CRM.Web /bi` 路由，SubMenu 新增"分析"组，纯 CSS/SVG 图表无三方依赖
