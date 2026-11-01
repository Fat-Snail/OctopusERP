# 架构决策记录

## 认证模型

**UMC 自身会话**：基于 Cookie（HttpOnly + Secure）。UMC.Web 使用 Cookie 维持自身登录状态。
**外部应用令牌**：由 OpenIddict 通过 OIDC 授权码流 + PKCE 颁发 JWT。
**理由**：Cookie 对第一方浏览器会话更安全；JWT 无状态，适合跨服务鉴权。

---

## 数据库

**生产**：MySQL 8.0，使用 Pomelo.EntityFrameworkCore.MySql 驱动。
**开发/测试**：SQLite（`Microsoft.EntityFrameworkCore.Sqlite`）。

| 环境 | 连接方式 | 说明 |
|---|---|---|
| 开发 | `Data Source=octopus_umc.db` | SQLite 文件，可用 DB Browser 查看 |
| 测试 | `Data Source=:memory:;Cache=Shared` | SQLite 内存 + 持久化连接（防止测试间数据丢失） |
| 生产 | `server=...;database=...` | MySQL 8.0 |

**连接字符串键名**：`ConnectionStrings:Default`
**迁移策略**：始终通过 `dotnet ef` CLI 手动执行，禁止在生产环境启动时自动迁移。
**字符集（MySQL）**：统一使用 `utf8mb4` + `utf8mb4_unicode_ci`，避免 emoji 及特殊字符存储问题。

---

## 用户-部门-角色：关联表而非单一外键

### 决策

用户与部门、用户与角色、角色与菜单、角色与部门（数据权限范围）均使用**独立关联表**，而非在主实体上保存单一外键或 JSON 数组列。

### 关联表结构

```sql
sys_user_dept (UserId PK, DeptId PK, PostId?, IsPrimary)
sys_user_role (UserId PK, RoleId PK)
sys_role_menu (RoleId PK, MenuId PK)
sys_role_dept (RoleId PK, DeptId PK)  -- 角色数据权限自定义部门范围
```

### 理由

**原始方案（被放弃）**：在 `User` 表上存 `DeptId`（单个外键）。

**问题**：当一名员工同时管理多家公司的多个部门时，单个 `DeptId` 无法表达：

```
zhangsan
  ├── A公司：章鱼科技 - 技术部（主部门，职位：技术总监）
  └── B公司：海星科技 - 技术部（兼职，职位：工程师）
```

**关联表方案的优势**：
1. 一名用户可属于任意数量的部门（跨公司兼职）
2. `IsPrimary=true` 标记主部门，`UserResponse.deptId/deptName` 始终显示主部门
3. `?deptId=N` 数据权限查询基于 `sys_user_dept` 关联表，不依赖 `User` 本身字段
4. 用户在 B 公司的查询结果中仍显示其主部门信息（A 公司），不产生混淆
5. 同理适用于 `Role.MenuIds`（用 `sys_role_menu`）和 `Role.DeptIds`（用 `sys_role_dept`），消除了原来用 JSON 字符串列存储的坏味道

### 数据权限查询行为（关键规则）

```
?deptId=10（B公司技术部）查询结果：
  - zhangsan  → 出现（因 sys_user_dept 有记录）  deptId=3（主部门，A公司）
  - zhaoliu   → 出现（主部门就是 B公司技术部）   deptId=10

「能看到谁」由 sys_user_dept 决定
「显示什么归属」由 IsPrimary=true 的记录决定
两者相互独立，不会因查询条件改变响应内容
```

### 用户跨公司任职查询

新增端点 `GET /api/system/user/{userId}/depts`，返回该用户所有任职记录，含：
- 所属部门名称
- 向上追溯到的根公司名称
- 职位名称
- `isPrimary` 标志

---

## SQLite 测试策略（TestWebApplicationFactory）

**选择 SQLite 内存模式而非 EF Core InMemory Provider**，理由：

| 对比项 | EF Core InMemory | SQLite 内存 |
|---|---|---|
| 关系约束 | 不强制（可插入孤立记录） | 完整 SQL 语义 |
| 复合主键 | 支持 | 支持 |
| JOIN 查询 | 基本支持 | 完整 SQL JOIN |
| 与生产行为一致性 | 差异较大 | 接近（同为 SQL 数据库） |
| 数据可观察性 | 无 | 可通过 DB Browser 查看开发文件 |

**持久化连接**：`TestWebApplicationFactory` 持有一个 `SqliteConnection` 对象的生命周期与整个测试类相同，防止内存数据库在首次连接关闭后销毁。

---

## OpenIddict vs IdentityServer

选择 **OpenIddict**，理由：
- MIT 协议，生产环境免费
- 与 EF Core 原生集成，无需额外适配
- 支持 .NET 8+，持续活跃维护
- IdentityServer 6+ 生产环境需要商业授权

---

## 前端 UI 框架

**选型**：vue-pure-admin（精简版 pure-admin-thin），两套系统（UMC.Web、OA.Web）统一基座。
**理由**：
- 已内置完整的动态路由、按钮权限（`v-auth`）、数据权限前端支持，与 UMC 权限体系直接对齐
- 基于 Element Plus，组件生态完整，适合功能密集的管理后台
- UnoCSS 原子化 CSS，样式维护成本低
- 内置暗色模式、多主题切换，符合现代简约设计风格
- 统一基座保证两套系统视觉一致性，降低整体维护成本

**初始化方式**：`git clone https://github.com/pure-admin/pure-admin-thin`，不使用 `create vite`。
**主题色**：统一使用蓝色系主色 `#1677ff`，在 `src/style/index.scss` 中覆盖 Element Plus 的 `--el-color-primary`。

---

## 前端包管理器

**npm** — Node.js 官方内置包管理器，无需额外安装，团队上手零门槛。

---

## 不使用 ASP.NET Core Identity

使用自定义 `User`/`Role` 实体，而非 `IdentityUser`，保持领域模型简洁，避免引入 Identity 框架的冗余包袱。密码哈希使用 `BCrypt.Net-Next`（work factor ≥ 12）。

---

## 前端认证模式：Cookie，非 Bearer Token

UMC.Web（第一方管理后台）使用 Cookie 认证，不在前端存储任何 Token：

- `http.ts` Axios 实例：`withCredentials: true`，不附加 `Authorization` 头
- 用户 store（`user.ts`）：持有 `LoginResponse` 对象（来自 `/api/account/me`），不存 localStorage
- 刷新页面恢复登录状态：App.vue 初始化时调用 `fetchMe()`，若 Cookie 仍有效则自动恢复
- 路由守卫：基于 Pinia store 的 `isLoggedIn` 计算属性，而非 localStorage token

**理由**：HttpOnly Cookie 防 XSS 劫持；OA.Web 使用 Bearer Token 是因为其跨域场景（OIDC 授权码流），两者设计场景不同。

---

## OA.Web：Dialog / Sheet / Select 组件替换（reka-ui → 自定义实现）

### 决策

`OA.Web` 中所有 `Dialog`、`Sheet`（抽屉）、`Select` 组件**不使用 shadcn-vue 安装的版本**（底层依赖 reka-ui primitives），改为零 reka-ui 依赖的自定义实现，外观与 shadcn-vue 设计系统保持一致。

### 根本原因

reka-ui 在多个组件层面使用**模块级单例状态**，在 Vue Router SPA 导航时不能可靠清理：

| 模块 | 问题 |
|---|---|
| `DismissableLayer` | `layersRoot` / `layersWithOutsidePointerEventsDisabled` 两个 Set 是模块级单例；`watchEffect` cleanup 有 null guard，stale entries 永不移除 |
| `FocusScope` | `focusScopesStack.remove()` 在 `setTimeout(0)` 内异步执行，导航时来不及清理 |
| `useBodyScrollLock` | `createSharedComposable` 全局单例，`overflow` / `paddingRight` 重置依赖最后一个消费者离开，跨路由时引用计数失衡 |
| `Presence` | 等待 `animationend` 事件才触发 `unmounted`，导航强制卸载时 state machine 可能停在中间态 |

症状：约 15 次侧边栏导航后 UI 完全冻结（所有点击无响应）。根本机制：`FocusScope` 的不可见层叠在 `#app` 之上拦截所有指针事件。

### 新架构

**Dialog / Sheet**：
- `Dialog.vue` / `Sheet.vue`：`provide('dialogCtx' / 'sheetCtx', { open: ComputedRef<boolean>, close: () => void })`，无 DOM 输出
- `DialogContent.vue` / `SheetContent.vue`：原生 `<Teleport to="body">` + `v-if` 控制挂载/卸载；覆盖层和面板均为普通 `<div>`
- 零 DismissableLayer / FocusScope / Presence / useBodyScrollLock 依赖

**Select**：
- `Select.vue`：`provide('selectCtx', ...)` + `@vueuse/core` 的 `onClickOutside`；根元素 `position: relative`
- `SelectContent.vue`：`v-if="ctx.open.value"` + `position: absolute; top: 100%`，**无 Teleport portal**
- 彻底消除跨路由的 portal 生命周期问题

**防御性 cleanup**（`src/router/index.ts`）：
```typescript
router.afterEach(() => {
  // 重置可能被遗留的 body 样式
  document.body.style.pointerEvents = ''
  document.body.style.overflow = ''
  document.body.style.paddingRight = ''
  document.body.style.marginRight = ''
  // 移除 #app 上可能被 aria-hidden 库误加的属性
  const appEl = document.getElementById('app')
  if (appEl) {
    appEl.removeAttribute('aria-hidden')
    appEl.removeAttribute('inert')
  }
})
```

### 影响范围

- **0 个 view 文件改动**：所有消费方使用相同的组件名、props 和 emit 接口
- 替换目录：`src/components/ui/dialog/`、`src/components/ui/sheet/`、`src/components/ui/select/`

---

## ADR-004：H5 移动端独立 Vite 项目，不共享桌面端代码

**日期**：2026-05-09

**背景**：需要为 OA 和 UMC 开发 H5 移动端版本。有两种方案：
- 方案 A：在现有桌面端 Vite 项目内用响应式布局适配移动端
- 方案 B：新建独立 Vite 项目（`OctopusOA.H5`、`OctopusUMC.H5`）

**决策**：选择方案 B，每个系统独立 H5 项目。

**理由**：
1. **交互范式差异**：桌面端三栏布局 + 鼠标悬停，H5 底部 TabBar + 手势，用响应式 CSS 同时驾驭两者极易失控
2. **体积控制**：H5 只需高频场景的 10 个页面，引入桌面端全量路由和组件会使 bundle 虚高
3. **路由隔离**：桌面端路由复杂（25+ 页面），H5 路由简洁（8 页），独立项目路由文件清晰可读
4. **UI 组件隔离**：H5 使用 UnoCSS + 自定义移动组件，不混入桌面端的 shadcn-vue + TanStack Table
5. **API 层可复用**：`src/api/` 请求函数可以 Copy-paste，类型定义镜像后端 DTO（不做 npm package 共享，避免跨仓库依赖复杂度）

**代价**：API 类型定义维护两份（桌面 + H5）；后端 DTO 变更时需要同步两处前端类型。
**规避**：后端 DTO 变更时在 PR 描述中明确注明"需同步 H5 types"。

---

## ADR-005：跨系统 Webhook 使用直接 HTTP 而非消息队列

**日期**：2026-05-09

**背景**：跨系统事件链（OA→CRM、CRM→WMS、MES→WMS 等）需要可靠的跨进程通信。

**方案比较**：
- 方案 A：直接 HTTP POST（复用现有 `UserSyncService` 的 HMAC 模式）
- 方案 B：引入消息队列（RabbitMQ / Redis Streams）
- 方案 C：Outbox 模式（同库写事件表，后台轮询发送）

**决策**：当前阶段选择方案 A（直接 HTTP），失败只记日志不阻塞主流程；未来流量增长后迁移至方案 C（Outbox）。

**理由**：
1. **当前规模**：6 个系统、开发/小团队使用场景，Webhook 频率极低（每天几十次），直接 HTTP 足够
2. **一致性模式统一**：已有 `UserSyncService` 的 HMAC Webhook 模式运行稳定，新链路复用同一模式，学习成本为零
3. **避免引入新中间件**：消息队列需要额外运维（docker-compose 加服务、监控、消息积压处理），当前收益不足以覆盖成本
4. **失败可接受**：业务场景下跨系统调用失败可人工补录（如发货信息），不需要强一致性保证

**迁移路径**（当需要时）：
```sql
-- Outbox 表（未来各系统各加一张）
CREATE TABLE outbox_event (
  id         BIGINT PRIMARY KEY AUTO_INCREMENT,
  event_type VARCHAR(100) NOT NULL,
  payload    TEXT NOT NULL,
  target_url VARCHAR(500) NOT NULL,
  status     VARCHAR(20) DEFAULT 'pending',  -- pending / sent / failed
  retry_count INT DEFAULT 0,
  created_at DATETIME NOT NULL,
  sent_at    DATETIME
);
```

---

## ADR-006：OA SLA 催办使用 BackgroundService 轮询，不用 Hangfire/Sundial

**日期**：2026-05-09

**背景**：OA 审批 SLA 超时催办需要定时扫描。

**决策**：使用 .NET 内置 `BackgroundService`，每 30 分钟轮询一次，不引入 Hangfire 或 Sundial。

**理由**：
1. **UMC 已有 Sundial** 用于复杂任务调度，但 OA 只需一个简单的定时扫描，不值得额外引入依赖
2. **BackgroundService 零依赖**：直接注册，Aspire Dashboard 可见其健康状态，够用
3. **30 分钟粒度足够**：SLA 最短是 4 小时（补卡审批），30 分钟轮询误差在可接受范围内
4. **测试友好**：`BackgroundService` 可以在测试中直接调用 `ExecuteAsync` 验证逻辑，无需 mock 调度框架

**注意**：如未来需要精确到分钟的 SLA 或需要管理界面（手动触发/暂停任务），迁移到 Sundial（UMC 已有参考实现）。
- `src/components/ui/sheet/index.ts` 移除了 `sheetVariants` cva 导出（Sheet side 改为内部 record 映射）

---

## ADR-007：SQLite Schema 模型驱动自愈（替代 EF Migrations）

**日期**：2026-05-11

**背景**：开发环境用 SQLite + `Database.EnsureCreated()`，不上 EF Migrations。但 `EnsureCreated` 只在 db 文件不存在时建表，**任何后续的「实体新增字段 / 新增表」都不会增量同步到已有的 db**，导致下次启动 INSERT 时报 `no such column: X` 或 `table Y has no column named Z`，Seeder 直接抛异常阻塞整个进程，并连带让 Aspire 编排里依赖它的其它子系统也起不来（典型表现：MES 崩溃 → UMC 实际未启动 → 前端登录 `/.well-known/openid-configuration` 一直 pending）。

P12 加 `mes_garment_team / mes_garment_schedule`、P17 给 `WorkflowNode` 加 `SkipCondition/CcUsersJson`、PLM 给 `Product` 加 `SeasonCode/StyleCode/Series` 时都踩过这个坑。

**决策**：在每个子系统的 DbSeeder 开头调用 `EnsureSchemaUpToDate(db)`，**通过反射 EF Core 模型与实际表结构做差异比对**，自动补齐：

- **表级**：用 `IRelationalDatabaseCreator.GenerateCreateScript()` 重新生成全部 CREATE 语句，用字符串替换把 `CREATE TABLE` / `CREATE INDEX` 统一改成 `CREATE ... IF NOT EXISTS` 后重放 → 已存在的跳过，新表自动建。
- **列级**：遍历 `db.Model.GetEntityTypes()` → `et.GetProperties()`，对每个属性算出列名后用 `PRAGMA table_info("table")` 查实际列；缺则 `ALTER TABLE ADD COLUMN`。SQL 类型从 `prop.GetColumnType()` 取，没有则按 CLR 类型推断（int/long/bool → INTEGER，decimal → decimal(18,4)，DateTime → TEXT 等）。主键列跳过（SQLite 不允许 ADD PRIMARY KEY）。
- 异常一律打日志吞掉，不让 Seeder 因为兜底失败而阻塞主流程；服装行业等扩展 Seed 块用 `try/catch` 包裹。

**理由**：

1. **不引入 EF Migrations**：迁移文件需要按时间序提交、回滚、code review，对仅在本地开发用 SQLite 的场景成本太高，且不解决"老开发者本地有旧 db 文件"的问题。
2. **模型驱动 = 一次性根治**：未来再加列不需要改 Seeder。第一版用硬编码列表（`("table", "col", "TEXT NULL")`），加完一波就要漏，所以重构为遍历 EF 模型。
3. **幂等 + 低风险**：`ADD COLUMN` 永远是可空或带默认值；`IF NOT EXISTS` 重放 CREATE 不会破坏现有数据。
4. **生产迁移路径**：本规则只在 SQLite 路径生效（`db.Database.IsRelational()` 守卫 + 实际仅 SQLite 用 `EnsureCreated`）；切 MySQL 时应启用 EF Migrations + dotnet ef database update，不走自愈路径。

**关键实现**（统一抽到每个 DbSeeder 内部，避免跨项目耦合）：

```csharp
foreach (var et in db.Model.GetEntityTypes())
{
    var table = et.GetTableName();
    if (string.IsNullOrEmpty(table) || !TableExists(conn, table)) continue;
    var existing = ReadColumns(conn, table);
    var storeId = StoreObjectIdentifier.Table(table, et.GetSchema());
    foreach (var prop in et.GetProperties())
    {
        var col = prop.GetColumnName(storeId);
        if (existing.Contains(col) || prop.IsPrimaryKey()) continue;
        var def = $"{prop.GetColumnType() ?? InferSqliteType(prop.ClrType)} " +
                  (prop.IsNullable ? "NULL" : $"NOT NULL DEFAULT {DefaultValueLiteral(prop.ClrType)}");
        conn.Execute($"ALTER TABLE \"{table}\" ADD COLUMN \"{col}\" {def}");
    }
}
```

**已实装**：`OctopusOA/.../OaDbSeeder.cs`、`OctopusPLM/.../PlmDbSeeder.cs`、`OctopusMES/.../MesDbSeeder.cs`。

**注意**：

- 该机制只覆盖 ADD COLUMN / CREATE TABLE 等"加法"。**类型变更、列重命名、删除列、添加 NOT NULL 约束**仍然需要手动迁移或删 db 重建。
- 若实体改了主键结构，自愈会跳过（主键不能 ALTER ADD）；这种情况只能删 db 文件让 EnsureCreated 重建。
- 升级到 MySQL/Postgres 时改走 EF Migrations，自愈逻辑应被 `if (!provider == "Sqlite") return` 守卫。

---

## ADR-008：零售域走「独立服务 + tenant_id 只落零售库」，不把多租户侵入 UMC

**日期**：2026-10-12

**背景**：零售行业需要「店铺邀请人员加入」。既有 UMC 是典型 RuoYi 式**单租户** RBAC（`sys_user` / `sys_dept` / `sys_role` + 四张关联表），数据权限 `Role.DataScope` 字段存在但**只在 `RoleController` 里存，查询侧从未落地**——`UserController.GetList` 的数据权限实际只是前端传 `deptId` 参数过滤（`UserDataPermissionTests` 的类注释自认「Step 2 阶段通过 deptId 参数模拟」）。因此店长调 `/api/system/user/list` 不带参数即可看到全平台用户。

同时 `MINIAPP-PLATFORM.md` §3.2 明确论证过「不用多 Realm，多 Realm 是给多租户 SaaS 用的」，平台定位是**企业内部 ERP**。

**决策**：零售域采用 **B + C** 路线，并以一条硬边界限定作用域——

| 库 | 是否加 `tenant_id` | 角色 |
|---|---|---|
| **retail_db**（新增 `OctopusRetail`） | ✅ 全部业务表加 | 多租户隔离域 |
| `octopus_umc.db` | ❌ **不加** | 平台主数据后台，保持单租户 |
| OA / CRM / WMS / MES / FIN / HRM / OMS | ❌ 不加 | 与本决策无关 |

- B：独立服务 + 独立 DB，身份完全来自 Keycloak JWT，**不读 UMC 数据库、不调 UMC API**（沿用 `MINIAPP-PLATFORM.md` 原则 3）。
- C：租户隔离只在 retail_db 内用 `tenant_id` 实现；租户目录用独立的 `retail_tenant` 表，**不借用 UMC 的 `sys_dept`**（关店/并店/加盟商变更 ≠ 组织架构调整）。
- 用户外键一律用 Keycloak `sub`（UUID），不存 UMC `UserId`。

**理由**：

1. **给 UMC 加租户维度会污染 9 个子系统**。`UserSyncService` / `DeptSyncService` 会把用户与部门变更推送到 OA（`/api/users/sync`、`/api/contact/dept/sync`）等下游，UMC 一旦租户化，所有同步 payload 与鉴权都要改。
2. **C 的诉求不需要动 UMC**。「租户数据互不可见」在 retail_db 内部即可 100% 满足。
3. **B 与 C 不冲突**，前提就是本条边界：`tenant_id` 落在零售自己的库，而不是平台主数据库。

**关键实现**：

- `OctopusRetail/src/OctopusRetail.Api`，端口 5010；前端 `OctopusRetail/web/OctopusRetail.Web`，端口 5186。
- `RetailDbContext` 对实现 `ITenantScoped` 的实体施加 **fail-closed 全局过滤器**：`CurrentTenantId != null && e.TenantId == CurrentTenantId`。未解析租户时匹配**空集**而非全集。
- 过滤器读的是 DbContext 的**实例属性**而非直接捕获 Scoped 服务——EF Core 模型是跨实例缓存的，捕获 Scoped 服务会固化首个请求的租户、造成跨请求泄漏。已有测试用三个独立上下文验证隔离。
- `TenantFilterGuard` 在建模时校验：任何实现 `ITenantScoped` 却未配置过滤器的实体**直接抛异常**，让「新增业务表忘记隔离」在启动期暴露。
- 控制面表 `retail_tenant` / `retail_membership` 刻意不带过滤器（列「我的 workspace」必须跨租户），其查询一律强制约束 `KcSub`。

**注意**：`retail_invite` 带租户过滤器（A 店看不到 B 店邀请）；接受邀请是唯一跨租户路径，显式使用 `IgnoreQueryFilters()` 并按 `TokenHash` 精确定位。

---

## ADR-009：一人多租户的身份切换 = token 携带租户全集 + `X-Tenant-Id` 请求头服务端校验

**日期**：2026-10-12

**背景**：零售连锁中一人常属于多个门店（店长巡店、区域经理管多店、导购跨店支援），需要「workspace 切换」。但既有 Keycloak 集成的 claims 是**单值**的：`KeycloakSyncService.BuildUserAttributes()` 只取 `IsPrimary == true` 的那条 `UserDept`（种子里 zhangsan 本身就是一人两部门），且 `octopus-realm.json` 里 `primary_dept_id` / `company_name` 都是单值 `oidc-usermodel-attribute-mapper`。Keycloak Groups 同步在 M2 清单里一直未勾选，realm 文件连 `groups` 段都没有。

同时 Keycloak **无法表达「张三在 A 店是店长、在 B 店是店员」**：realm role 是全局的；client role 是 per-client（每租户一 client 运维爆炸）；group role mapping 技术上可行但 N 店 × M 角色 = N×M 个 role，命名/审计/回收不可控。

**决策**：

1. **三层概念严格分开**：Tenant（`retail_tenant`，持久化）/ Membership（`retail_membership`，user × tenant × 角色）/ **ActiveTenant（请求级运行时状态，绝不是 DB 字段）**。所谓「workspace」= Membership 的数据 + ActiveTenant 的载体。
2. **Keycloak 只回答「你是谁」和「你能进哪些租户」**；「你在该租户里是什么角色」一律由 `retail_membership` 判定，**不放进 JWT**。附带好处：改角色立即生效，无需等 token 过期。
3. **身份切换不重新签发 token**：token 携带可进入的租户全集（`groups` claim，形如 `/tenants/t_shanghai_001`），切换时只改请求头 `X-Tenant-Id`；服务端中间件校验 `X-Tenant-Id ∈ token.groups`。
4. **默认拒绝**：未标记 `[AllowCrossTenant]` 且需要认证的端点，缺失 `X-Tenant-Id` → 400，不在 token 租户集合内 → 403。

**理由**：

1. **前端本地 state 不能作为授权依据**。若 active tenant 只存在 Pinia/localStorage，后端每个接口都得回业务库校验「这人真属于这个租户吗」，等于丢掉 JWT 自包含的意义，且**只要漏一个接口就是跨租户越权**。
2. **`X-Tenant-Id` 是可伪造的请求头**，只有对照 token 中不可伪造的 `groups` 校验后才有意义——这是本方案安全性的核心。校验集中在**一个中间件**里，不散落到各 Controller。
3. **不选 Token Exchange（RFC 8693）**：需启用 feature、配置 client-to-client 权限，注入自定义 claim 的行为随版本变化；且 `accessTokenLifespan` 为 3600s，每次切店都换 token 的往返与签发成本不划算。请求头方案安全性不打折、成本大幅下降。
4. **UMC 侧的 claim 语义保持纯粹**：M0 只补了多值 `dept_ids`（全部部门，修复「兼职部门被静默丢弃」）与 Group Membership mapper 产出的 `groups` claim，**没有**把零售概念（tenant）写进 UMC 的同步属性。

**关键实现**：

- M0（UMC）：`KeycloakGroupService` 提供组原语与**受前缀保护的 reconcile**（`/tenants` 只匹配 `/tenants` 与 `/tenants/a`，不匹配 `/tenantsX`——防止一次同步清空用户的 ERP 组织组）；`BuildUserAttributes` 新增多值 `dept_ids`；realm 加 `groups` 段 + `dept_ids` mapper + Group Membership mapper。
- M1（Retail）：`TenantResolutionMiddleware`（越权防护第一道）、`ITenantContext`、fail-closed 全局过滤器（第二道）、`TenantFilterGuard`。
- M3：`retail-invite-bot` 独立 service account（**Keycloak service account 权限是 realm 级的，做不到「上海店的 bot 只能管上海店的 group」，因此凭据绝不下发给前端或租户**，建号与加组一律由服务端代理）。
- 双写一致性：业务库是事实来源，Keycloak group 是下游投影；`GroupSyncReconciler` 扫描 `KcSyncState ∈ {null, pending, failed}` 重放，`GroupSyncWorker` 每 60s 一轮。Keycloak 不可用时保持 `pending` 而**不是**误标 `synced`。

**注意**：

- 本决策**限定在零售域**。它不推翻 `MINIAPP-PLATFORM.md` §3.2：仍用单 Realm，租户用 Groups 表达，不引入多 Realm。
- `X-Tenant-Id` 的校验**只在中间件里**。新增需要租户上下文的端点时不要自己解析该头。
- 新增业务表请实现 `ITenantScoped` 并配置 `HasQueryFilter`，否则启动即失败（这是刻意设计）。

---

## ADR-010：UMC 是唯一用户主数据源（**修正 ADR-008 的错误部分**）

**日期**：2026-10-18

**背景**：ADR-008 定下了「零售用户不进 UMC」的边界，理由是「给 UMC 加租户维度会污染 9 个子系统」。
这个理由**把两件事混为一谈了**：

| 问题 | 是否真的会污染下游 |
|---|---|
| 给 UMC 业务表加 `tenant_id`（做多租户） | 会 —— 这条判断是对的 |
| 零售的用户在不在 `sys_user` | **不会** |

结果：运营在 `5173/system/user` 查不到 `feiwoniu001`，而他明明能登录零售域；
既不能停用、也不能改密，只能去 Keycloak 控制台手工操作。
这与 `CLAUDE.md` 写明的定位（「统一用户中心 IdP，**管理全平台用户**」）直接矛盾——
一个统一用户中心里查不到能登录系统的人，「统一」是假的。

**决策**：UMC 是**唯一用户主数据源**，所有能登录的用户（含零售客户/店员）都在 `sys_user`。
但**仍然不给 UMC 业务表加 `tenant_id`**。

### 保留（ADR-008 正确的部分）

- `tenant_id` 只落零售库，租户隔离在 `retail_db` 内部完成
- 租户目录用 `retail_tenant`，不借用 `sys_dept`
- 用户外键以 Keycloak `sub` 为准

### 推翻（ADR-008 错误的部分）

- ~~零售用户不进 UMC~~ → **全部进 UMC**
- ~~零售注册直连 Keycloak 建号~~ → **改为调 UMC 内部接口建号**

### 实现

| 环节 | 做法 |
|---|---|
| `sys_user` | 新增 `UserType`（employee/customer/partner）、`Source`（umc/retail）、`KcSub` |
| 下游同步 | `UserSyncService` **只推 `employee`**，客户永不进 OA 通讯录 —— 用一次过滤解决当初的顾虑 |
| 建号 | `POST /api/internal/users/retail`（`X-Internal-Secret`），UMC 建号后同步 Keycloak 并回写 `kc_sub` |
| 存量 | `POST /api/internal/users/adopt` 幂等接管已在 Keycloak 的账号（随机密码，管理员重置后才可登 UMC） |
| 零售域 | `InviteService.RegisterAsync` 调 `UmcUserClient`，不再直连 Keycloak 建号 |

### 为什么客户进 UMC 也不会污染下游

关键就一行：

```csharp
if (payload.UserType != "employee") return;   // UserSyncService
```

客户、外部协作方可以安然存在于 `sys_user`，OA 通讯录只会收到员工。

**代价**：零售注册现在依赖 UMC 可用。UMC 不可达时注册会失败并提示，
但**不会**留下「UMC 有、Keycloak 没有」的半成品——建号失败会回滚。

**测试**：`InternalUserApiTests`（5 个）、零售注册用例改写为 UMC 契约。
零售 110/110，UMC 157/157。

---

## ADR-011：存量真实姓名匿名化 + 轻量数据迁移机制

**日期**：2026-10-20

**背景**：早期演示种子把 6 位真实员工的姓名、拼音账号与邮箱直接写进了
UMC 的 `sys_user` / `sys_dept` 和 MES 的 `mes_sync_user` / `mes_garment_team`。
这些数据会出现在 5173 用户管理、演示与截图中，属于个人信息泄露。

**难点**：两个子系统都用 `EnsureCreated()` + 幂等 Seeder，**没有 EF Migrations**。
Seeder 只在空库插入，**改种子不会修正已有数据的库**——必须单独的、只执行一次的订正机制。

**决策**：

### 1. 匿名化规则

| 维度 | 规则 |
|---|---|
| `sys_user.UserName` / `mes_sync_user.UserName` | `worker{UserId}` |
| `sys_user.NickName` / `mes_sync_user.NickName` | `员工{UserId}` |
| `Email` | `worker{UserId}@example.invalid` |
| `sys_dept.DeptName`（106/107） | `L组-104` / `L组-105` |
| `mes_garment_team.TeamCode`（L 组） | `L-{UmcUserId}` |
| `mes_garment_team.TeamName` / `Leader` | `{GroupLabel}-员工{UmcUserId}` / `员工{UmcUserId}` |

**为什么是稳定工号而不是随机串**：`leaderToUmcUserId` / `codeToDeptId` 依赖
`UmcUserId` 做关联，随机化会断链；每次随机会破坏测试与截图的可复现性。
匿名性由「不含姓名/PII」保证，与是否随机无关。

### 2. 迁移脚本只按 id 匹配，仓库不残留 PII

两个 SQL 都只写 `WHERE UserId = 100` / `WHERE LeaderUmcUserId = 104`，
**不出现任何真实姓名**。`TeamName` 用 `GroupLabel || '-员工' || LeaderUmcUserId`
拼装，同样不需要原姓名。否则"清理 PII 的脚本"自己就变成泄漏源。

### 3. 迁移执行器（UMC + MES 各一份）

- 脚本放 `Persistence/Migrations/*.sql`，以 **EmbeddedResource** 编译进程序集
  （避免运行目录缺文件导致升级静默失效）
- 文件名即迁移 Id，按名排序执行，执行后记入 `sys_data_migration` / `mes_data_migration`
- 启动时在 `EnsureCreated + Seed` 之后运行；失败记日志但不阻塞启动
- 脚本必须幂等（全部 UPDATE 以 id 为条件）
- 提供 `throwOnError` 开关供测试暴露失败——否则异常会被 catch 静默吞掉

### 4. ⚠️ 踩坑：`ExecuteSqlRaw` 会无条件做 `string.Format`

脚本注释里出现 `{UmcUserId}` 这类花括号时，EF 会当成格式说明符并抛
`FormatException: Expected an ASCII digit`，且被 catch 吞掉后表现为"迁移什么都没做"。
**修法**：改用原始 ADO（`DbCommand.ExecuteNonQuery`），既能规避格式化，也支持多语句。

### 5. Keycloak 需单独处理

Keycloak 是独立存储，DB 迁移管不到。提供
`scripts/dev-anonymize-keycloak-users.sh`，**按 `attributes.octopus_user_id` 定位**
（不依赖真实用户名），改 `username/email/firstName`。
改名不影响 `sub`，因此 UMC 的 `KcSub` 与零售域成员关系保持有效。

### 6. 历史登录日志

`sys_logininfor` 里残留的旧用户名属于历史审计流水，未写入迁移脚本
（迁移里没有名字就无法定位）。本次以一次性清理处理；其它环境如有需要，
应单独评估「审计日志保留 vs PII 清理」的取舍。

**实测**：UMC 8 行、MES 14 行受影响；两个库全表扫描 12 个姓名/账号关键字
**0 命中**。测试：MES **117/117**、UMC **157/157**。
