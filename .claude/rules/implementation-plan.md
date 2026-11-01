# 分阶段实施计划

> 各 Phase 按顺序推进，每个 Phase 完成后运行全量测试验证。

---

## Phase 0：脚手架搭建

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

dotnet sln add src/**/*.csproj tests/**/*.csproj

dotnet add src/OctopusUMC.Infrastructure reference src/OctopusUMC.Core
dotnet add src/OctopusUMC.Api reference src/OctopusUMC.Infrastructure
dotnet add src/OctopusUMC.Api reference src/OctopusUMC.Core

# OA
mkdir -p OctopusOA/src OctopusOA/tests OctopusOA/web
dotnet new webapi -n OctopusOA.Api  -o OctopusOA/src/OctopusOA.Api
dotnet new xunit  -n OctopusOA.Api.Tests -o OctopusOA/tests/OctopusOA.Api.Tests

# 前端：基于 pure-admin-thin 克隆
cd OctopusUMC/web
git clone https://github.com/pure-admin/pure-admin-thin OctopusUMC.Web --depth=1
cd OctopusUMC.Web && rm -rf .git && npm install

cd ../../OctopusOA/web
git clone https://github.com/pure-admin/pure-admin-thin OctopusOA.Web --depth=1
cd OctopusOA.Web && rm -rf .git && npm install
```

克隆后初始化：
1. 修改 `package.json` 的 `name`
2. 修改 `public/platform-config.json`：`Title`
3. 修改 `src/config/index.ts`：配置后端 API BaseURL

**验收**：四项目均能启动，`dotnet test` 0 失败，`npm run dev` 显示 pure-admin 默认界面。

---

## Phase 1：OIDC 身份核心

**目标**：UMC 成为可用的 OIDC 身份提供商，用户能注册/登录。

**TDD 顺序**：先写 AccountController 测试 → 写 UseCase → 写 Repository → 写 Controller

### 1.1 领域实体（Core）

```csharp
// src/OctopusUMC.Core/Domain/Entities/
User     { UserId, UserName, NickName, Email, PasswordHash, PhoneNumber, Sex, Avatar, Status, CreateTime }
Role     { RoleId, RoleName, RoleKey, RoleSort, DataScope, Status, CreateTime }
Dept     { DeptId, ParentId, DeptName, OrderNum, Status, CreateTime }
Post     { PostId, PostName, PostCode, PostSort, Status, CreateTime }
Menu     { MenuId, ParentId, MenuName, MenuType, Path, Component, Permission, Icon, OrderNum, Status }
Dict     { DictId/DictCode, DictType, DictLabel, DictValue, DictSort, Status }

// 多对多关联表
UserDept { UserId, DeptId, PostId?, IsPrimary }
UserRole { UserId, RoleId }
RoleMenu { RoleId, MenuId }
RoleDept { RoleId, DeptId }
```

### 1.2 核心 NuGet 包

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

### 1.3 OpenIddict 配置

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

        o.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();

        o.UseAspNetCore()
         .EnableAuthorizationEndpointPassthrough()
         .EnableTokenEndpointPassthrough()
         .EnableLogoutEndpointPassthrough()
         .EnableUserinfoEndpointPassthrough();
    })
    .AddValidation(o => { o.UseLocalServer(); o.UseAspNetCore(); });
```

### 1.4 中间件注册顺序（严格遵守）

```csharp
app.UseCors("OctopusPolicy");      // 必须第一
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

**验收**：注册 → 登录 → Cookie 会话 → `/api/account/me` 返回用户信息。

---

## Phase 2：SSO 与单点登出

**目标**：OA 通过 UMC OIDC 登录，退出时同步注销 UMC 会话。

### 2.1 注册 OA 客户端（UMC 种子数据）

```csharp
await manager.CreateAsync(new OpenIddictApplicationDescriptor
{
    ClientId = "octopus-oa-web",
    ClientType = OpenIddictConstants.ClientTypes.Public,
    DisplayName = "OctopusOA Web",
    RedirectUris          = { new Uri("http://localhost:5174/callback") },
    PostLogoutRedirectUris = { new Uri("http://localhost:5174") },
    Permissions = { /* 全部必要权限，参见 api-conventions.md */ }
});
```

### 2.2 OA.Api JWT 验证

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = "https://localhost:7001/";
        o.Audience = "octopus-oa-web";
        o.RequireHttpsMetadata = false; // 仅开发
    });
```

**验收**：
- OA 登录 → 跳转 UMC 登录页 → 跳回 OA 显示用户名
- OA 退出 → UMC Cookie 会话被清除

---

## Phase 3：权限体系（优先于用户管理 UI）

**目标**：搭建完整的 RBAC + 数据权限骨架，所有后续功能基于此构建。

### 3.1 权限模型

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

### 3.2 权限校验实现

```csharp
[HasPermission("system:user:add")]
public async Task<IActionResult> CreateUser(...)
```

### 3.3 前端权限集成

```typescript
interface AuthState {
  user: UserInfo | null
  permissions: string[]   // ["system:user:add", "system:role:edit", ...]
  roles: string[]
  menus: MenuTree[]
}
// 按钮显示控制：v-permission="'system:user:add'"
```

**验收**：不同角色看到不同菜单，无权限用户访问受保护接口返回 403，数据权限过滤生效。

---

## Phase 4：用户管理完整后台

**目标**：实现完整的 RBAC 管理后台 UI，达到市面主流后台管理系统水准。

### 4.1 接口清单（UMC）

**用户管理**
```
GET/POST/PUT/DELETE /api/system/user/...
PUT    /api/system/user/status           启用/禁用
PUT    /api/system/user/resetPwd         重置密码
GET    /api/system/user/authRole/{id}    用户已绑定角色
PUT    /api/system/user/authRole         用户绑定角色
PUT    /api/system/user/profile/...      个人资料/密码/头像
GET    /api/system/user/export           导出 Excel
POST   /api/system/user/import           批量导入
```

**机构/职位/菜单/角色/字典**
```
GET/POST/PUT/DELETE /api/system/dept/...
GET/POST/PUT/DELETE /api/system/post/...
GET/POST/PUT/DELETE /api/system/menu/...
GET/POST/PUT/DELETE /api/system/role/...
GET/POST/PUT/DELETE /api/system/dict/type/...
GET/POST/PUT/DELETE /api/system/dict/data/...
```

### 4.2 前端页面清单（pure-admin 目录约定）

```
src/views/
├── dashboard/workbench/index.vue
├── system/user/index.vue + form.vue + role.vue
├── system/dept/index.vue
├── system/role/index.vue + menu.vue + scope.vue
├── system/menu/index.vue
├── system/post/index.vue
├── system/dict/index.vue + data.vue
└── profile/index.vue
```

---

## Phase 5：扩展功能

各模块按独立特性开发，互不阻塞，可并行推进。

### 5.1 访问日志 & 操作日志

```
GET/DELETE /api/monitor/logininfor/...
GET/DELETE /api/monitor/operlog/...
```

AOP 实现：Action Filter 拦截 `[Log("操作描述")]`，异步写日志。

### 5.2 在线用户（SignalR）

```csharp
// OnlineUserHub：连接时记录 / 断开时移除
// 强制下线：服务端主动发送 "ForceLogout" 事件
```

```
GET    /api/monitor/online/list
DELETE /api/monitor/online/{token}
```

### 5.3 公告管理（SignalR 实时推送）

```
GET/POST/PUT/DELETE /api/system/notice/...
```

发布后 Hub 推送给所有在线用户。

### 5.4 文件管理

策略模式，实现 `IStorageProvider`：`LocalStorageProvider` / `AliyunOssStorageProvider` / `TencentCosStorageProvider`

```
POST /api/common/upload
POST /api/common/uploadOss
GET/DELETE /api/system/oss/...
```

### 5.5 任务调度（Sundial）

```bash
dotnet add package Sundial
```

```
GET/POST/PUT/DELETE /api/monitor/job/...
PUT /api/monitor/job/run|pause|resume
GET /api/monitor/jobLog/list
```

### 5.6 服务监控

```
GET /api/monitor/server    CPU、内存、磁盘、.NET Runtime 信息
```

### 5.7 系统配置

```
GET/POST/PUT/DELETE /api/system/config/...
GET /api/system/config/key/{configKey}
PUT /api/system/config/refreshCache
```

### 5.8 导入导出

```bash
dotnet add package Magicodes.IE.Excel
dotnet add package Magicodes.IE.Pdf
```

通用模式：`GET /api/{module}/export` + `POST /api/{module}/import`

### 5.9 限流控制

```bash
dotnet add package AspNetCoreRateLimit
```

在 `appsettings.json` 中按 IP / ClientId / 接口路径配置速率。

### 5.10 开放授权（微信等 OAuth 2.0）

在 OpenIddict 中注册外部身份提供商，处理微信 OAuth 回调，创建或绑定本地账户。
