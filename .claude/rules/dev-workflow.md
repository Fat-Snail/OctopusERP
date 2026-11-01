# 开发流程 — 六步闸门制（完整细节）

> 铁律：每一步都有明确的通过标准（Gate），未全部通过禁止进入下一步。

```
Step 1 前端样板  →  Step 2 后端骨架  →  Step 3 接口确认
                                              ↓
Step 6 功能验收  ←  Step 5 SSO打通  ←  Step 4 前后端联调
```

---

## Step 1：前端样板（仅 UI，无后端依赖）

**目标**：两个项目的完整前端壳子跑通，全部使用 Mock 数据，不依赖任何后端接口。

### 1.1 Mock 方案

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

### 1.2 UMC.Web 需完成的页面

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

### 1.3 OA.Web 需完成的页面

| 页面 | 路由 | Mock 数据 |
|---|---|---|
| 登录/回调页 | `/login`、`/callback` | Mock OIDC 流程（点击登录直接跳回） |
| 首页 | `/` | 显示 Mock 用户名、公告列表 |
| 个人中心 | `/profile` | 从 Mock Token Claims 展示用户信息 |

### Step 1 通过标准（Gate 1）

- [ ] `npm run dev` 两个项目均无报错启动
- [ ] `npx tsc --noEmit` 零 TypeScript 类型错误
- [ ] 所有页面可正常导航，无白屏、无控制台报错
- [ ] Mock 数据正确渲染（表格有数据、树形结构正常展开）
- [ ] 登录/登出流程 UI 完整（不需要真实认证）
- [ ] **确认通过后方可进入 Step 2**

---

## Step 2：后端骨架（内存模拟，不连数据库）

**目标**：两个项目后端 API 跑通，全部用硬编码内存数据响应，不连 MySQL，不接 OpenIddict。

### 2.1 持久化方式（SQLite）

使用 **SQLite 文件数据库**（开发）+ **SQLite 内存模式**（测试）。

```bash
# 开发模式：SQLite 文件
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

> **种子数据变更时**：删除 `src/OctopusUMC.Api/octopus_umc.db` 后重新启动 API。

### 2.2 UMC.Api 需完成的接口

**用户管理**
```
GET    /api/system/user/list             分页列表（支持按用户名/手机号/状态筛选）
GET    /api/system/user/{id}             详情（deptId/deptName 始终返回主部门 IsPrimary=true）
GET    /api/system/user/{userId}/depts   用户所属的全部部门（含兼职/多公司），返回 IsPrimary 标志
POST   /api/system/user                  新增
PUT    /api/system/user                  修改
DELETE /api/system/user/{ids}            删除（批量）
PUT    /api/system/user/status           启用/禁用
PUT    /api/system/user/resetPwd         重置密码
```

**机构/职位/菜单/角色/字典管理**
```
GET/POST/PUT/DELETE /api/system/dept/...
GET/POST/PUT/DELETE /api/system/post/...
GET/POST/PUT/DELETE /api/system/menu/...
GET/POST/PUT/DELETE /api/system/role/...
GET/POST/PUT/DELETE /api/system/dict/...

POST /api/system/role/menu    角色绑定菜单
POST /api/system/role/dept    角色绑定数据权限范围
```

**账户接口**
```
POST /api/account/login   → Cookie
POST /api/account/logout  → 清除 Cookie
GET  /api/account/me      → 用户信息
```

### 2.3 OA.Api 需完成的接口

```
GET    /api/me                       返回当前用户信息（从假 Token 解析）
POST   /api/users/sync               接收 UMC 推送的用户同步数据
POST   /api/auth/backchannel-logout  接收 UMC 的登出通知
```

### Step 2 通过标准（Gate 2）

- [ ] 所有接口在 Swagger UI 可见，路径与上方清单一致
- [ ] 每个接口手动调用返回合理的 Mock 数据（非 500）
- [ ] 增删改查逻辑在内存中闭环（新增后查询能看到）
- [ ] 统一响应格式 `{ code, msg, data }` 所有接口一致
- [ ] **确认通过后方可进入 Step 3**

### Step 2 验证命令

```bash
cd OctopusUMC
export PATH="$PATH:/usr/local/share/dotnet"
dotnet test
# 预期：已通过! - 失败: 0，通过: 79，总计: 79

cd ../OctopusOA
dotnet test
# 预期：已通过! - 失败: 0，通过: 6，总计: 6
```

**运行业务流程日志：**
```bash
cd OctopusUMC
dotnet test --filter "Flow" --logger "console;verbosity=detailed"
```

5 条 Flow 日志覆盖：用户管理链路、角色权限链路、字典管理链路、用户状态链路、多公司数据权限链路。

**测试文件对应关系：**

| 测试文件 | 测试数 | 覆盖模块 |
|---|---|---|
| `AccountControllerTests.cs` | 6 | 登录/登出/权限返回 |
| `UserControllerTests.cs` | 9 | 用户 CRUD + 状态/密码操作 |
| `DeptControllerTests.cs` | 7 | 部门 CRUD + 树形结构 + 删除保护 |
| `PostControllerTests.cs` | 10 | 职位 CRUD + 编码唯一性 + 批量删除 |
| `RoleControllerTests.cs` | 13 | 角色 CRUD + 绑定菜单 + 五种数据权限范围 |
| `UserDataPermissionTests.cs` | 16 | 按部门过滤用户列表 + 角色绑定 + 菜单权限差异 |
| `BusinessFlowTests.cs` | 4 | 端到端业务流程 Flow 1–4 |
| `MultiCompanyTests.cs` | 6 | 多公司场景：跨公司兼职 + 数据隔离 |

---

## Step 3：接口层确认（Swagger + DTO 对齐）

**目标**：逐一核对接口契约，确保后端 DTO 与前端 TypeScript 类型完全对齐。

### 3.1 DTO 对齐检查清单

```
后端 C# DTO  ←→  Swagger JSON Schema  ←→  前端 TypeScript interface
```

- [ ] 字段名（camelCase 序列化后）与前端 TypeScript 字段名完全一致
- [ ] 可空字段在 DTO 中标 `?`，TypeScript 中对应 `T | null`
- [ ] 枚举类型：后端序列化为数字还是字符串，前端对应 union type
- [ ] 分页接口返回 `{ rows: T[], total: number }` 结构一致
- [ ] 日期字段统一为 ISO 8601 字符串

### Step 3 通过标准（Gate 3）

- [x] Swagger 可正常访问，所有接口有文档注释
- [x] 每个接口的 Request/Response Schema 与前端 TypeScript 类型零差异
- [x] 业务流程全部由集成测试覆盖（73 个测试全部通过）
- [x] **Step 3 已通过，可进入 Step 4**

### Step 3 对齐修正记录

**新增前端类型（`src/api/types.ts`）：**
- `LoginRequest` — (userName, password, rememberMe?)
- `LoginResponse` — (userId, userName, nickName, avatar, roles[], permissions[])

**架构修正：**
- `src/utils/http.ts`：移除 Bearer Token，改用 `withCredentials: true`（Cookie 认证）
- `src/store/modules/user.ts`：重写为 Cookie 认证模式，移除 localStorage token 逻辑
- `src/router/index.ts`：路由守卫改用 Pinia store 判断登录状态

---

## Step 4：前后端联调（替换 Mock，接真实接口）

**目标**：前端 Mock 全部替换为真实后端调用，权限体系在前后端联动下正确生效。

### 4.1 替换顺序

1. 移除 MSW Mock，开启真实 Axios 请求
2. 登录接口联调（UMC.Web → `/api/account/login`）
3. 动态菜单路由联调（登录后调 `/api/system/menu/tree`）
4. 按模块逐一联调：用户 → 机构 → 职位 → 菜单 → 角色 → 字典

### Step 4 通过标准（Gate 4）

- [ ] 前端零 Mock 请求，全部走真实接口
- [ ] 登录/登出流程正常，Cookie 正确设置和清除
- [ ] 动态菜单路由按角色正确渲染
- [ ] 菜单权限、按钮权限、数据权限正确生效
- [ ] **确认通过后方可进入 Step 5**

---

## Step 5：SSO 打通与用户数据同步

**目标**：OA 通过 UMC OIDC 实现 SSO，用户数据在两系统间正确同步，退出时会话联动注销。

### 5.1 用户数据架构

```
UMC（主库）                     OA（同步缓存）
─────────────────               ────────────────────
Users 表（权威数据源）    →同步→  SyncUsers 表（只读缓存）
  - 用户名、邮件、手机            - UMC 用户 ID（外键引用）
  - 密码哈希                      - 用户名、邮件、头像
  - 状态、角色                     - OA 本地角色/权限
```

OA 的 `SyncUsers` 表**不存密码**，只做展示和 OA 内部授权。

### 5.2 UMC 用户变更同步到 OA

UMC 修改用户信息时，POST Webhook 到 OA `/api/users/sync`（携带 HMAC-SHA256 签名）：
```
Header: X-Sync-Signature: sha256=<HMAC-SHA256(body, sharedSecret)>
```

触发同步的 UMC 操作：修改用户基础信息、修改状态（启用/禁用）、删除用户。

### 5.3 单点登出（Back-Channel Logout）

```
OA 点击退出 → userManager.signoutRedirect()
  → UMC 清除 Cookie
  → UMC POST /api/auth/backchannel-logout（logout_token）到 OA
  → OA 验证 logout_token → 撤销该用户的 OA Token/会话
```

### 5.4 OA.Web OIDC 客户端配置

```typescript
export const userManager = new UserManager({
  authority: 'https://localhost:7001',
  client_id: 'octopus-oa-web',
  redirect_uri: 'http://localhost:5174/callback',
  post_logout_redirect_uri: 'http://localhost:5174',
  response_type: 'code',
  scope: 'openid profile email roles offline_access',
  automaticSilentRenew: true,
  accessTokenExpiringNotificationTimeInSeconds: 60,
})
```

### Step 5 通过标准（Gate 5）

- [ ] OA.Web 点击登录 → 跳转 UMC 登录页 → 登录后跳回 OA 显示用户名
- [ ] OA 首次登录自动在 `SyncUsers` 创建用户记录
- [ ] UMC 修改用户信息 → OA `SyncUsers` 自动更新（≤ 3 秒）
- [ ] OA 退出 → UMC Cookie 会话已清除
- [ ] UMC 退出 → OA 会话通过 Back-Channel Logout 联动注销
- [ ] Webhook 签名验证：伪造请求返回 401
- [ ] **确认通过后方可进入 Step 6**

---

## Step 6：功能验收

**目标**：两个系统完整功能的端到端验收，达到生产环境交付标准。

### 6.1 UMC 验收清单

- [ ] 菜单权限：Admin 看全部菜单，普通用户按角色看子集
- [ ] 按钮权限：增删改按钮按权限标识显示/隐藏
- [ ] 数据权限：五种范围各自过滤正确
- [ ] 用户 CRUD 完整（新增/编辑/删除/批量删除/启用禁用/重置密码）
- [ ] 各模块增删改查闭环

### 6.2 OA 验收清单

- [ ] SSO 登录 / 登出完整闭环
- [ ] SyncUsers 缓存与 UMC 主库一致
- [ ] OA 本地权限配置不影响 UMC 数据

### 6.3 集成验收清单

- [ ] `dotnet test` 全部通过，零失败
- [ ] `npx tsc --noEmit` 零类型错误
- [ ] 关键接口有 Swagger 文档和入参校验
- [ ] 异常处理：接口异常返回统一格式，不暴露堆栈信息
