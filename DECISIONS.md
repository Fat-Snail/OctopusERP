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
- `src/components/ui/sheet/index.ts` 移除了 `sheetVariants` cva 导出（Sheet side 改为内部 record 映射）
