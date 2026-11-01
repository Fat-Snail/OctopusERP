# OctopusERP — 小程序平台架构方案

> **目标**：在 ERP 之外构建一个小程序平台，每个小程序：
> - **独立域名**（如 `todo.miniapp.octopus.com`）
> - **独立容器**运行
> - **不读 UMC 数据库、不调 UMC API**
> - 通过 **Keycloak** 实现身份与数据隔离
>
> **可行性结论**：**完全可行**。本文给出三阶段渐进式实施方案。

---

## 一、整体架构

```
                          ┌─────────────────────────────────────────┐
                          │       Keycloak  (IdP 单一身份源)         │
                          │       Realm: octopus                    │
                          │   ┌──────────────────────────────────┐  │
                          │   │  Users  (zhangsan, lisi, ...)    │  │
                          │   │  Groups (dept tree)              │  │
                          │   │  Roles  (realm / per-client)     │  │
                          │   │  Clients:                        │  │
                          │   │   - octopus-portal               │  │
                          │   │   - octopus-oa, octopus-crm...   │  │
                          │   │   - miniapp-todo                 │  │
                          │   │   - miniapp-board                │  │
                          │   │   - miniapp-XXX (按需新增)        │  │
                          │   │  Protocol Mappers (custom claims)│  │
                          │   └──────────────────────────────────┘  │
                          └─────────────────────────────────────────┘
                                            ▲
                          ┌─────────────────┼─────────────────┐
                          │     OIDC (Auth Code + PKCE)         │
            ┌─────────────┴───┐  ┌───────────┴────────┐  ┌────┴────────────────┐
            │ portal.oct.com  │  │ todo.miniapp.       │  │ board.miniapp.      │
            │  (ERP 主门户)   │  │  octopus.com        │  │  octopus.com        │
            │                 │  │                     │  │                     │
            │ ┌────────────┐  │  │ ┌──────────────┐    │  │ ┌──────────────┐    │
            │ │ OA/CRM/... │  │  │ │ miniapp-todo │    │  │ │ miniapp-board│    │
            │ │ (现有系统)  │  │  │ │  容器        │    │  │ │  容器        │    │
            │ └────────────┘  │  │ │   ↓          │    │  │ │   ↓          │    │
            │                 │  │ │ todo_db      │    │  │ │ board_db     │    │
            │                 │  │ │ (独立)       │    │  │ │ (独立)       │    │
            │                 │  │ └──────────────┘    │  │ └──────────────┘    │
            └─────────────────┘  └─────────────────────┘  └─────────────────────┘
                    │
                    │ UMC 仍是用户主数据后台（管员工/组织/权限）
                    ▼
            ┌──────────────────┐
            │   UMC.Api        │ ──→ KeycloakSyncService ──→ Keycloak Admin REST
            │ (用户增删改/部门) │     (单向同步：UMC → Keycloak)
            └──────────────────┘
```

**核心原则**：
1. **Keycloak 是唯一的"在线身份源"** — 所有运行时鉴权都走 Keycloak
2. **UMC 是"用户主数据维护后台"** — HR/管理员在 UMC 增删改用户，自动同步到 Keycloak
3. **小程序不读 UMC** — 任何用户信息只能从 Keycloak token claims 或 Keycloak userinfo 获取
4. **小程序自带 DB** — 业务表外键用 Keycloak `sub`（UUID），不存 UMC UserId

---

## 二、为什么选 Keycloak

| 维度 | Keycloak | 自研 (OpenIddict 扩展) |
|---|---|---|
| 多 Client / Realm | ✅ 原生 | 需自己实现 |
| 用户管理 UI | ✅ Admin Console 开箱 | 需自己写 |
| Token Exchange (RFC 8693) | ✅ 内置 | 需扩展 |
| Fine-Grained Authz (UMA 2.0) | ✅ 内置 | 不支持 |
| 联邦登录 (微信/钉钉/LDAP/SAML) | ✅ 内置 IDP brokering | 需各自集成 |
| 协议映射器 (custom claims) | ✅ UI 配置 | 代码层修改 |
| 生产部署成熟度 | ✅ Red Hat 背书 | — |
| 学习曲线 | 中（一周熟悉） | 低（已有） |
| 资源占用 | 1-2GB RAM | 低 |

**结论**：小程序平台引入 Keycloak 收益 >> 成本。UMC 现有 OpenIddict 保留给老系统，新平台用 Keycloak。

---

## 三、Realm / Client 设计

### 3.1 Realm 结构（单 Realm 多 Client）

```
Realm: octopus
├── Users         (zhangsan, lisi, ...)         ← 由 UMC 同步
├── Groups        (章鱼科技/技术部, ...)         ← 由 UMC 同步
├── Roles
│   ├── Realm Roles: oa_admin, oa_user, ...
│   └── Client Roles
│       ├── miniapp-todo: { user, manager }
│       └── miniapp-board: { viewer, editor }
├── Clients
│   ├── octopus-portal              (Public, PKCE)  ERP 主门户
│   ├── octopus-oa-web              (Public, PKCE)  迁移自 UMC OpenIddict
│   ├── octopus-crm-web             (Public, PKCE)
│   ├── octopus-wms-web             (Public, PKCE)
│   ├── octopus-mes-web             (Public, PKCE)
│   ├── miniapp-todo                (Public, PKCE)  小程序
│   ├── miniapp-board               (Public, PKCE)
│   ├── umc-sync-bot                (Confidential, Service Account)  UMC 同步用
│   └── platform-introspector       (Confidential)  内网 token 验签用
├── Identity Providers
│   ├── wechat-work    (企业微信)
│   ├── dingtalk       (钉钉)
│   └── ldap           (公司域)
└── Protocol Mappers
    ├── octopus_user_id     (User Attribute → token claim)
    ├── primary_dept_id     (Group → token claim)
    ├── primary_dept_name   (User Attribute → token claim)
    └── miniapp_scope       (User Attribute → token claim)
```

### 3.2 为什么不用多 Realm

| 多 Realm | 单 Realm |
|---|---|
| 用户完全隔离（适合 SaaS 多租户）| ✅ 用户统一（适合企业内部）|
| 跨小程序登录要 IDP Brokering | ✅ 一次登录全局 SSO |
| 用户同步要建 N 份 | ✅ 同步一次到位 |

公司内部 ERP 用户体系应该是一份。多 Realm 是给"多个互相隔离的客户/租户"用的，不适合我们的场景。

### 3.3 自定义 Claims（Protocol Mappers）

进入小程序的 JWT 应该自包含必要业务字段，避免小程序回调 UMC：

```json
{
  "iss": "https://keycloak.octopus.com/realms/octopus",
  "sub": "f8a2b3c4-d5e6-...",        // Keycloak User UUID（业务表的外键）
  "aud": "miniapp-todo",
  "preferred_username": "zhangsan",
  "email": "zhangsan@octopus.com",
  "name": "张三",

  // —— 自定义 claims（由 UMC 同步时写入 User Attribute，Protocol Mapper 注入）
  "octopus_user_id": "2",            // UMC 原始 UserId（用于内部一致性，可选）
  "primary_dept_id": "3",
  "primary_dept_name": "技术部",
  "company_name": "章鱼科技",
  "job_title": "技术经理",
  "miniapp_scope": "personal",       // 该用户在小程序里的可见范围（personal/team/all）

  // —— 角色
  "realm_access": { "roles": ["oa_user"] },
  "resource_access": {
    "miniapp-todo": { "roles": ["user", "manager"] }
  },

  "exp": 1716000000
}
```

**关键**：小程序后端只信任 JWT，绝不再回 UMC 查任何数据。

---

## 四、数据隔离方案

### 4.1 数据库隔离

**每个小程序自带数据库**（独立 schema 或独立实例）。建议：

```
miniapp-todo:    todo_db    (PostgreSQL 容器, 端口仅暴露给 todo-api)
miniapp-board:   board_db   (同上)
miniapp-XXX:     XXX_db
```

**绝对禁止**：
- ❌ 小程序连 UMC 的 `octopus_umc.db` / MySQL
- ❌ 小程序调 UMC `GET /api/system/user/{id}`
- ❌ 在小程序 DB 里冗余存 UMC 用户表

### 4.2 业务表结构示范

```sql
-- 错误示范 ❌
CREATE TABLE todo_item (
    id BIGSERIAL PRIMARY KEY,
    title TEXT,
    umc_user_id BIGINT,   -- 不要！这是 UMC 内部 ID
    ...
);

-- 正确示范 ✅
CREATE TABLE todo_item (
    id BIGSERIAL PRIMARY KEY,
    title TEXT,
    kc_sub UUID NOT NULL,  -- Keycloak subject（JWT sub claim）
    ...
);

CREATE INDEX idx_todo_kc_sub ON todo_item (kc_sub);
```

### 4.3 用户首次进入小程序（Lazy Provisioning）

```
用户首次访问 miniapp-todo.octopus.com
  → 小程序后端从 JWT 拿 sub + 必要 claims
  → 检查本地 user_profile 表
      ├─ 存在 → 直接用
      └─ 不存在 → INSERT user_profile (kc_sub, snapshot_name, snapshot_email, ...)
                     ← snapshot 是为了离线/展示快照，避免每次都从 token 取
  → 继续业务
```

```sql
CREATE TABLE user_profile (
    kc_sub          UUID PRIMARY KEY,
    snapshot_name   TEXT,            -- token 中的 name，写入即不变（除非 sync 更新）
    snapshot_email  TEXT,
    last_login_at   TIMESTAMP,
    preferences     JSONB,           -- 小程序自己的偏好设置
    created_at      TIMESTAMP DEFAULT now()
);
```

**优势**：
- 小程序完全自治，不依赖 UMC 可用性
- 用户离开公司后，UMC 禁用 → Keycloak 同步禁用 → 小程序拿不到 token，自然访问失败
- 数据合规：小程序 DB 里没有手机号/邮箱等敏感字段（除了 token 同意暴露的）

---

## 五、用户身份同步（UMC → Keycloak）

### 5.1 同步策略

**单向同步：UMC 是 source of truth，事件驱动推 Keycloak**

```
UMC 操作                      触发动作
─────────────                 ────────────────────────────────
创建用户       ──→ POST /admin/realms/octopus/users
修改用户       ──→ PUT  /admin/realms/octopus/users/{id}
禁用用户       ──→ PUT  /admin/realms/octopus/users/{id} { enabled: false }
启用用户       ──→ PUT  /admin/realms/octopus/users/{id} { enabled: true }
修改密码       ──→ PUT  /admin/realms/octopus/users/{id}/reset-password
分配角色       ──→ POST /admin/realms/octopus/users/{id}/role-mappings/realm
绑定部门(组)   ──→ PUT  /admin/realms/octopus/users/{id}/groups/{groupId}
删除用户       ──→ DELETE /admin/realms/octopus/users/{id}  (软删建议用 disable)
```

### 5.2 新增 `KeycloakSyncService`（UMC.Api）

```csharp
public class KeycloakSyncService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<KeycloakSyncService> logger)
{
    private async Task<string> GetAdminTokenAsync()
    {
        // Client Credentials Flow，使用 umc-sync-bot
        var client = httpFactory.CreateClient();
        var resp = await client.PostAsync(
            $"{config["Keycloak:BaseUrl"]}/realms/octopus/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = config["Keycloak:SyncBotClientId"],
                ["client_secret"] = config["Keycloak:SyncBotClientSecret"],
            }));
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    public async Task SyncUserAsync(User u)
    {
        var token = await GetAdminTokenAsync();
        var client = httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var payload = new
        {
            username = u.UserName,
            email = u.Email,
            firstName = u.NickName,
            enabled = u.Status == 1,
            attributes = new Dictionary<string, string[]>
            {
                ["octopus_user_id"] = new[] { u.UserId.ToString() },
                ["primary_dept_id"] = new[] { u.PrimaryDeptId?.ToString() ?? "" },
                ["primary_dept_name"] = new[] { u.PrimaryDeptName ?? "" },
                ["company_name"] = new[] { u.CompanyName ?? "" },
                ["miniapp_scope"] = new[] { u.MiniappScope ?? "personal" },
            },
        };

        // 1) 查是否已存在（按 username）
        var search = await client.GetAsync(
            $"{config["Keycloak:BaseUrl"]}/admin/realms/octopus/users?username={u.UserName}&exact=true");
        var found = await search.Content.ReadFromJsonAsync<JsonElement>();
        if (found.GetArrayLength() == 0)
        {
            // 新建
            await client.PostAsJsonAsync($"{config["Keycloak:BaseUrl"]}/admin/realms/octopus/users", payload);
        }
        else
        {
            // 更新
            var kcId = found[0].GetProperty("id").GetString();
            await client.PutAsJsonAsync($"{config["Keycloak:BaseUrl"]}/admin/realms/octopus/users/{kcId}", payload);
        }
    }
}
```

### 5.3 同步触发点

UMC `UserController.Create/Update/Delete/SetStatus/ResetPassword` 末尾调用 `_kcSync.SyncUserAsync(user)`，**失败只记日志不阻塞主流程**（最终一致性）。

### 5.4 反向同步：避免循环

Keycloak 不应该写回 UMC。所有 Keycloak 端的修改都视为"临时"，下次 UMC 同步会覆盖。建议在 Keycloak Admin Console 直接对最终用户写 attribute 时给运维人员明确警告。

---

## 六、SSO 流程（用户视角无感）

### 6.1 用户首次登录小程序

```
1. 用户在浏览器打开 todo.miniapp.octopus.com
2. miniapp-todo 前端检查无 token → 重定向 Keycloak:
   GET https://keycloak.octopus.com/realms/octopus/protocol/openid-connect/auth
       ?client_id=miniapp-todo
       &response_type=code
       &redirect_uri=https://todo.miniapp.octopus.com/callback
       &scope=openid profile email
       &state=...&code_challenge=...&code_challenge_method=S256
3. Keycloak 检测无 SSO cookie → 显示登录页
4. 用户输入 zhangsan / 密码 → Keycloak 颁发 code 并跳转回 todo
5. miniapp-todo 后端用 code 换 token
6. miniapp-todo 拿到 JWT → 设 HttpOnly Cookie
```

### 6.2 用户已登录 ERP 主门户，跳转到小程序（核心 SSO 体验）

```
1. 用户已登录 portal.octopus.com（即 octopus-portal client 的 Keycloak session 已建立）
2. 点击 "Todo" 小程序图标
3. 浏览器跳转 todo.miniapp.octopus.com
4. miniapp-todo 重定向 Keycloak /auth
5. Keycloak 检测到 SSO cookie 已存在 → 不要求登录 → 直接颁发 code 跳回
6. 用户无感知，瞬间进入 Todo

关键：所有 Keycloak client 共享一个 SSO 会话 cookie（属于 keycloak.octopus.com 域）
```

### 6.3 退出登录

```
点击退出 → POST keycloak/protocol/openid-connect/logout
Keycloak 触发 Back-Channel Logout → 所有 client 的 session 失效
```

---

## 七、容器化 + 域名 + 部署

### 7.1 docker-compose 增量（小程序平台）

```yaml
services:
  # —————————————————— Keycloak ——————————————————
  keycloak:
    image: quay.io/keycloak/keycloak:25.0
    command: start
    environment:
      KC_DB: postgres
      KC_DB_URL: jdbc:postgresql://keycloak-db:5432/keycloak
      KC_DB_USERNAME: keycloak
      KC_DB_PASSWORD: ${KEYCLOAK_DB_PASSWORD}
      KC_HOSTNAME: keycloak.octopus.com
      KC_HOSTNAME_STRICT: "true"
      KC_PROXY: edge        # 在反代后
      KEYCLOAK_ADMIN: admin
      KEYCLOAK_ADMIN_PASSWORD: ${KEYCLOAK_ADMIN_PASSWORD}
    depends_on:
      keycloak-db:
        condition: service_healthy
    networks: [octopus-net]

  keycloak-db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: keycloak
      POSTGRES_USER: keycloak
      POSTGRES_PASSWORD: ${KEYCLOAK_DB_PASSWORD}
    volumes: [keycloak-pgdata:/var/lib/postgresql/data]
    networks: [octopus-net]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U keycloak"]
      interval: 10s

  # —————————————————— 小程序：Todo ——————————————————
  miniapp-todo:
    build: ./miniapps/todo
    environment:
      KC_ISSUER: https://keycloak.octopus.com/realms/octopus
      KC_CLIENT_ID: miniapp-todo
      DB_URL: postgresql://todo:${TODO_DB_PASSWORD}@todo-db:5432/todo
    depends_on: [keycloak, todo-db]
    networks: [octopus-net]
    # 注意：不在 ports 暴露，由 Caddy 路由

  todo-db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: todo
      POSTGRES_USER: todo
      POSTGRES_PASSWORD: ${TODO_DB_PASSWORD}
    volumes: [todo-pgdata:/var/lib/postgresql/data]
    networks: [octopus-net]

  # —————————————————— 小程序：Board ——————————————————
  miniapp-board:
    build: ./miniapps/board
    environment:
      KC_ISSUER: https://keycloak.octopus.com/realms/octopus
      KC_CLIENT_ID: miniapp-board
      DB_URL: postgresql://board:${BOARD_DB_PASSWORD}@board-db:5432/board
    depends_on: [keycloak, board-db]
    networks: [octopus-net]

  board-db:
    image: postgres:16-alpine
    # 同上

  # —————————————————— 反向代理（Caddy 自动 TLS） ——————————————————
  caddy:
    image: caddy:2-alpine
    ports: ["80:80", "443:443"]
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy-data:/data
    networks: [octopus-net]
```

### 7.2 Caddyfile（按域名路由到容器）

```caddy
keycloak.octopus.com {
    reverse_proxy keycloak:8080
}

portal.octopus.com {
    reverse_proxy umc-web:80
}

todo.miniapp.octopus.com {
    reverse_proxy miniapp-todo:8080
}

board.miniapp.octopus.com {
    reverse_proxy miniapp-board:8080
}

# 新增小程序只需加一行
*.miniapp.octopus.com {
    # 通配符路由（按子域名映射）— 进阶用法
}
```

### 7.3 DNS

- `*.miniapp.octopus.com` 通配符 A 记录指向反代服务器
- 通配符证书（Let's Encrypt DNS-01 challenge）或 Caddy 自动签发

---

## 八、小程序开发模板

### 8.1 推荐技术栈

**后端（小程序方便起见，推荐统一）**：
- Node.js + Fastify + `fastify-keycloak-adapter`
- 或 .NET + `Microsoft.AspNetCore.Authentication.JwtBearer`（与 ERP 一致）

**前端**：
- Vue 3 + Vite（与 ERP 子系统一致）
- 或 SvelteKit / Next.js（小程序自由度高）

### 8.2 .NET 小程序最小模板

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// JWT Bearer 验证（信任 Keycloak）
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["KC_ISSUER"];
        o.Audience = builder.Configuration["KC_CLIENT_ID"];
        o.RequireHttpsMetadata = builder.Environment.IsProduction();
    });

builder.Services.AddAuthorization();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration["DB_URL"]));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// 用户上下文：仅从 token 提取，不查 UMC
app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
{
    sub = user.FindFirst("sub")?.Value,
    name = user.FindFirst("name")?.Value,
    primaryDeptName = user.FindFirst("primary_dept_name")?.Value,
    miniappScope = user.FindFirst("miniapp_scope")?.Value,
    roles = user.FindAll("realm_access").Select(c => c.Value),
})).RequireAuthorization();

// 业务 API
app.MapGet("/api/todos", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var kcSub = Guid.Parse(user.FindFirst("sub")!.Value);
    return await db.Todos.Where(t => t.KcSub == kcSub).ToListAsync();
}).RequireAuthorization();

app.Run();
```

### 8.3 项目骨架

每个小程序仓库结构（建议放 `miniapps/` 目录下统一管理）：

```
miniapps/
├── todo/
│   ├── Dockerfile
│   ├── README.md
│   ├── src/
│   │   ├── Program.cs
│   │   ├── Models/
│   │   ├── Controllers/
│   │   └── Migrations/
│   └── web/                 # 前端
│       ├── package.json
│       └── src/
├── board/
│   └── ...
└── _template/               # 新增小程序的脚手架模板
    ├── Dockerfile
    ├── Program.cs.template
    └── web/
```

---

## 九、跨小程序授权（Token Exchange）

如果 miniapp-todo 需要调用 miniapp-board 的 API：

**方案 A：Token Exchange（推荐，RFC 8693）**

```
miniapp-todo 持有的 token (aud=miniapp-todo)
    ↓ POST keycloak/token (grant_type=urn:ietf:params:oauth:grant-type:token-exchange)
       subject_token=<原 token>
       audience=miniapp-board
    ↓
新 token (aud=miniapp-board，代表同一用户)
    ↓ 调 board API
```

需 Keycloak 中启用 Token Exchange feature 并配置 client-to-client 权限。

**方案 B：内部 API 用 Client Credentials**

服务对服务调用（非代表用户），用 client_credentials grant，最简单但失去用户上下文。

---

## 十、实施阶段（4 个 milestone，预计 6-8 周）

### M1：Keycloak 基础设施（1.5 周）

- [ ] docker-compose 加 keycloak + keycloak-db
- [ ] 创建 `octopus` realm，配置基础 client（umc-sync-bot）
- [ ] DNS + 通配符证书 + Caddy 路由
- [ ] Keycloak Admin Console 可访问，能手工创建测试用户
- [ ] 文档：Keycloak realm 配置导出 JSON（版本控制）

### M2：UMC ↔ Keycloak 用户同步（2 周）

- [ ] UMC 新增 `KeycloakSyncService`（admin REST API）
- [ ] UMC `UserController` CRUD 末尾接入同步
- [ ] 历史用户全量初始化同步（一次性脚本）
- [ ] 部门 = Keycloak Groups 同步
- [ ] 角色映射（UMC 角色 → Keycloak Realm Roles）
- [ ] Protocol Mapper 配置（octopus_user_id / primary_dept_id 等注入 token）
- [ ] 集成测试：UMC 改用户 → 1s 内 Keycloak 可见

### M3：第一个小程序 PoC（2 周）

- [ ] 创建 `miniapp-todo` Keycloak client（Public + PKCE）
- [ ] miniapp-todo 后端项目（.NET 模板）
- [ ] miniapp-todo 前端项目（Vue + oidc-client-ts）
- [ ] 独立 PostgreSQL，跑通业务（待办增删改）
- [ ] 验证：从 portal 跳过来无感登录
- [ ] 验证：禁用 UMC 用户后小程序自动失效
- [ ] 文档：小程序开发指南 + 模板仓库

### M4：平台化与运维（1.5 周）

- [ ] 小程序脚手架 CLI（`octopus-miniapp create <name>`）
- [ ] 通配符 DNS + 通配符证书
- [ ] 小程序注册中心（哪些小程序存在、谁负责、版本号）
- [ ] 监控告警：Keycloak 健康、token 颁发速率
- [ ] 备份策略：Keycloak realm 配置 + DB

---

## 十一、风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| Keycloak 是 SPOF | 全平台登录瘫痪 | 主从部署 + Redis 共享 session + 健康检查 |
| Keycloak 升级破坏 SPI | 自定义功能可能失效 | 锁定大版本，每次升级在测试环境验证 |
| Token 失效但用户还在用 | 重要操作中途断开 | 前端 silent refresh + 优雅降级 |
| UMC → Keycloak 同步延迟/失败 | 新用户无法登录小程序 | 重试队列 + 监控告警 + 手工同步入口 |
| Token 体积变大（自定义 claims 过多） | 性能下降 | 只放必要 claims，敏感信息走 introspection |
| 小程序 DB 数据失联（用户被删） | 业务数据孤儿 | 软删用户（Keycloak disable），保留 sub；外键不级联删除 |
| Token 中包含 PII 泄漏风险 | 合规问题 | claims 最小化原则，敏感字段不放 token |
| 跨小程序 token exchange 配置复杂 | 互通受阻 | 优先用独立 API + service account，token exchange 仅在需要时启用 |

---

## 十二、运维与监控要点

### 12.1 关键指标

- Keycloak QPS（token 颁发 / 验证）
- Token 颁发延迟（P95 < 200ms）
- UMC → Keycloak 同步失败率（< 0.1%）
- 小程序登录成功率（> 99.9%）
- 各小程序容器健康度

### 12.2 关键日志

- Keycloak event log（启用 admin event + login event）
- UMC `KeycloakSyncService` 操作日志
- 各小程序的认证失败日志

### 12.3 备份

- Keycloak realm 配置：每周导出 JSON 入 Git
- Keycloak DB：每日全量备份
- 各小程序 DB：按业务重要度配置

---

## 十三、与现有 ERP 子系统的关系

### 13.1 短期（共存阶段）

```
UMC OpenIddict       Keycloak
   ↓                    ↓
OA, CRM, WMS, MES    miniapps (todo, board, ...)
```

老系统继续用 UMC OpenIddict 颁发的 token，新小程序用 Keycloak。
用户在 UMC 创建后**同时**同步到 Keycloak。

### 13.2 中长期（统一阶段，可选）

把现有 OA/CRM/WMS/MES 也迁到 Keycloak（每个变成 Keycloak 的一个 client）。
此时 UMC OpenIddict 可以下线，UMC 仅作"用户管理后台"。

**迁移成本评估**（每个子系统）：
- 后端 JwtBearer 配置改 Authority/Audience：30 分钟
- 前端 oidc-client-ts 配置改：30 分钟
- 集成测试调整：2 小时
- 总计每个系统 < 1 天

---

## 十四、决策清单（建议在开工前敲定）

1. **是否引入 Keycloak？** → 推荐 ✅（理由见第二章）
2. **单 Realm 还是多 Realm？** → 单 Realm（理由见 3.2）
3. **是否统一所有系统到 Keycloak？** → 分两期，先并存再统一
4. **小程序后端语言？** → 推荐 .NET（与现有团队一致），允许 Node.js（小程序团队自治）
5. **每个小程序独立 DB 还是共享 schema？** → **独立 DB**（强隔离）
6. **DNS 策略？** → `*.miniapp.octopus.com` 通配符
7. **小程序代码仓库放哪？** → `miniapps/` 子目录（monorepo）或独立仓库（自治）

---

## 十五、参考实现资源

- Keycloak 官方文档：https://www.keycloak.org/documentation
- Token Exchange：https://www.keycloak.org/securing-apps/token-exchange
- Protocol Mappers：https://www.keycloak.org/server/features
- .NET JWT Bearer：https://learn.microsoft.com/aspnet/core/security/authentication/jwt
- oidc-client-ts（前端）：https://authts.github.io/oidc-client-ts/
- Caddy 反向代理：https://caddyserver.com/docs/quick-starts/reverse-proxy

---

## 附录：最小可运行 Demo

```bash
# 1. 起 Keycloak
docker run -d --name kc -p 8080:8080 \
  -e KEYCLOAK_ADMIN=admin -e KEYCLOAK_ADMIN_PASSWORD=admin \
  quay.io/keycloak/keycloak:25.0 start-dev

# 2. 进 http://localhost:8080，创建 realm: octopus，client: miniapp-todo
#    勾选 Standard flow + PKCE，redirect URI: http://localhost:3000/*
#    创建用户 zhangsan / 密码

# 3. 起一个 .NET miniapp-todo
dotnet new webapi -n MiniappTodo
cd MiniappTodo
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
# 按第 8.2 节配置 Program.cs，运行：dotnet run

# 4. 访问 http://localhost:3000 测试登录跳转
```

10 分钟可看到完整 SSO 流程。
