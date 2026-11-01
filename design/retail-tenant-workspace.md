# 零售多租户 + Workspace 身份切换设计方案

> **范围**：零售业务域（门店邀请成员入店），采用 **B + C** 路线——
> B = 独立零售服务 + Keycloak 承载身份；C = 数据层加 `tenant_id` 多租户隔离。
>
> **本文结论**：B+C 可行，但你现有的 Keycloak 集成有 **3 处前置债务**必须先还，
> 否则 C 落不了地。另外，"一个人多个租户要不要加 workspace"——**要，但关键在于
> 分清 Tenant / Membership / ActiveTenant 三层，做错层级会比不做更麻烦。**

---

## 1. 现状核查：B 的前置债务

三处都已在代码中确认（不是推测）。

### 1.1 `primary_dept_id` 是单值 claim，一人多租户在 token 层就表达不了

`KeycloakSyncService.BuildUserAttributes()` 只取 `IsPrimary == true` 的那一条 `UserDept`：

```csharp
var primaryUd = _db.UserDepts
    .Where(ud => ud.UserId == user.UserId && ud.IsPrimary)
    .FirstOrDefault();
```

而种子数据里 **zhangsan 本身就是一人两部门**（章鱼科技技术部为主 / 海星科技技术部为兼）。
也就是说：UMC 数据模型支持一人多组织，但**同步到 Keycloak 时被静默丢掉了**。
`octopus-realm.json` 里对应的 Protocol Mapper 也都是 `oidc-usermodel-attribute-mapper` 单值映射。

**影响**：`primary_dept_id` / `company_name` 无法承载"我能进哪些租户"。
C 方案一旦落地，token 层必须先改成多值。

### 1.2 Keycloak Groups 同步根本没实现

全仓搜索确认：

```
grep -rn "groups/|GetGroups|GroupId" KeycloakSyncService.cs   →  无命中
```

`MINIAPP-PLATFORM.md` 的 M2 清单里 `[ ] 部门 = Keycloak Groups 同步` 未勾选，且
`octopus-realm.json` **整个文件没有 `groups` 定义段**（只有 clients / users）。

**影响**：Groups 是 B 方案表达"用户在哪些租户"的**唯一原生机制**。这是欠债，不是新增。
必须先补，否则 C 的租户成员关系无处承载。

### 1.3 现有设计文档明确否决过多租户，你现在要推翻它

`MINIAPP-PLATFORM.md` §3.2「为什么不用多 Realm」原文：

| 多 Realm | 单 Realm |
|---|---|
| 用户完全隔离（适合 SaaS 多租户）| ✅ 用户统一（适合企业内部）|
| 跨小程序登录要 IDP Brokering | ✅ 一次登录全局 SSO |

> "公司内部 ERP 用户体系应该是一份。多 Realm 是给'多个互相隔离的客户/租户'用的，不适合我们的场景。"

这个判断在**当时是对的**。你现在引入 C，等于把平台定位从"企业内部 ERP"扩展到
"承载零售租户"。**这不必然错，但必须显式推翻并限定作用域**——见 §3。
若不做限定，`tenant_id` 会顺着 UMC → OA/CRM/WMS/MES 的同步链路扩散到 9 个子系统。

---

## 2. 核心问题：一人多租户，要不要加 workspace？

**要。但 90% 的失败案例都是把三层概念揉成了一层。**

### 2.1 必须拆开的三层

| 层 | 是什么 | 生命周期 | 存放位置 | 零售场景对应 |
|---|---|---|---|---|
| **Tenant（租户）** | 数据归属边界 | 长期，业务主体 | `retail_tenant` 表 | 门店 / 连锁企业 |
| **Membership（成员关系）** | user × tenant 的关联 + 该租户内的角色和状态 | 随入职/离职 | `retail_membership` 表 | 张三在 A 店是店长 |
| **ActiveTenant（当前身份）** | 会话级的"我现在以哪个身份操作" | 请求/会话级 | **请求上下文**，不是 DB 字段 | 张三切到 B 店 |

**你说的 workspace，本质是 Membership 的 UI 投影 + ActiveTenant 的载体。**
它不是一张新表，而是"Membership（数据） + ActiveTenant（运行时）"的组合概念。

### 2.2 判定：到底需不需要 workspace

| 场景 | 是否需要 workspace | 实现 |
|---|---|---|
| 一个用户只属于一个租户 | **不需要** | 现有 `primary_dept_id` 够用，`tenant_id` 从 token 直取 |
| 一个用户可能属于多个租户 | **必须要** | Membership 表 + 切换机制 + 多值 `tenants` claim |

零售连锁几乎必然是后者（店长巡店、区域经理管多店、导购跨店支援）。

### 2.3 硬结论：切换身份 = 重新签发 token，不能是前端本地切换

这是整个设计里最容易做错的一点。

**错误做法**：前端存一个 `activeTenantId`（localStorage / Pinia），调 API 时带上。

**为什么错**：后端只信任 JWT。如果 active tenant 只是前端 state，那么：

- 后端每个接口都必须额外校验「这个用户真的属于这个租户吗」→ 等于每次都回业务库查，
  彻底失去 JWT 自包含的意义；
- 更危险的是**漏校验**：只要有一个接口忘记校验，就是跨租户越权。

**正确做法（二选一）**：

| 方案 | 机制 | 代价 |
|---|---|---|
| **A. Token 自包含全集 + 请求头选定（推荐）** | token 携带 `tenants: ["t_a","t_b"]` 全集；客户端用 `X-Tenant-Id: t_b` 声明当前身份；服务端校验 `header ∈ token.tenants` | 需自研中间件；但校验是内存集合判断，零 DB 开销；**无需重新签发 token** |
| **B. Token Exchange（RFC 8693）** | 切换时调 Keycloak 换取 `active_tenant` 变化的新 token | 语义最正统；但需启用 Token Exchange feature、配置 client-to-client 权限，且注入自定义 claim 的行为随版本变化，运维成本高 |

**推荐 A**。理由：token 有效期当前是 3600s（`accessTokenLifespan`），
若走 Token Exchange，每次切店都要一次网络往返 + 新 token 签发，体验和 Keycloak 压力都不划算。
方案 A 把"能否进这个租户"放在不可伪造的 token 里，把"当前在哪个租户"降级为可校验的请求参数——**安全性不打折，成本大幅下降**。

> 注意：方案 A 的前提是**校验必须集中在一个中间件里，fail-closed**，
> 绝不能散落在各 Controller（见 §7）。

---

## 3. B+C 的边界：`tenant_id` 加在哪个库

**这是 B 和 C 会不会互相打架的唯一决定点。**

### 3.1 结论：`tenant_id` 只加在零售域自己的库，绝不加到 UMC

| 库 | 是否加 tenant_id | 角色 |
|---|---|---|
| **retail_db**（新增） | ✅ 全部业务表加 | 多租户隔离域 |
| `octopus_umc.db` | ❌ **不加** | 平台主数据后台，保持单租户 |
| OA / CRM / WMS / MES / FIN / HRM / OMS | ❌ 不加 | 与本方案无关 |

### 3.2 为什么不能加到 UMC

1. **违背 B 的根基**。`MINIAPP-PLATFORM.md` 原则 2：「UMC 是用户主数据维护后台」；
   原则 3：「小程序不读 UMC」。给 UMC 加租户维度，等于让它同时承担
   "平台主数据"和"租户业务数据"两个矛盾角色。
2. **会污染 9 个子系统**。`UserSyncService` / `DeptSyncService` 把用户和部门变更
   推送到 OA（`/api/users/sync`、`/api/contact/dept/sync`）以及其他系统。
   UMC 一旦租户化，所有下游 payload + 鉴权都要改。
3. **C 的诉求能被独立库满足**。"租户数据不互相看见"这个需求，
   在 retail_db 内部用 `tenant_id` 就能 100% 实现，不需要动 UMC。

### 3.3 租户的权威列表放哪

| 选项 | 评价 |
|---|---|
| UMC `sys_dept` 的门店节点 | ⚠️ 借用现成组织树，但**租户生命周期 ≠ 部门生命周期**（关店/并店/加盟商变更 ≠ 组织调整），且会触发 `DeptSyncService` 把门店推给 OA |
| **独立 `retail_tenant` 表（推荐）** | 租户是零售域的一等实体，生命周期自主；UMC 仅在"创建租户时拉取公司信息"这一处被动引用 |

**推荐独立表**。UMC 退化为"提供企业基础信息的数据源"，而不是租户注册中心。

### 3.4 身份归属：谁是谁

```
UMC sys_user (UserId)                     ← 平台级主数据，全局唯一身份
      │  同步
      ▼
Keycloak user (sub UUID)                  ← 在线身份源，token 的 sub
      │  Group Membership
      ▼
Keycloak Group /{tenantCode}              ← "能进哪些租户"（多值）
      │  镜像
      ▼
retail_membership (kc_sub × tenant_id)    ← "在该租户里是什么角色"（业务库判定）
      │
      ▼
retail_* 业务表 (tenant_id)               ← 数据隔离
```

**关键**：`sub`（UUID）是零售库引用用户的唯一外键。**不要存 UMC `UserId`**——
这是 `MINIAPP-PLATFORM.md` §4.2 已确立的原则，零售服务必须遵守。

---

## 4. Keycloak 侧改造

### 4.1 用 Groups 表达租户成员关系

补上欠债（§1.2）。层级建议：

```
octopus (realm)
└── tenants
    ├── t_shanghai_001   (上海南京路店)
    ├── t_beijing_002    (北京王府井店)
    └── t_chain_hq       (连锁总部)
```

成员关系 = 用户在 group 里。新增 **Group Membership** Protocol Mapper，
把所有 client 的 claim 从单值升级为多值：

```json
{
  "sub": "f8a2b3c4-...",
  "tenants": ["t_shanghai_001", "t_chain_hq"],   // 新增：多值，我能进哪些租户
  "primary_dept_id": "3",                         // 保留：向后兼容
  "octopus_user_id": "2"
}
```

`primary_dept_*` / `company_name` 保留不删，避免打断现有 miniapp-todo 等已上线的小程序。

### 4.2 ActiveTenant 的存放（承接 §2.3）

Keycloak 的 **user attribute 是静态的**，无法表达"当前会话在哪个租户"。
因此 ActiveTenant **不能**放在 Keycloak attribute 里。

落地方式（方案 A）：

```
客户端： Authorization: Bearer <token>          ← 不可伪造，含 tenants[] 全集
         X-Tenant-Id: t_shanghai_001            ← 声明当前身份

服务端中间件（fail-closed）：
  1. 无 X-Tenant-Id                → 400（除非接口标记 [AllowCrossTenant]）
  2. X-Tenant-Id ∉ token.tenants   → 403
  3. 通过 → 写入 HttpContext.Items + 设置 EF 全局过滤器
```

### 4.3 ⚠️ Keycloak 的能力边界：角色**不能**按租户区分

这是最容易踩的坑。Keycloak 无法原生表达
**"张三在 A 店是店长，在 B 店是店员"**：

| 机制 | 为什么不行 |
|---|---|
| Realm Role | 全局的，与租户无关 |
| Client Role | per-client；若每租户一 client，N 个店 = N 个 client，运维爆炸 |
| Group Role Mapping | 技术上可行（group → role），但 N 店 × M 角色 = N×M 个 role，命名/审计/回收都不可控 |

**结论（本方案的核心架构判断）**：

> **Keycloak 只回答两个问题：「你是谁」和「你能进哪些租户」。
> 「你在某个租户里是什么角色」必须由 retail_db 的 `retail_membership` 表判定。**

因此 JWT 里**不要**放业务角色。权限判定的正确位置是
retail-api 的授权中间件（查 membership 表或进程内缓存），不是 Keycloak。
这同时也解决了"租户角色变更何时生效"的问题——改 membership 立即生效，
不必等 token 过期或强制刷新。

---

## 5. 邀请流程（含一个必须避开的坑）

### 5.1 安全坑：不能把 Keycloak Admin 凭据给租户

现有 realm 配置 `"registrationAllowed": false`，即**不允许自助注册**。
邀请新用户就必须调 Keycloak Admin API 建号。

但 **Keycloak 的 service account 权限是 realm 级的**——
做不到"上海店的 bot 只能管上海店的 group"。

> **所以绝不能让零售前端或租户直接持有 Admin API 凭据。**
> 邀请必须由 retail-api 代理；retail-api 持有 `retail-invite-bot` service account，
> 并**先在自己的 membership 表校验 inviter 是否有 `member:invite` 权限**。

### 5.2 建号方式：用 required action，不要开 `registrationAllowed`

错误做法：把 `registrationAllowed` 改成 `true` → 任何人都能注册进平台。

正确做法：Admin API 创建用户 + 设 `UPDATE_PASSWORD` required action
（或走短信验证码），用户首次登录强制设密码。这是"受控自助注册"。

### 5.3 时序

```
1. 店长在店铺后台填手机号/企微
   POST /api/retail/invites          X-Tenant-Id: t_shanghai_001

2. retail-api 中间件：校验 token.tenants 包含 X-Tenant-Id
   授权：查 membership → 置位 member:invite ？

3. 落库 retail_invite
   (invite_id, tenant_id, phone, role_in_tenant, token_hash,
    expires_at, max_uses, used_count, state, invited_by_sub)

4. 短信 / 企业微信 发邀请链接
   https://retail.octopus.com/invite/accept?t=<token>

5. 被邀请人打开落地页
   ├─ 已有 Keycloak 账号 → OIDC 登录 → 跳到 6
   └─ 无账号 → retail-api 用 retail-invite-bot 建号(受控) → 用户设密码 → 跳到 6

6. retail-api 写 retail_membership (kc_sub, tenant_id, role, status=active)
   + 调 Keycloak Admin API 加入 group /tenants/{tenantCode}
   + 标记 invite.state = accepted

7. 用户下次取 token 时，tenants[] 里出现新租户
```

**注意第 6 步的双写**（业务库 + Keycloak group）。
必须做成**幂等 + 可对账**：任一失败要能重放。建议 `retail_membership` 增加
`kc_synced_at` / `kc_sync_state` 字段，配一个后台对账 worker。
这是 B 方案必付的复杂度——两套系统存同一份成员关系，一致性只能靠对账兜底。

---

## 6. 数据模型（retail_db）

```sql
-- 租户（门店 / 连锁企业）
retail_tenant (
  tenant_id      TEXT PRIMARY KEY,      -- 与 Keycloak group 名对齐，如 t_shanghai_001
  tenant_code    TEXT UNIQUE NOT NULL,
  tenant_name    TEXT NOT NULL,
  tenant_type    TEXT NOT NULL,         -- store | chain | franchise
  parent_tenant_id TEXT NULL,           -- 加盟/连锁归属
  umc_company_dept_id BIGINT NULL,      -- 仅溯源用，不做外键
  status         TEXT NOT NULL DEFAULT 'active',
  created_at     DATETIME NOT NULL
);

-- ★ 这就是"workspace"的持久化形态：user × tenant 的成员关系
retail_membership (
  membership_id  INTEGER PRIMARY KEY AUTOINCREMENT,
  kc_sub         TEXT NOT NULL,         -- Keycloak sub，唯一用户外键
  tenant_id      TEXT NOT NULL,         -- → retail_tenant
  role_in_tenant TEXT NOT NULL,         -- owner | manager | staff | cashier
  status         TEXT NOT NULL,         -- active | suspended | left
  joined_at      DATETIME NOT NULL,
  kc_synced_at   DATETIME NULL,         -- 对账用（§5.3）
  kc_sync_state  TEXT NULL,
  UNIQUE (kc_sub, tenant_id)
);
CREATE INDEX idx_membership_sub ON retail_membership (kc_sub, status);

-- 邀请
retail_invite (
  invite_id     INTEGER PRIMARY KEY AUTOINCREMENT,
  tenant_id     TEXT NOT NULL,
  phone         TEXT NULL,              -- 定向邀请
  invite_code   TEXT NULL,              -- 通用邀请码（可空，二选一）
  token_hash    TEXT NOT NULL,          -- 只存哈希，不存明文
  role_in_tenant TEXT NOT NULL,
  max_uses      INTEGER NOT NULL DEFAULT 1,
  used_count    INTEGER NOT NULL DEFAULT 0,
  state         TEXT NOT NULL,          -- pending|accepted|expired|revoked
  expires_at    DATETIME NOT NULL,
  invited_by_sub TEXT NOT NULL,
  accepted_by_sub TEXT NULL,
  created_at    DATETIME NOT NULL
);
CREATE INDEX idx_invite_tenant_state ON retail_invite (tenant_id, state);

-- 业务表示范：一律带 tenant_id
retail_member_profile (
  profile_id INTEGER PRIMARY KEY AUTOINCREMENT,
  tenant_id  TEXT NOT NULL,
  kc_sub     TEXT NOT NULL,
  display_name TEXT,
  ...
);
```

**要点**：
- 所有业务表第一列之后就是 `tenant_id`，且**必须**进复合索引前缀。
- `retail_membership` 加 `UNIQUE(kc_sub, tenant_id)`——同一人在同一店只能有一条关系。
- 用户软删（Keycloak disable），不物理删 `kc_sub`，避免业务数据孤儿
  （沿用 `MINIAPP-PLATFORM.md` §11 已列的对策）。

---

## 7. EF Core 多租户落地（fail-closed）

**本方案最危险的地方**：全局查询过滤器如果"忘记设置租户"，默认行为是
**返回全部租户的数据**。必须反转默认值。

```csharp
// 1) 租户上下文（Scoped，由中间件填充）
public interface ITenantContext
{
    string? TenantId { get; }
    bool IsResolved { get; }          // 是否已确定租户
}

// 2) DbContext 全局过滤器 —— 未解析租户时匹配空集，而不是全集
protected override void OnModelCreating(ModelBuilder mb)
{
    mb.Entity<RetailMemberProfile>()
      .HasQueryFilter(x => _tenant.IsResolved && x.TenantId == _tenant.TenantId);
    //  ↑ 未解析 → 恒 false → 查不到任何数据（fail-closed）
}

// 3) 中间件：解析失败直接短路，不要让请求进到业务层
if (endpointRequiresTenant && !tenantContext.IsResolved)
    return Results.Json(new { code = 400, msg = "缺少或非法的租户上下文" }, statusCode: 400);
```

**三道防线**：
1. 中间件短路（第一道）
2. 全局过滤器 fail-closed（第二道，防业务代码绕过）
3. 写操作在 `SaveChanges` 拦截器里强制填充/校验 `tenant_id`（第三道，防伪造）

> 必须有测试覆盖："不传 `X-Tenant-Id` → 400"、
> "传一个不属于自己的 tenant → 403"、"跨租户读 → 返回空集"。

---

## 8. 分期与工期

| 阶段 | 内容 | 工期 | 备注 |
|---|---|---|---|
| **M0** | 补 Keycloak Groups 同步 + 多值 `tenants` claim | 3–5 天 | **B 的前置欠债，必须先还** |
| **M1** | retail 服务骨架 + `retail_tenant` / `retail_membership` + 多租户中间件 + 三道防线 + TDD | 5–7 天 | C 的地基 |
| **M2** | 邀请域（表 / 服务 / 接口 / 状态机 / 短信） | 4–5 天 | |
| **M3** | `retail-invite-bot` service account + 受控建号 + Keycloak group 双写对账 | 3–4 天 | |
| **M4** | 前端：workspace 切换器 + 邀请管理页 | 4–6 天 | |
| **M5** | 端到端联调 + 越权用例回归 | 3 天 | |
| | **合计** | **约 5–6 周** | 一人全职 |

**相比上一轮方案 A 的 2–3 周，B+C 的成本大约是 2 倍。**
多出来的成本主要是：Keycloak Groups 欠债（M0）、双写对账（M3）、多租户防线（M1）。
换来的是**鉴权层硬隔离**和**可扩展到加盟商 / 外部租户**的能力。

---

## 9. 风险清单

| 风险 | 影响 | 对策 |
|---|---|---|
| 全局过滤器忘设租户 → 返回全集 | **跨租户数据泄漏** | fail-closed 默认空集 + 三道防线 + 专门的越权回归测试 |
| 业务库与 Keycloak group 双写不一致 | 用户进不去 / 已离职仍可进 | `kc_sync_state` + 后台对账 worker + 幂等重放 |
| `X-Tenant-Id` 被伪造 | 越权 | 必须校验 `header ∈ token.tenants`，不可只信 header |
| Keycloak Groups 同步未做就先上 C | 租户成员关系无承载 | M0 列为硬前置，不通过不进 M1 |
| 推翻 MINIAPP-PLATFORM §3.2 未留记录 | 后续维护者困惑，可能被"纠正"回去 | 在 `DECISIONS.md` 显式登记本次决策与作用域限定 |
| realm 里 `directAccessGrantsEnabled: true` | 密码模式开启，暴力破解面扩大 | 生产关闭，或至少加限流（UMC 已有 login 限流可参考） |
| 一人多租户的租户数膨胀 | token 体积变大 | `tenants[]` 只放 tenant_id，不放名称/角色；必要时超 50 个改走 introspection |
| 租户删了但 Keycloak group 还在 | 幽灵权限 | 租户生命周期状态机 + 级联清理 group |

---

## 10. 开工前必须敲定的 4 件事

1. **租户粒度**：一个连锁企业 = 一个租户，还是每个门店 = 一个租户？
   - 前者：`retail_tenant` 只有几十条，group 层级浅，token 小
   - 后者：可能上万条，需要考虑 `tenants[]` 体积和 group 管理自动化
   - **这一条直接决定 M0 的 group 层级设计和 token 策略，必须先定。**

2. **是否允许"同一用户在两个租户里有不同角色"？**
   - 允许 → 权限判定必须在 `retail_membership`（本方案默认）
   - 不允许 → 可以用 Keycloak client role 简化，token 直接带角色，业务库少查一次
   - 零售连锁**几乎必然允许**，所以本方案按"允许"设计。

3. **邀请对象确认**：店员（内部员工）还是会员（消费者）？
   - 店员 → 本方案适用
   - 会员 → **不要进 Keycloak 用户体系**，量级和生命周期完全不同，应走 CRM / 会员域的独立表 + 手机号验证

4. **`tenant_id` 是否只限零售域**？（本方案建议：是）
   - 若将来 OMS 的 `oms_shop` 也要纳入同一租户体系，需要现在就定好
     `oms_shop` 与 `retail_tenant` 的映射关系，避免二次改造

---

## 附：与现有资产的关系

| 现有资产 | 本方案如何处理 |
|---|---|
| `MINIAPP-PLATFORM.md` §3.2「不用多 Realm」 | **保留**——本方案仍用单 Realm，租户用 Groups 表达，不引入多 Realm |
| `KeycloakSyncService` | **扩展**：补 Groups 同步 + 多值 `tenants`；保留现有单值 attribute 兼容 |
| `octopus-realm.json` | **扩展**：加 `groups` 段 + Group Membership mapper + `retail-invite-bot` client |
| UMC `UserController` | **不改**（本方案不动 UMC；仅"创建租户时读取公司信息"这一处被动引用） |
| `miniapps/todo` 双通道认证模板 | **复用**：retail-api 直接套用其 PolicyScheme + OIDC + JwtBearer 结构 |
| UMC `sys_user` / `sys_dept` | **不改**：UMC 保持平台主数据后台定位 |

---

# 实施记录（M0–M5 已完成）

> 本节记录**实际落地**的内容，与上文规划如有差异以本节为准。

## 与规划的两处细化

### 1. `tenants` claim 不由 UMC 写入（§4.1 的修正）

原规划写「新增 `tenants: [...]` claim」。实施时改为**职责分离**，理由是这样 UMC 完全不必知道"租户"这个零售概念（与 §3.3 的结论一致）：

| claim | 来源 | 语义 |
|---|---|---|
| `dept_ids` | UMC `BuildUserAttributes`（多值） | 用户在 ERP 组织中的全部部门 |
| `groups` | Keycloak **Group Membership mapper**（`full.path=true`） | 用户所属的全部 group，含 `/tenants/*` |

前端/服务端从 `groups` 中筛出 `/tenants/` 前缀的项即得到可进入的租户集合。M0 只修了 UMC 侧那个真实缺陷（兼职部门被静默丢弃），没有把 tenant 写进 UMC。

### 2. 补了「受控自助建号」接口（§5.2 的落地）

`POST /api/retail/invites/register`（**匿名**，凭邀请令牌授权）：
- 由 `retail-invite-bot` 服务账号建号，**不打开 realm 的 `registrationAllowed`**
- 被邀请人在注册页自设密码（`temporary=false`），无需二次下发临时口令
- 定向邀请校验手机号（归一化容忍 `+86`/`0086`/空格/连字符）
- 建号成功后立即建立成员关系并置 `KcSyncState=pending`
- `POST /api/retail/invites/preview` 亦改为匿名，让无账号者先看到"加入哪家店"再决定注册或登录

## 交付物

| 位置 | 内容 |
|---|---|
| `OctopusUMC/src/OctopusUMC.Api/Services/KeycloakGroupService.cs` | Group 原语 + 受前缀保护的 reconcile |
| `OctopusUMC/src/OctopusUMC.Api/Services/KeycloakSyncService.cs` | 多值 `dept_ids` |
| `aspire/.../keycloak/realms/octopus-realm.json` | `groups` 段、`dept_ids`/`groups` mapper、`octopus-retail`、`retail-invite-bot` |
| `OctopusRetail/src/OctopusRetail.Api/` | 独立零售服务（端口 5010） |
| `OctopusRetail/web/OctopusRetail.Web/` | 前端 workspace 切换器 + 邀请管理页（端口 5186） |

### 三道防线（实现位置）

1. `Multitenancy/TenantResolutionMiddleware.cs` —— 缺头 400 / 越权 403 / 不解析端点拦截
2. `Persistence/RetailDbContext.cs` —— `ITenantScoped` 实体的 fail-closed 全局过滤器
3. `Persistence/TenantFilterGuard.cs` —— 建模期校验"有隔离标记必有过滤器"，否则启动失败

## 测试

| 套件 | 用例数 | 状态 |
|---|---|---|
| OctopusUMC.Api.Tests | 137 | ✅ 全通过（含 M0 新增 16） |
| OctopusRetail.Api.Tests | 64 | ✅ 全通过 |

零售侧覆盖：多租户隔离与越权矩阵、fail-closed 过滤器（含多上下文不串租户）、workspace 切换、邀请状态机与额度、token 只存哈希、受控建号安全边界、group 双写对账、以及 M5 的完整端到端闭环。

前端 `npm run build`（含 `vue-tsc` 类型检查）通过。

## 尚未覆盖 / 后续项

- **租户粒度尚未最终确定**（§10 第 1 条）。当前种子按「连锁总部 + 门店」两级建模，`parent_tenant_id` 已留好层级，但 `tenants[]` 体积策略未按"一店一租户且数量上万"做压测。
- **生产迁移路径**：retail_db 目前走 SQLite `EnsureCreated()`，切 MySQL 时需启用 EF Migrations（与 ADR-007 的约定一致）。
- **短信/企微触达未接入**：邀请创建后只返回链接，实际发送渠道待接。
- **`retail_tenant` 的增删改接口未提供**（当前仅种子数据）；租户生命周期状态机（关店 → 级联清理 Keycloak group）尚未实现。
- **Keycloak group 双写的反向对账**：当前只做"业务库 → Keycloak"单向；若有人在 Keycloak 侧手工加组，不会被业务库纠正。

---

# 验收预演发现并修复的缺陷（2026-10-12）

在真实 Keycloak 26.0 上做端到端预演时，发现 **2 个单元测试测不到的缺陷**——
共同原因是测试用的假 HTTP handler 把我对 Keycloak API 的**错误假设**固化了下来。
两者都已修复并补了锁定真实响应形态的回归测试。

## 缺陷 1：service account 没有 `realm-management` 角色 → 建号 403

`octopus-realm.json` 里两个 bot client 只声明了 `serviceAccountsEnabled: true`，
**没有指派任何角色**。实测 `retail-invite-bot` 能拿到 token，但调用 Admin API 建用户返回
`HTTP 403 Forbidden`，整个「受控建号 + group 双写」链路在真实环境完全不可用。

单元测试测不到，是因为 `FakeHandler` 直接对 `POST /users` 返回 201，从未经过真实鉴权。

**修复**：在 realm 的 `users` 段显式声明服务账号用户并挂 `realm-management` 角色：

```json
{
  "username": "service-account-retail-invite-bot",
  "enabled": true,
  "serviceAccountClientId": "retail-invite-bot",
  "clientRoles": {
    "realm-management": ["manage-users","view-users","query-users",
                         "manage-groups","view-groups","query-groups"]
  }
}
```

`umc-sync-bot` 同样补齐（其用户同步路径存在同一问题）。

**验证**：重建 realm 后 `retail-invite-bot` 建用户返回 **201**，创建子组 **201**，入组 **204**。

## 缺陷 2：Keycloak 26 的 `GET /groups` 不内联 `subGroups` → 组树永远解析不出

Keycloak 26.0 实测返回：

```json
{ "id":"66c876c2-…", "name":"tenants", "path":"/tenants", "subGroupCount":1, "subGroups":[] }
```

子组**不内联**，只给计数。而第一版 `ExpandGroupsAsync` / `FlattenGroups` 递归的正是
那个恒为空的 `subGroups`，后果是：

- `EnsureGroupAsync("/tenants/t_shanghai_001")` 解析不出子组 → 尝试创建已存在的组 → 拿到 **409**
  → 返回 `null` → `AddUserToGroupAsync` 失败 → 对账把成员标为 `failed`
- 即整条 group 同步链路静默失效，且表现为"看起来在重试"

单元测试测不到，是因为 `TreeWithTenantsAndChildren` / `TenantsTree` 这些假数据
**把 subGroups 内联了**——假数据比真实 API 更"友好"，反而掩盖了缺陷。

**修复**：`subGroups` 非空则直接递归；为空且 `subGroupCount > 0` 时显式拉
`GET /groups/{id}/children`。两种形态都兼容（老版本/其他配置若内联则省一次往返）。

**验证**：真实环境下 `/groups` → `subGroupCount=1`，`/groups/{id}/children` → 返回
`/tenants/t_shanghai_001`（id `32c77c30`）。

**回归测试**：UMC `KeycloakGroupServiceTests` 新增 3 例、零售 `KeycloakSyncTests` 新增 2 例，
全部使用**真实 26 形态**（`subGroupCount` + `/children`）；`RetailE2EFactory` 的假端点也
改成真实形态，避免 E2E 继续验证错误假设。

## 教训

假 HTTP handler 只能验证"我的代码如何消费响应"，**不能验证"响应长什么样"**。
涉及外部系统契约的代码，必须在真实实例上至少手工验证一次。
建议后续对 Keycloak/微信等外部依赖的集成，都把"真实实例冒烟"作为验收门禁的一项。

---

# 补充实现：自助建店（消除引导缺口）

## 问题

「邀请」需要店长权限，店长权限来自成员关系，成员关系来自邀请——**第一个店长没有任何人能邀请**。
交付初版只能手工写 SQL + 手动改 Keycloak 组，真实环境几乎不可用。

同时种子数据用占位 sub（`kc-zhangsan` 等）模拟成员关系，这些 sub 在真实 Keycloak 中不存在，
对账器每轮都会为它们发起请求并永久失败，日志被无意义重试刷屏。

## 方案

### 1. `POST /api/retail/tenants` —— 自助建店

任何已登录用户都可创建门店并成为 `owner`：建 `retail_tenant` + `retail_membership`，
并**立即**把 owner 加入 Keycloak 组 `/tenants/{tenantId}`，使其下次取 token 即带上该租户。

- 端点标记 `[AllowCrossTenant]`：调用者此时通常还没有任何租户，无法提供 `X-Tenant-Id`
- `tenantId` 由 `tenantCode` 派生（`t_` + 小写 code），保证是 group-safe 的名称
- 唯一性**同时校验 `TenantCode` 与派生出的 `TenantId`**——只查 `TenantId` 会漏掉
  「同编码但历史数据派生出了不同 id」的情况（种子里的 `SH001` / `t_shanghai_001` 正是如此）
- 防滥用：编码格式 `^[A-Za-z0-9][A-Za-z0-9_-]{1,31}$`、名称 1-64 字、
  类型限 `store|chain|franchise`、单用户 owner 门店数上限 10
- Keycloak 不可用**不阻塞建店**：成员关系落 `pending`，交给对账器重试
- 响应含 `nextStep` 明示「必须重新获取 token」——`groups` claim 不会自动更新

### 2. 种子噪声消除

新增 `MembershipSyncState.Unmanaged`：演示用占位 sub 的成员关系标为此状态，对账器跳过。
Seeder 里加了幂等修补，老库中已以 `pending` 落库的占位行会在启动时自动转为 `unmanaged`。

顺带把同步状态常量从 `Services/GroupSyncReconciler` 抽到 `Persistence/MembershipSyncState`——
Seeder / 业务服务 / 对账器都要用，让 `Persistence` 反过来依赖 `Services` 是分层倒挂。

## 实测（2026-10-18，真实 Keycloak）

用**全新用户**（无任何门店）验证：

| # | 操作 | 结果 |
|---|---|---|
| 1 | 登录后查 workspace | `total = 0`（鸡生蛋起点） |
| 2 | `POST /api/retail/tenants` 建「成都春熙路店 / CD001」 | 200，`tenantId=t_cd001`，`role=owner`，`groupSynced=true` |
| 3 | 重新登录查 token | `groups = ['/tenants/t_cd001']` |
| 4 | 查 workspace | 成都春熙路店 / owner |
| 5 | 对该门店创建邀请 | 200 邀请已创建 |
| 6 | 查库同步状态 | `synced: 5, unmanaged: 4`，**待对账 0 条** |

测试：零售 84/84 通过（新增 10 个建店相关用例，含编码校验、重复、上限、Keycloak 不可用、
以及「占位成员关系不再被对账」的回归）。

---

# 补充实现：短邀请链接 · 组织类型泛化 · 入店即同步

## 1. 短邀请链接 `/i/{code}`

原链接把 43 字符的 base64url token 放在 query 里，又长又难口头传达。
现在额外生成 **8 位短码**（字符表 55 个，剔除形近的 `0/O`、`1/l/I`、`5/S`），
熵约 46 bit，配合有效期与使用次数上限足够作为凭据。

- URL：`http://localhost:5186/i/B1V2d9xK`
- **仍然只存哈希**（`ShortCodeHash`），明文仅创建时返回一次
- 长 token 链接保持兼容；服务端按长度自动区分（8 位当短码）

### 为什么不用「加密 ID 再解密」

加密（AES / Hashids 之类）作用于数据库 ID 时是**确定性映射**：拿到任一有效链接
即可推导出 ID±1 对应的码，**枚举出全部邀请**（典型 IDOR）。而且仍需管理密钥，
解密后照样要按 ID 查库。短随机码之间没有推导关系，成本相同、安全性更高。

## 2. 组织类型泛化（不再写死「门店」）

业务上邀请不只用于门店：**子公司的销售也会邀请客户、企业会邀请外部协作方**。

- 类型扩展为 `store | chain | franchise | company | subsidiary`
- 预览响应新增 `tenantType`，前端用 `tenantNoun()` 决定称谓
  （store→门店、company→企业、subsidiary→子公司，未知→组织）
- 后端错误文案中性化为「组织类型 / 名称长度」，不再出现"门店"

## 3. 入店即同步 Keycloak 组（修复「加入成功却 403」）

原实现把成员关系写成 `pending`，等后台对账（最多 60 秒）才推 Keycloak 组。
但用户入店后**马上就会登录**，而 token 的 `groups` 是登录时从 Keycloak 取的——
结果 workspace 里能看到组织（走 DB），任何租户内接口却返回 403。

现在 `GrantMembershipAsync` 在落库后**立即**尝试同步（与建店路径对齐），
失败则降级为 `failed` 交对账器重试。

## 4. 链接按当前访问地址重建

后端按 `Retail:PublicBaseUrl` 生成链接，但那可能与用户实际访问方式不一致：
实测 Safari 会把 `http://localhost:5186` **自动升级为 https**，而 Vite dev server
不支持 TLS，直接报「无法与服务器建立安全连接」。前端改为用
`window.location.origin` 重建链接，保证协议/主机与实际访问一致。

## 实测（2026-10-18，真实 Keycloak）

| # | 操作 | 结果 |
|---|---|---|
| 1 | 建「子公司」类型组织 `t_sub01` | 200 |
| 2 | 用短链建邀请 | 短码 `DJp8rBj7` |
| 3 | 预览 | 组织=华东销售子公司，**tenantType=subsidiary** |
| 4 | 全新用户凭短码注册 | 200，账号已创建，tenant=t_sub01 |
| 5 | 立刻登录 | `groups=['/tenants/t_sub01']`（**无需等对账**） |
| 6 | 访问本组织成员 | **200** |
| 7 | 越权访问别的组织 | **403** |

测试：零售 97/97、UMC 140/140。

---

# 补充实现：UMC→Keycloak 存量同步 · 组织成员管理

## 1. 打开 UMC→Keycloak 同步（并补存量回填）

**问题**：`KeycloakSyncService.Enabled` 依赖 `Keycloak:SyncEnabled`，AppHost 里只有
retail-api 打开了它。UMC 侧一直是 false，所以：

- 平台员工（lisi / wangwu / editor / 6 个车间组长…）在 Keycloak 里**没有账号**，无法登录任何 OIDC 应用
- Keycloak 里只有 realm JSON 种进去的 admin / zhangsan
- 交叉核对：UMC 有 10 人 Keycloak 没有

**修法两步**：
1. AppHost 给 `umc-api` 加 `Keycloak__SyncEnabled=true`
2. 新增 `KeycloakBackfillWorker`——**光开开关不会同步已有用户**（同步只在用户增删改时触发）。
   worker 在启动 3s 后做一次全量幂等 upsert，失败只记日志。

**实测**：回填后 Keycloak 从 9 → 19 个用户，UMC 的 12 人**全部补齐**。

### 期间发现并修复：Keycloak person-name 校验

回填时 `zhaoliu` 报 400：

```
{"field":"firstName","errorMessage":"error-person-name-invalid-character"}
```

原因：UMC 把 `NickName` 直接映射成 Keycloak 的 `firstName`，而昵称可以带括号
（`赵六(海星技术)`），Keycloak 的 person-name 校验不接受。

**修法**：`SanitizePersonName()` 只保留字母（含中文）/空格/连字符/撇号，
清洗后为空则回退到用户名；原昵称保留到 `nickname` 属性，避免信息丢失。已加 2 个测试锁定。

## 2. 组织成员管理（补齐「零售用户没后台」的缺口）

零售域用户主数据在 Keycloak（ADR-008），此前禁用/改密/移出只能去 Keycloak
Admin Console 手工操作。

### 接口

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/retail/members` | 本组织成员（含用户名/姓名快照） |
| POST | `/api/retail/members/{id}/status` | 停用/启用，**同时下发 Keycloak `enabled`** |
| POST | `/api/retail/members/{id}/remove` | 移出组织（保留账号，对账器会移出 group） |
| POST | `/api/retail/members/{id}/reset-password` | 重置 Keycloak 密码 |

### 权限模型（核心）

- 只有 `owner` / `manager` 能管理成员
- **不能操作同级或更高级的成员**（manager 管不了 owner 和别的 manager）
- **不能操作自己**
- **不能停用/移除组织内最后一个 owner**（否则组织无主，谁都管不了）

### 快照与自愈

`retail_membership` 新增 `UserName` / `DisplayName` 快照列（加入时写入）。
早于该字段加入的历史成员由 `ListAsync` **惰性自愈**：向 Keycloak 补取一次并写回，
单次最多补 20 个。占位 sub（`kc-` 前缀）跳过。

### 实测

| # | 操作 | 结果 |
|---|---|---|
| 1 | 上海店成员列表 | 7 人，真实成员的姓名已自动补齐 |
| 2 | 停用 `staff174942` | API「已停用」 |
| 3 | 该用户立即登录 | **`invalid_grant`**（Keycloak 账号已禁用） |
| 4 | 重新启用 | API「已启用」 |
| 5 | 权限分级 / 最后一个 owner / 跨租户 | 均有测试覆盖（403） |

测试：零售 **110/110**、UMC **142/142**。

---

# 附：UMC 与 Keycloak 的种子账号密码（易混淆）

**同一个用户名在两个系统里密码不同**，登录失败通常不是 bug：

| 用户 | UMC（5173）密码 | Keycloak（8088）密码 | 说明 |
|---|---|---|---|
| admin | `Admin@123` | `Admin@123` | 一致 |
| **zhangsan** | **`User@123`** | **`Admin@123`** | ⚠️ 不一致，见下 |
| lisi / zhaoliu / 6 名组长 | `User@123` | — | Keycloak 无密码 |
| editor | `Editor@123` | — | Keycloak 无密码 |
| wangwu | `User@123` | — | UMC 侧已禁用 |

## 两个根因

1. **种子数据不一致**：UMC 的 `DbSeeder` 给 zhangsan 用的是 `User@123`，
   而 `octopus-realm.json` 里给的是 `Admin@123`。
   → 已把 realm JSON 对齐为 `User@123`（仅影响全新导入的 realm）。

2. **BCrypt 密码哈希无法迁移到 Keycloak**：
   `KeycloakSyncService.SyncUserAsync` 只同步用户名/邮箱/姓名/启用状态/attributes，
   **不同步密码**（Keycloak 用的是自己的哈希算法）。
   所以 UMC 同步过去的 10 个用户**在 Keycloak 里没有凭据，无法登录任何 OIDC 应用**
   （实测 `lisi` 登录报 `Invalid user credentials`）。

## 让 UMC 用户能登录 Keycloak 的方法

**已在代码里打通**：在 UMC 用户管理里执行「重置密码」，
`UserController` → `KeycloakSyncService.ResetPasswordAsync` 会把新密码下发到 Keycloak：

```csharp
// UserController.ResetPassword
_ = _keycloak.ResetPasswordAsync(u.UserId, u.UserName, req.NewPassword);
```

生产环境更合适的做法是 Keycloak 的 **execute-actions-email**（发"设置密码"邮件），
避免管理员经手明文密码；但需要配置 SMTP。

## 一次性脚本：批量下发 Keycloak 密码

`scripts/dev-reset-keycloak-passwords.sh`

```bash
bash scripts/dev-reset-keycloak-passwords.sh                # 默认 Admin@123
bash scripts/dev-reset-keycloak-passwords.sh 'MyPass@123'   # 指定密码
DRY_RUN=1 bash scripts/dev-reset-keycloak-passwords.sh      # 只打印不改
```

- 用 `retail-invite-bot` 服务账号（已有 `manage-users`），不依赖 master admin 口令
- 跳过 `service-account-*`
- 只重置密码，不创建/删除用户
- 幂等：重复执行结果一致

**⚠️ 仅限本地开发**：它会把所有人的密码设成同一个已知值，等于批量开后门。

实测 19 个用户全部重置成功，`admin / zhangsan / lisi / editor / zhaoliu /
worker100 / staff174942 / client201059` 均可凭 `Admin@123` 登录 Keycloak；
UMC 自身登录不受影响（UMC 仍用自己的 BCrypt 校验）。

### 两套密码并存是设计结果，不是 bug

| 系统 | 权威 | zhangsan 的密码 |
|---|---|---|
| UMC（5173） | `sys_user.PasswordHash`（BCrypt） | `User@123` |
| Keycloak / 零售域（5186） | Keycloak credential | `Admin@123` |

BCrypt 哈希无法迁移到 Keycloak（算法不同），所以两套密码**不会自动保持一致**。
正式环境的正解是 Keycloak 的 execute-actions-email（让用户自己设密码），需要 SMTP。

---

# 安全修复：空密码认证绕过 + 误导性登录错误

## 1. 认证绕过（严重）

`PUT /api/system/user/resetPwd` 对 `NewPassword` **没有任何校验**：

```csharp
// 修复前
u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);  // "" 也会被哈希
return ApiResponse<object?>.Success(null, "密码重置成功");
```

配合登录接口也没有挡空密码，结果是：

```
POST /api/account/login {"userName":"zhangsan","password":""}
→ HTTP 200 + Set-Cookie: umc_session=...      ← 空密码直接登录成功
```

任何知道用户名的人都能登录。触发条件很现实：调用方字段名写错
（DTO 是 `NewPassword`，传了 `password`）→ 反序列化为空串 → 哈希空串 → 后门打开。

**修复**：
- `ResetPassword` 校验 `NewPassword` 非空且 ≥ 8 位，否则 400，且**不改动哈希**
- `Login` 显式拒绝空白密码（纵深防御，即使库里已有坏数据也登不进去）

**回归测试**：`PasswordSecurityTests`（5 个用例，含"库里哈希恰好匹配空串也必须 401"）

## 2. 登录页把所有失败都说成「账号或密码错误」

```ts
// 修复前
} catch (e) {
  if (msg.includes('403') || msg.includes('禁用')) { ... }
  else { errorMsg.value = '账号或密码错误' }   // 网络错误/429/500 全变这句
}
```

排查时会被这句话带偏，一直怀疑密码。**修复**：按 HTTP 状态如实区分——

| 情况 | 提示 |
|---|---|
| 无 response | 无法连接到服务，请确认后端已启动 |
| 429 | 尝试过于频繁，请等一分钟（登录限流 10 次/分钟/IP） |
| 403 | 账号已被禁用 |
| 401 | 用户名或密码错误 |
| 其他 | 后端返回的真实 msg + HTTP 状态码 |

## 3. 密码最终统一为 `Admin@123`

`Admin@123` 同时适用于 **UMC（5173）** 与 **Keycloak / 零售域（5186）**。
原来 zhangsan 在 UMC 是 `User@123`、在 Keycloak 是 `Admin@123`，已通过
UMC `resetPwd`（会一并同步 Keycloak）统一。

> 注意：UMC `DbSeeder` 里的种子密码仍是 `User@123`。
> 删除 `data/octopus_umc.db` 重建后，需要重新执行一次统一操作。

测试：UMC **147/147**。

---

# 种子密码统一为 Admin@123（根治）

上一节只是通过 API 改了**数据**，`DbSeeder` 里仍是 `User@123` / `Editor@123`，
删库重建就会退回多套密码。现已从源头统一。

## 改动

| 位置 | 内容 |
|---|---|
| `DbSeeder.cs` | 10 处 `HashPassword("User@123")` + 1 处 `HashPassword("Editor@123")` → `Admin@123` |
| `DbSeeder.cs` | `sys_config` 的 `sys.user.initPwd`（新增用户初始密码）`User@123` → `Admin@123` |
| `InMemoryDataStore.cs` | 同步 5 处，保持内存兜底数据一致 |
| 测试 | `AccountControllerTests` / `UserDataPermissionTests` 里 4 处登录密码同步 |

## 统一种子密码表（当前）

| 账号 | 密码 | 说明 |
|---|---|---|
| admin / zhangsan / lisi / editor / zhaoliu | `Admin@123` | 全部一致 |
| worker100~105（原 6 名组长） | `Admin@123` | 同上 |
| **wangwu** | `Admin@123` | **Status=0 禁用**，密码正确也登不进（返回 403） |

UMC 与 Keycloak / 零售域现在**同一个密码**，不再需要记忆两套。

## 回归测试

`SeedPasswordPolicyTests`（5 个用例）：

- 4 个启用账号（admin / zhangsan / lisi / editor）必须都能用 `Admin@123` 登录
- wangwu 必须返回 **403 禁用**，而不是 401 密码错误

测试库是从 seeder 新建的，所以这组用例直接验证**种子**而不是现有数据。
有人把密码改回多套时会立刻失败。

## 注意

- 这是**开发环境约定**，生产请勿沿用统一弱密码
- 生产应改为：初始密码随机生成 + 首次登录强制修改（Keycloak `UPDATE_PASSWORD` required action）

测试：UMC **152/152**。

---

# ADR-010：UMC 成为唯一用户主数据源（修正此前的错误规划）

## 之前错在哪

本文前半段（ADR-008）写的是「零售用户不进 UMC」，理由「会污染 9 个子系统」。
这个理由**混淆了两件事**：给 UMC 业务表加 `tenant_id` 确实会波及下游，
但「零售用户在不在 `sys_user`」跟它无关。

后果：`feiwoniu001` 能登录零售域，却在 `5173/system/user` 查无此人，
既停不了用也改不了密——与 UMC 的定位（「统一用户中心，管理全平台用户」）直接矛盾。

## 现在的架构

```
零售注册 ──▶ UMC /api/internal/users/retail ──▶ sys_user(user_type=customer)
                        │                              │
                        └──同步──▶ Keycloak ◀──────────┘（回写 kc_sub）
                                        │
零售域只写成员关系：retail_membership(kc_sub, tenant_id, role)
```

| 保留（ADR-008 正确的部分） | 推翻 |
|---|---|
| `tenant_id` 只落零售库 | ~~零售用户不进 UMC~~ |
| 租户目录用 `retail_tenant`，不借 `sys_dept` | ~~零售直连 Keycloak 建号~~ |
| 用户外键用 Keycloak `sub` | |

## 关键实现

- `sys_user` 新增 `UserType` / `Source` / `KcSub`
- `UserSyncService` **只推 `employee`** —— 客户进 UMC 也不会污染 OA 通讯录
- `POST /api/internal/users/retail`（`X-Internal-Secret`）：建号 + 同步 Keycloak + 回写 `kc_sub`；
  Keycloak 失败时**回滚 UMC 用户**，不留半成品
- `POST /api/internal/users/adopt`：幂等接管存量 Keycloak 账号（随机密码，需重置后才可登 UMC）
- 零售 `InviteService` 改调 `UmcUserClient`，不再直连 Keycloak 建号
- UMC 用户列表加「类型」列 + 筛选

## 实测

| # | 操作 | 结果 |
|---|---|---|
| 1 | 存量 6 个零售用户 adopt | 全部 200 |
| 2 | `5173` 用户列表 | **18 人**（12 员工 + 6 客户） |
| 3 | 按类型筛选 | employee 12 / customer 6 |
| 4 | 新用户凭短码注册 | 200，UMC 里立刻出现（`UserId=112`，customer/retail） |
| 5 | 该用户登录零售域 | `groups=['/tenants/t_shanghai_001']`，工作空间可见 |

## 期间修掉的自愈 bug

`SchemaUpgrader` 补列时没带默认值，导致 12 个老用户 `UserType` 为 NULL，
EF 读非空属性抛 `The data is NULL at ordinal N` → 用户列表接口 500。

两处修正：
1. 补列时带上 EF 模型声明的 `DEFAULT`，并把历史 NULL 回填
2. **执行顺序**：先补列，再重放建表/建索引脚本
   （脚本里有 `CREATE INDEX ... ON sys_user (KcSub)`，先建索引会报 `no such column`）

测试：零售 **110/110**、UMC **157/157**。

---

# 补充：手机号必须落库

## 问题

`feiwoniu001（牛一）` 注册时填了手机号，但 UMC 和零售库两头都查不到。

根因：旧实现里手机号只用于**校验定向邀请**（`invite.Phone == 输入手机号`），
校验完就丢，**从不持久化**。Keycloak 用户对象也没有 phone 字段（只能放 attributes），
所以这个值彻底消失了。

## 修复

| 位置 | 改动 |
|---|---|
| `retail_membership` | 新增 `Phone` 快照列（与 `UserName` / `DisplayName` 一致） |
| `GrantMembershipAsync` | 入店时保存注册填写的手机号 |
| `/api/retail/members` | 返回 `phone`，前端成员列表新增「手机号」列 |
| `POST /api/internal/users/adopt` | 支持补手机号（**只在本地为空时写，不覆盖**） |
| UMC `sys_user.PhoneNumber` | 新注册经内部接口建号时直接写入 |

## 存量回填

手机号还能从**邀请记录**里找回：`retail_invite.AcceptedBySub` 关联成员 sub，
`retail_invite.Phone` 就是当时的手机号。

```
UPDATE retail_membership SET Phone = (
  SELECT i.Phone FROM retail_invite i
   WHERE i.AcceptedBySub = retail_membership.KcSub AND i.Phone IS NOT NULL
   ORDER BY i.InviteId DESC LIMIT 1)
WHERE Phone IS NULL AND EXISTS (...)
```

回填 6 条，再经 `adopt` 幂等同步进 UMC。

## 实测

| 用户 | 手机号 | 来源 |
|---|---|---|
| feiwoniu001 | `1348051774` | 从邀请 #17 反查回填 |
| client201059 | `13600001111` | 邀请 #14 |
| client201216 | `13600002222` | 邀请 #15 |
| member204638 | `13500001111` | 邀请 #19 |
| staff174942 | `13700001234` | 邀请记录 |
| newboss180543 | **（无）** | 该邀请本身没带手机号，无从恢复 |

回归测试：`Register_ValidToken_CreatesAccountAndMembership` 增加断言
`m.Phone.Should().Be("13900001111")`。

零售 110/110。

---

# 补充：手机号双向同步（UMC ⇄ 零售快照）

## 背景

上一节让手机号"落库"了，但只解决了**写入**；两个方向的补齐还缺：

1. 功能上线**之前**注册的用户，零售库快照仍是空（如 `adr010215244`：UMC 有号、零售库没有）
2. 管理员在 5173 改了手机号，零售成员列表不会变

## 做法

### ① UMC → 零售：资料变更回推

新增零售内部接口：

```
POST /api/retail/internal/members/sync-profile
X-Internal-Secret: ...
{ "kcSub": "...", "userName": "...", "displayName": "...", "phone": "..." }
```

- 按 `kcSub` 更新该用户**所有组织**里的快照（一个人可能在多个组织）
- **只覆盖非空字段**，未传的字段保持原值
- 需 `[AllowCrossTenant]`：UMC 不知道 `X-Tenant-Id`

UMC 侧 `RetailProfileSyncService` 在 `PUT /api/system/user` 之后回推（fire-and-forget，
失败只记日志，不阻塞主流程），配置项 `Retail:BaseUrl`。

> Aspire 里用**固定环境变量** `Retail__BaseUrl=http://localhost:5010`，
> 而不是 `WithReference(retailApi)`——retail 已经引用了 umc，反向引用会形成依赖环。

### ② 存量补齐

用同一个内部接口，把 UMC 里有手机号的用户逐个回推，一次补齐零售库：

| 用户 | 手机号 |
|---|---|
| client201059 / client201216 / member204638 | 13600001111 / 13600002222 / 13500001111 |
| feiwoniu001 | 1348051774（先由邀请 #17 反查得到） |
| staff174942 | 13700001234 |
| **adr010215244** | **13600009999**（本次从 UMC 补进零售） |
| zhangsan | 13800000002 |

## 实测

**回推链路**：在 5173 把 `newboss180543` 的手机号改成 `13988887777` →
零售库 `retail_membership(kcSub=2d708bb3…)` **立即**变成 `13988887777`
（同时把此前为空的 `UserName`/`DisplayName` 也补齐了）。

**最终一致性**：

```
UMC 客户 7 人              零售成员列表（上海店 8 人）
feiwoniu001  1348051774  ⟷  feiwoniu001  1348051774
staff174942  13700001234  ⟷  staff174942  13700001234
adr010215244 13600009999  ⟷  adr010215244 13600009999
newboss180543 13988887777 ⟷  (成都春熙路店，zhangsan 无权限查看)
```

测试：零售 **115/115**（新增 `InternalMemberProfileTests` 5 个用例，
覆盖无密钥拒绝、字段更新、空字段不覆盖、未知 sub 返回 0、跨组织一起更新）、
UMC **157/157**。
