# 零售多租户 + 门店邀请 验收清单

> 对应设计：`design/retail-tenant-workspace.md`
> 对应决策：`DECISIONS.md` ADR-008 / ADR-009
> 服务：`retail-api` http://localhost:5010 ｜ `retail-web` http://localhost:5186 ｜ Keycloak http://localhost:8088

---

## 0. 验收前置（必读，跳过会误判为失败）

### 0.1 ⚠️ 必须重建 Keycloak realm

`octopus-realm.json` 本次新增了 `groups` 段、`dept_ids` / `groups` mapper、`octopus-retail`、
`retail-invite-bot` 两个 client，以及两个 bot 的 `realm-management` 服务账号角色。

**Keycloak 的 `--import-realm` 只在 realm 不存在时导入**。本机已有持久化卷
`octopus-keycloak-data`（含 5 月导入的旧 realm），所以**不删卷就拿不到新配置**，
现象是 `retail-invite-bot` 建用户返回 403、token 里没有 `groups` claim。

```bash
# 仓库自带入口：已实现「杀 Aspire/子进程 → 释放端口 → 删 KC 容器与数据卷 → 重启 Aspire」
bash restart.sh --reset-kc
```

> 用 `restart.sh` 而不是手工 `docker volume rm`，是因为它还负责释放端口、终止残留的
> `OctopusRetail.Api` / vite 进程；只删卷不重启，旧 Aspire 实例仍会持有旧配置。

> 若不想删卷，可改用 Keycloak Admin Console 手工补配置（groups 段、2 个 client、
> 2 个 mapper、2 个服务账号角色），但**不推荐**——漏一项的表现是运行期 403，很难排查。

### 0.2 前端依赖

`OctopusRetail.Web` 是新增项目，首次需装依赖：

```bash
cd OctopusRetail/web/OctopusRetail.Web && npm install
```

> 在**启动 Aspire 之前**装好。Aspire 的 `AddViteApp` 会在启动时直接跑 `npm run dev`，
> 缺 `node_modules` 时该资源会启动失败（在 Dashboard 里表现为 Exited，而不是不出现）。

### 0.3 清理旧的 retail 开发库（可选）

如需从零验证，删除 `data/octopus_retail.db`，启动时会重建并植入种子。

### 0.4 启动与自检

```bash
bash restart.sh          # 或 cd aspire/OctopusAspire.AppHost && dotnet run
# Dashboard: http://localhost:15256
```

**先在 Dashboard 资源列表里确认 5 个新增资源都在**（若看不到，见下方排障）：

| 资源 | 类型 | 端口 |
|---|---|---|
| `retail-api` | project | 5010 |
| `retail-web` | Vite（container.v1） | 5186 |
| `octopus-retail` / `retail-invite-bot` | Keycloak client（非 Aspire 资源） | — |
| `keycloak` | container | 8088 |

- [ ] Dashboard 中 `retail-api` 为 Running
- [ ] Dashboard 中 `retail-web` 为 Running
- [ ] `curl http://localhost:5010/health` 返回 `{"status":"ok"}`
- [ ] `curl -I http://localhost:5186` 返回 200
- [ ] `retail-api` 在 `keycloak` 之后就绪（`WaitFor(keycloak)` 是必须的：JwtBearer/OIDC 启动即拉 discovery 文档，KC 未就绪会导致首次登录 500 / IDX20803）

<details>
<summary><b>排障：Dashboard 里看不到 <code>retail-web</code></b></summary>

1. **最常见：Aspire 实例是旧构建。** 改动 `AppHost/Program.cs` 后必须重启 `dotnet run`，
   已经在跑的实例不会热加载资源模型。用端口自检：
   ```bash
   lsof -nP -iTCP -sTCP:LISTEN | grep -E ":(5010|5186)"
   # 无输出 = 跑的还是旧的编排
   ```
2. **不启动服务也能验证资源模型**（最快的确诊手段）：
   ```bash
   cd aspire/OctopusAspire.AppHost
   dotnet run --no-launch-profile -- --publisher manifest --output-path /tmp/m.json
   python3 -c "import json;print([k for k in json.load(open('/tmp/m.json'))['resources'] if 'retail' in k])"
   # 期望：['retail-api', 'retail-web']
   ```
3. 若上面两个都正常但 Dashboard 仍无该资源，检查 `retail-web` 是否启动失败（Exited）：
   最常见原因是 `OctopusRetail.Web` 没执行过 `npm install`。
</details>

---

## A. 自动化测试（先跑，成本最低）

```bash
# UMC 侧（M0：group 原语 + 多值 claim）
cd OctopusUMC && dotnet test tests/OctopusUMC.Api.Tests/OctopusUMC.Api.Tests.csproj

# 零售侧（M1–M5：多租户隔离 / 邀请 / 建号 / 对账 / 端到端）
cd OctopusRetail && dotnet test tests/OctopusRetail.Api.Tests/OctopusRetail.Api.Tests.csproj

# 前端类型检查 + 生产构建
cd OctopusRetail/web/OctopusRetail.Web && npm run build
```

- [ ] UMC 测试 **140/140** 通过
- [ ] 零售测试 **66/66** 通过
- [ ] 前端 `vue-tsc` 无类型错误、`vite build` 成功

**必须全绿的用例**（其余可参考，这几条是安全底线）：

| 用例 | 验证的性质 |
|---|---|
| `TenantQueryFilterTests.MemberProfiles_WithDifferentTenantContexts_AreIsolated` | EF 模型缓存不会固化首个请求的租户 |
| `TenantQueryFilterTests.MemberProfiles_WhenTenantUnresolved_ReturnsEmptySet` | fail-closed：未解析租户返回空集而非全集 |
| `TenantQueryFilterTests.TenantScopedEntity_WithoutFilter_GuardThrows` | 新增业务表漏配过滤器 → 启动即失败 |
| `TenantIsolationTests.Members_WithTenantNotInToken_Returns403` | `X-Tenant-Id` 伪造被拒 |
| `InviteTests.CreateInvite_PersistsHashOnly_NeverPlaintext` | 邀请令牌只存哈希 |
| `InviteRegisterTests.Register_PhoneMismatch_IsRejected` | 定向邀请不可被转发冒用 |
| `KeycloakSyncTests.EnsureGroup_Keycloak26Shape_ExpandsViaChildrenEndpoint` | 嵌套组解析（真实 KC 26 响应形态） |

---

## B. 租户与身份上下文

- [ ] 未带 `X-Tenant-Id` 访问租户内接口 → **HTTP 400**，msg 提示声明租户
- [ ] 带一个不属于自己的租户 → **HTTP 403**
- [ ] 带合法租户 → 200，且返回数据的 `tenantId` 与请求头一致
- [ ] 跨租户端点（`/api/retail/me`、`/workspaces`）**无需** `X-Tenant-Id` 即可访问
- [ ] token 的 `groups` 只识别 `/tenants/*`；`/depts/*`、`/oa/*`、`/tenantsX/*` **不得**被当作租户
- [ ] 同一用户可进入多个租户；`/api/retail/me` 的 `availableTenants` 列出全部

```bash
AT=<你的 access_token>

curl -i http://localhost:5010/api/retail/members -H "Authorization: Bearer $AT"                    # 期望 400
curl -i http://localhost:5010/api/retail/members -H "Authorization: Bearer $AT" -H "X-Tenant-Id: t_beijing_002"   # 期望 403
curl -s http://localhost:5010/api/retail/members -H "Authorization: Bearer $AT" -H "X-Tenant-Id: t_shanghai_001"  # 期望 200
```

---

## C. 租户数据隔离

- [ ] `/api/retail/profiles` 的查询语句里**没有** `TenantId` 条件（读源码确认），但切换 `X-Tenant-Id` 返回不同数据集
- [ ] 两个租户的返回结果**无交集**
- [ ] 未解析租户的上下文直接查库，业务表返回 **0 条**

- [ ] 代码走查：`RetailDbContext.OnModelCreating` 中，凡实现 `ITenantScoped` 的实体都有 `HasQueryFilter`
- [ ] 代码走查：新增业务表若只实现 `ITenantScoped` 而漏配过滤器，`TenantFilterGuard` 会抛异常

---

## D. 邀请生命周期

### D.1 权限

- [ ] 店长（`manager`）/ 店主（`owner`）可创建邀请
- [ ] 店员（`staff`）/ 收银（`cashier`）创建邀请 → 业务码 **403**，msg 含"无权邀请成员"
- [ ] 前端对无权限角色隐藏"邀请管理"入口与创建表单

### D.2 创建与安全

- [ ] 手机号与通用邀请码二选一；都不填 → 拒绝
- [ ] `maxUses` 越界（<1 或 >1000）→ 拒绝
- [ ] 创建响应中的 `inviteUrl` **只出现一次**；直接查库，`TokenHash` 是 64 位 hex，且**不等于**返回的明文 token
- [ ] 邀请列表接口**不返回**任何 token（明文与哈希都不返回）

### D.3 状态机

- [ ] 过期邀请接受 → 失败，且库中状态被置为 `expired`
- [ ] 撤销后接受 → 失败，msg 含"已被撤销"
- [ ] 一次性邀请被第二人使用 → 失败，msg 含"已被使用"，且第二人**未**建立成员关系
- [ ] 多次邀请按 `maxUses` 放行，用满后状态转 `accepted`
- [ ] **同一人重复点开已用尽的一次性链接 → 幂等成功**（`alreadyMember=true`），不报"已被使用"
- [ ] 已 `accepted` / `revoked` 的邀请不能撤销（终态不回流）

### D.4 租户绑定

- [ ] A 店的邀请只能进入 A 店，不能跨到 B 店
- [ ] 已有上海店身份的人接受北京店邀请 → 进入北京店，且**上海店的成员关系与角色不受影响**（不存在跨租户提权）
- [ ] 邀请列表按租户隔离：A 店看不到 B 店的邀请
- [ ] 未解析租户的上下文查 `retail_invite` → 0 条（带租户过滤器）

---

## E. 受控自助建号

- [ ] 无账号者可凭邀请链接**匿名**预览"将加入哪家店"
- [ ] 匿名建号成功；Keycloak 中该用户被创建，且 `requiredActions` **不含** `UPDATE_PASSWORD`（被邀请人自设密码）
- [ ] realm 的 `registrationAllowed` 仍为 **false**（未为了建号而开放公开注册）
- [ ] 定向邀请：手机号不匹配 → 拒绝，且**未**发起建号请求
- [ ] 手机号归一化容忍 `+86` / `0086` / 空格 / 连字符
- [ ] 密码 < 8 位 → 拒绝
- [ ] 用户名已被占用 → 建号失败，且**不**建立成员关系、**不**消耗邀请额度
- [ ] Keycloak 未启用时调用 → 明确报错而非静默失败

---

## F. Keycloak group 双写对账

- [ ] 建号后成员关系为 `KcSyncState=pending`
- [ ] 后台对账（默认 60s，可设 `Retail:GroupSyncIntervalSeconds` 调小）自动把成员加入 Keycloak group `/tenants/{tenantId}`
- [ ] 入组成功后状态转 `synced` 且写入 `KcSyncedAt`
- [ ] Keycloak 不可用时保持 `pending`（**不得**误标 `synced`）
- [ ] Keycloak 报错时标 `failed` 且进程不崩，下一轮重试
- [ ] 成员状态改为 `left`/`suspended` 后，对账将其**移出** group
- [ ] 已 `synced` 的记录不会被重复推送
- [ ] 后台任务无租户上下文，但能跨租户扫描（走 `IgnoreQueryFilters`）

> 验证 `F` 需要 Keycloak 可用 + `Keycloak:SyncEnabled=true`。
> 单元测试用假 HTTP 端点覆盖，但**真实 Keycloak 需手工验证一次**（见 G）。

---

## G. 端到端真实环境闭环（本次已实测通过）

> 下面记录 2026-10-12 的实测结果。`sub` 与组 id 为当次实际值。

### G.0 引导第一个店长：自助建店（已解决，无需手工 SQL）

设计上「邀请」需要店长权限，而成员关系来自邀请——**没有引导步骤就永远是空 workspace**。

现在用 `POST /api/retail/tenants` 解决：任何已登录用户都能**自助建店并成为 owner**，
不需要预先存在平台管理员，也不需要手工写 SQL 或操作 Keycloak 组。

```bash
# 1) 以任意已登录用户身份自建门店（跨租户端点，不需要 X-Tenant-Id）
AT=<你的 access_token>
curl -s -X POST http://localhost:5010/api/retail/tenants \
  -H "Authorization: Bearer $AT" -H "Content-Type: application/json" \
  -d '{"tenantCode":"CD001","tenantName":"成都春熙路店","tenantType":"store"}'
```

响应关键字段：

| 字段 | 说明 |
|---|---|
| `tenantId` | 由 code 派生，形如 `t_cd001`（= Keycloak 组名 `/tenants/t_cd001`） |
| `role` | 恒为 `owner` |
| `groupSynced` | 建店时是否已把 owner 同步进 Keycloak 组 |
| `nextStep` | **提示必须重新获取 token** |

- [ ] `groupSynced` 为 `true`（若为 `false`，等一轮对账或查 Keycloak 连通性）
- [ ] **重新登录 / 刷新页面**后再验证——当前 token 的 `groups` claim 不会自动更新
- [ ] 重新登录后 `groups` 含 `/tenants/t_cd001`，workspace 里出现该门店且角色为 owner
- [ ] 该用户能对该门店创建邀请

> 首次验证用**全新用户**最直观：建店前 `workspace total = 0`，建店+重新登录后变为 1。

### G.0.1 建店校验与防护

- [ ] 编码格式非法（空/单字符/中文/含空格或斜杠）→ 拒绝，提示"租户编码…"
- [ ] 编码重复（大小写不敏感，含种子里的 `SH001`）→ 拒绝，提示"已被占用"
- [ ] 门店名为空 → 拒绝
- [ ] `tenantType` 不在 `store|chain|franchise` 内 → 拒绝
- [ ] 指定的 `parentTenantId` 不存在 → 拒绝
- [ ] 单用户拥有门店数超过上限（`MaxOwnedTenants = 10`）→ 拒绝，提示"上限"
- [ ] Keycloak 不可用时仍能建店成功，但 `groupSynced=false` 且成员关系为 `pending`（留给对账器）

### G.1 实测步骤与结果

| # | 操作 | 期望 | 实测 |
|---|---|---|---|
| 1 | zhangsan 登录，读 token claims | `dept_ids=['3','10']`（多部门不丢） | ✅ |
| 2 | `GET /api/retail/me` | 200，`availableTenants` 含上海店 | ✅ |
| 3 | `GET /api/retail/workspaces` | 上海南京路店 / manager | ✅ |
| 4 | `POST /api/retail/invites`（带 `X-Tenant-Id: t_shanghai_001`） | 200，返回一次性 `inviteUrl` | ✅ |
| 5 | `GET /api/retail/invites/preview?token=…`（**匿名**） | 上海南京路店 / cashier / usable | ✅ |
| 6 | `POST /api/retail/invites/register`（**匿名**） | 200，账号已创建 | ✅ |
| 7 | 越权：`X-Tenant-Id: t_beijing_002` | **403** 无权访问该租户 | ✅ |
| 8 | 越权：缺 `X-Tenant-Id` | **400** 缺少租户上下文 | ✅ |
| 9 | 等 8s 后查 Keycloak | newstaff 已在 `/tenants/t_shanghai_001` | ✅ |
| 10 | 查 retail 库对账状态 | `KcSyncState=synced` | ✅ |
| 11 | newstaff 登录 | `groups=['/tenants/t_shanghai_001']` | ✅ |
| 12 | newstaff 查 workspace / 本店成员 | 上海南京路店 / cashier；成员数 4 | ✅ |

- [ ] 重复上述 1–12，结果一致

---

## H. 前端（浏览器）

打开 http://localhost:5186

- [ ] 未登录访问受保护路由 → 跳转 Keycloak 登录，登录后回到原页面
- [ ] 顶栏 workspace 切换器显示当前门店；下拉列出可进入的全部门店与各自角色
- [ ] 切换门店后，页面数据随之变化；刷新后仍保持所选门店
- [ ] 手动改 `localStorage['retail.activeTenantId']` 为一个不属于自己的门店 → 请求被 **403** 拒绝（前端状态不构成授权）
- [ ] 店长/店主可见"邀请管理"入口；店员/收银不可见
- [ ] 邀请管理页可创建邀请，**创建后明文链接只展示一次**，可复制
- [ ] 邀请列表展示状态徽标（待接受/已接受/已过期/已撤销）与使用次数
- [ ] 可撤销待接受的邀请；已终结的邀请无撤销按钮
- [ ] 邀请落地页（`/invite/accept?token=…`）未登录时展示门店名 + 注册表单 + "已有账号去登录"
- [ ] 落地页已登录时直接显示"加入该门店"
- [ ] 失效/伪造 token → 页面明确提示，不出现白屏

---

## I. 已知缺口（不作为本次验收失败项，但需知悉）

| 缺口 | 影响 | 现状 |
|---|---|---|
| ~~无「租户初始化」接口~~ | — | ✅ **已补**：`POST /api/retail/tenants` 自助建店并成为 owner（见 G.0） |
| 种子成员用占位 sub | 真实用户登录后 workspace 为空；本店成员数含占位项 | ✅ 已标注 `KcSyncState=unmanaged`，对账器不再无效重试；真实门店请用自建 |
| 短信 / 企微触达未接 | 邀请链接需人工复制发送 | 接口已返回 `inviteUrl`，仅差发送渠道 |
| `retail_tenant` 无 CRUD | 租户只能靠种子/手工 SQL | 待补 |
| 关店无级联清理 | 门店关闭后 Keycloak 组残留 | 待补租户生命周期状态机 |
| group 对账为单向 | Keycloak 侧手工加组不会被业务库纠正 | 设计如此，需反向对账能力才闭环 |
| 租户粒度未定 | 「一店一租户且上万门店」的 token 体积未压测 | 见设计文档 §10 第 1 条 |
| 生产迁移未做 | retail_db 仍走 SQLite `EnsureCreated()` | 切 MySQL 需启用 EF Migrations（ADR-007） |

---

## J. 验收签署

| 项 | 结论 | 备注 |
|---|---|---|
| A 自动化测试 | ☐ 通过 ☐ 不通过 | |
| B 身份上下文 | ☐ 通过 ☐ 不通过 | |
| C 租户隔离 | ☐ 通过 ☐ 不通过 | |
| D 邀请生命周期 | ☐ 通过 ☐ 不通过 | |
| E 受控建号 | ☐ 通过 ☐ 不通过 | |
| F group 对账 | ☐ 通过 ☐ 不通过 | |
| G 端到端真实闭环 | ☐ 通过 ☐ 不通过 | |
| H 前端 | ☐ 通过 ☐ 不通过 | |

验收人：____________　日期：____________
