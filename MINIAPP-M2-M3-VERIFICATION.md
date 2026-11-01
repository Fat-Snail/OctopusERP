# M2 + M3 验收清单

> Commit: `f9d563c`
> 配套架构文档：[MINIAPP-PLATFORM.md](MINIAPP-PLATFORM.md)
> Aspire 集成基础：commit `d6cb354` / `66cf344` / `738dc0a`

---

## 总览

| 阶段 | 范围 | 状态 |
|---|---|---|
| **M2** | UMC → Keycloak 单向用户同步 | ✅ |
| **M3** | 第一个小程序 demo (miniapp-todo) | ✅ |

测试矩阵：**445 / 445 通过**

| 系统 | 通过 |
|---|---|
| OctopusUMC | 121（+6 P9 M2）|
| OctopusOA | 100 |
| OctopusPLM | 27 |
| OctopusCRM | 38 |
| OctopusWMS | 41 |
| OctopusMES | 68 |
| OctopusFIN | 19 |
| OctopusHRM | 11 |
| OctopusOMS | 10 |
| **miniapp-todo** | **10**（M3 新增） |

---

## M2 验收：UMC → Keycloak 用户同步

### 自动化验证（无需启动）

```bash
cd OctopusUMC
dotnet test tests/OctopusUMC.Api.Tests/OctopusUMC.Api.Tests.csproj \
  --filter "KeycloakSyncServiceTests"
# 期望：6 / 6 通过
```

6 个测试覆盖：

| 测试 | 验证 |
|---|---|
| `SyncUserAsync_Disabled_SkipsAllCalls` | `Keycloak:SyncEnabled=false` 时静默 skip，零 HTTP 调用 |
| `SyncUserAsync_NewUser_PostsCreatePayload` | 用户不存在 → POST `/users`，payload 含 username/enabled/octopus_user_id/scope |
| `SyncUserAsync_ExistingUser_PutsUpdate` | 用户已存在 → PUT `/users/{kcId}`，Status=0 → enabled=false |
| `GetAdminTokenAsync_CachesToken_OnlyHitsTokenEndpointOnce` | Token 缓存生效，3 次调用只打 1 次 token 端点 |
| `SyncUserAsync_OnHttpFailure_DoesNotThrow` | 网络异常被吃掉，不传播（最终一致性）|
| `ResetPasswordAsync_CallsResetEndpoint` | 调 `/users/{id}/reset-password`，temporary=false |

### 手动端到端验证（需启动 Aspire + Keycloak）

```bash
# 1. 启动全栈
bash restart.sh

# 2. 等 Keycloak 就绪
until curl -fs http://localhost:8088/health/ready >/dev/null 2>&1; do
  printf "."; sleep 2
done && echo " ✅"

# 3. 在 UMC 创建一个新用户（用 curl 或 UMC.Web）
#    访问 http://localhost:5173，admin/Admin@123 登录
#    系统管理 → 用户管理 → 新增 → 用户名 testkc / 密码 Test@123

# 4. 验证已同步到 Keycloak
#    浏览器打开 http://localhost:8088
#    admin/admin 登录 → 切到 octopus realm → Users → 应看到 testkc

# 5. 验证 attributes 已注入
#    点 testkc → Attributes 标签
#    应看到：
#      octopus_user_id = <UMC UserId>
#      primary_dept_id = <UMC 主部门 ID>
#      primary_dept_name = <部门名>
#      company_name = <公司名>
#      miniapp_scope = personal

# 6. 在 UMC 禁用该用户（Status=0）
# 7. 回 Keycloak 看 testkc 的 Enabled 列 → 应变红色 ✗
```

### 关键配置

`OctopusUMC/src/OctopusUMC.Api/appsettings.json`：

```json
"Keycloak": {
  "BaseUrl": "http://localhost:8088",
  "Realm": "octopus",
  "SyncBotClientId": "umc-sync-bot",
  "SyncBotClientSecret": "umc-sync-bot-secret-dev",
  "SyncEnabled": "true"
}
```

测试环境通过环境变量覆盖 `Keycloak__SyncEnabled=false` 即可关闭同步。

### 接入点

| 操作 | 触发的 Keycloak 调用 |
|---|---|
| `POST /api/system/user` 新建用户 | POST `/users`（含 attributes） |
| `PUT /api/system/user` 修改用户 | PUT `/users/{kcId}`（含 attributes） |
| `PUT /api/system/user/status` 启用/禁用 | PUT `/users/{kcId}` `{enabled: bool}` |
| `PUT /api/system/user/resetPwd` | PUT `/users/{kcId}/reset-password` |
| `DELETE /api/system/user/{ids}` | DELETE `/users/{kcId}`（每个删除的用户） |

---

## M3 验收：miniapp-todo

### 自动化验证

```bash
dotnet test miniapps/todo/tests/MiniappTodo.Api.Tests/MiniappTodo.Api.Tests.csproj
# 期望：10 / 10 通过
```

10 个测试覆盖：

| 测试 | 验证 |
|---|---|
| `GetMe_ReturnsClaimsAndCreatesLocalProfile` | JWT claims 提取 + Lazy Provisioning UserProfile |
| `UnauthenticatedRequest_Returns401` | （TestAuth 默认通过，验证 header 注入机制）|
| `CreateTodo_PersistsAndReturnsItem` | 创建 Todo + KcSub 自动绑定当前用户 |
| `CreateTodo_RejectsEmptyTitle` | 空 Title 返回 400 |
| `ListTodos_OnlyReturnsCurrentUserItems` | **用户隔离**：Alice 看 2 条，Bob 看 1 条，各自只看到自己的 |
| `UserA_CannotAccessUserBs_Todo` | **跨用户攻击防御**：A 访问 / 改 / 删 B 的 todo 全返回 404 |
| `UpdateTodo_MarksAsDoneAndSetsCompletedAt` | 标记完成时 CompletedAt 自动写入 |
| `DeleteTodo_RemovesItem` | 删除返回 204 |
| `Stats_ReturnsCurrentUserCounts` | 统计 total/done/pending/overdue 按 KcSub 隔离 |
| `ListWithPendingOnlyFilter_ExcludesDone` | pendingOnly=true 过滤已完成 |

### 手动端到端验证

```bash
# 1. 启动 Aspire（包含 keycloak + miniapp-todo）
bash restart.sh

# 2. 等 Keycloak 就绪
until curl -fs http://localhost:8088/health/ready >/dev/null 2>&1; do
  printf "."; sleep 2
done && echo " ✅"

# 3. 用 zhangsan 拿一个 access token（账号已在 realm import 里）
TOKEN=$(curl -s -X POST \
  "http://localhost:8088/realms/octopus/protocol/openid-connect/token" \
  -d "grant_type=password" \
  -d "client_id=octopus-portal" \
  -d "username=zhangsan" \
  -d "password=Admin@123" \
  -d "scope=openid profile email octopus-claims" \
  | python3 -c "import json,sys; print(json.load(sys.stdin)['access_token'])")
echo "Token: $TOKEN"

# 注：octopus-portal 默认未开 password grant，可能要先在 Keycloak 里给该 client 开启
#     Direct Access Grants（Client → Settings → Capability config → Direct access grants on）

# 4. 调用 miniapp-todo
#    /api/me：应返回 claims + profile
curl -H "Authorization: Bearer $TOKEN" http://localhost:5201/api/me | jq

#    POST /api/todos
curl -X POST -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"验收测试","priority":4}' \
  http://localhost:5201/api/todos | jq

#    GET /api/todos
curl -H "Authorization: Bearer $TOKEN" http://localhost:5201/api/todos | jq

#    GET /api/todos/stats
curl -H "Authorization: Bearer $TOKEN" http://localhost:5201/api/todos/stats | jq

# 5. 验证用户隔离：换 admin 登 → 应看到 0 条（zhangsan 的不应出现）
ADMIN_TOKEN=$(curl -s -X POST \
  "http://localhost:8088/realms/octopus/protocol/openid-connect/token" \
  -d "grant_type=password" -d "client_id=octopus-portal" \
  -d "username=admin" -d "password=Admin@123" \
  -d "scope=openid profile email octopus-claims" \
  | python3 -c "import json,sys; print(json.load(sys.stdin)['access_token'])")
curl -H "Authorization: Bearer $ADMIN_TOKEN" http://localhost:5201/api/todos | jq
# 期望：{ rows: [], total: 0 }
```

### Swagger UI 快捷验证

直接访问 `http://localhost:5201/swagger`：

1. 点 **Authorize** → 粘贴 zhangsan 的 access token
2. 试 `/api/me` → 应看到完整 claims（含 octopus_user_id, primary_dept_name 等）
3. 试 `/api/todos` POST → 创建一条
4. 试 `/api/todos` GET → 列表只看到自己的

### 关键设计验证

**【强隔离】** 小程序的 `TodoDbContext` 完全独立于 UMC：

```bash
# 验证 miniapp-todo 没有引用 UMC
grep -rn "OctopusUMC\|UmcUserId\|umc_user" miniapps/todo/src/ 2>/dev/null
# 期望：空（无任何匹配）

# 验证业务表用 KcSub 而非 UMC UserId
grep -n "KcSub\|UmcUserId" miniapps/todo/src/MiniappTodo.Api/Persistence/Entities.cs
# 期望：只看到 KcSub
```

**【运行时不查 UMC】** 启动小程序后任何业务调用都不会触发 UMC API：

```bash
# 监听 UMC API 流量（启动 miniapp-todo + 调用任意接口期间）
# 不应有任何 5001 端口流量
```

---

## 完整启动顺序

```bash
# 1. 启动全栈
bash restart.sh

# 2. 等 Keycloak（约 30 秒）
until curl -fs http://localhost:8088/health/ready >/dev/null 2>&1; do
  printf "."; sleep 2
done && echo " ✅ Keycloak ready"

# 3. 等 miniapp-todo（应该几秒就好）
until curl -fs http://localhost:5201/health >/dev/null 2>&1; do
  printf "."; sleep 1
done && echo " ✅ miniapp-todo ready"

# 4. 打开 Aspire Dashboard
open http://localhost:15256
# 应看到全部 11 个资源 🟢（含 keycloak / keycloak-db / miniapp-todo）

# 5. 打开 miniapp-todo Swagger
open http://localhost:5201/swagger
```

---

## 端口表（更新后）

| 端口 | 服务 | 备注 |
|---|---|---|
| 5001 | UMC API | 含 KeycloakSyncService |
| 5002-5009 | OA/PLM/CRM/WMS/MES/FIN/HRM/OMS | — |
| 5173-5183 | 各前端 Web/H5 | — |
| **5201** | **miniapp-todo** | **新增**，独立小程序后端 |
| **8088** | **Keycloak Admin Console** | admin/admin |
| 15256 | Aspire Dashboard | — |

---

## 下一步（M4：平台化）

如果 M2+M3 验收通过，下一阶段可以：

1. **批量同步现有 6 个 UMC seed 用户到 Keycloak**（一次性脚本）
2. **写一个小程序脚手架 CLI**（`octopus-miniapp create <name>` 一键生成新小程序骨架）
3. **小程序前端 demo**（Vue + oidc-client-ts，验证完整浏览器 SSO 体验）
4. **第二个小程序**：公告板 / 调研问卷 / 简易 CMS

是否继续推进 M4，看你需求。
