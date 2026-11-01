# SSO Flow Review Skill

审查 SSO 登录流程的完整性，防止常见配置错误。

## 触发场景
- 实现或修改 OIDC 相关代码（AuthorizationController、oidcService.ts）
- 新增 OIDC 客户端注册
- SSO 流程出现异常（登录后无法跳回、Token 无效等）

## SSO 完整流程图

```
用户点击"登录"
    │
    ▼
OA.Web: oidcService.login()
    │  signinRedirect() 生成 state + code_verifier(PKCE)
    ▼
浏览器跳转到 UMC.Api /connect/authorize
    │  参数: client_id, redirect_uri, scope, code_challenge, state
    ▼
UMC.Api: AuthorizationController
    │  检查用户是否已有 Cookie 会话
    ├─ 无会话 → 重定向到 UMC.Web /login
    │               用户输入账号密码
    │               POST /api/account/login → 设置 Cookie
    │               跳回 /connect/authorize
    └─ 有会话 → 生成授权码(code)，重定向到 redirect_uri
    │
    ▼
浏览器跳转到 OA.Web /callback?code=xxx&state=xxx
    │
    ▼
OA.Web: Callback.vue
    │  oidcService.handleCallback()
    │  用 code + code_verifier 换 access_token + id_token
    │  POST /connect/token（在浏览器端发起）
    │
    ▼
OA.Web 获得 tokens，存入 oidc-client-ts UserStore
    │
    ▼
OA.Web 带 Authorization: Bearer <access_token> 调用 OA.Api
    │
    ▼
OA.Api: JWT Bearer 中间件验证 Token
    │  向 UMC.Api /.well-known/openid-configuration 获取公钥
    └─ 验证通过 → 执行业务逻辑
```

## 逐步排查清单

### Step 1：验证 UMC 客户端注册
```bash
# 查询 openiddict_applications 表
# 确认以下字段正确
SELECT client_id, client_type, redirect_uris, permissions
FROM openiddict_applications
WHERE client_id = 'octopus-oa-web';
```

检查：
- [ ] `client_id` = `octopus-oa-web`
- [ ] `client_type` = `public`
- [ ] `redirect_uris` 包含 `http://localhost:5174/callback`（URL 精确匹配）
- [ ] `permissions` JSON 包含所有必要权限

### Step 2：验证 OA.Web OIDC 配置
检查 `oidcService.ts`：
- [ ] `authority` = `https://localhost:7001`（与 UMC.Api 地址一致）
- [ ] `client_id` = `octopus-oa-web`（与数据库注册完全一致）
- [ ] `redirect_uri` = `http://localhost:5174/callback`（精确一致）
- [ ] `scope` 包含 `openid`（必须）+ `offline_access`（获取 refresh_token）

### Step 3：验证 Callback.vue
- [ ] 路由 `/callback` 已在 `router/index.ts` 中注册
- [ ] `onMounted` 调用 `oidcService.handleCallback()`
- [ ] 回调成功后跳转到首页（非 `/callback`）
- [ ] 没有 `meta.requiresAuth: true`（回调页不需要鉴权）

### Step 4：验证 OA.Api JWT 配置
- [ ] `Authority` 末尾有斜杠：`https://localhost:7001/`
- [ ] `Audience` = `octopus-oa-web`（与 client_id 一致）
- [ ] `UseAuthentication()` 和 `UseAuthorization()` 均已注册到中间件管道

### Step 5：验证 CORS
- [ ] UMC.Api 的 CORS 白名单包含 OA.Web 的 origin
- [ ] `UseCors` 在 `UseAuthentication` 之前

## 常见故障与解决

| 故障现象 | 根本原因 | 解决方案 |
|---|---|---|
| 跳转 UMC 后白屏 | UMC.Web 未运行或地址错误 | 确认 UMC.Web 在 5173 运行 |
| `redirect_uri_mismatch` 错误 | DB 注册地址与实际 redirect_uri 不一致 | 精确匹配，注意末尾斜杠 |
| Callback 页面死循环 | router guard 未排除 `/callback` 路由 | 给 callback 路由加 `meta.requiresAuth: false` |
| Token 验证 401 | audience 不匹配 | OA.Api Audience 必须等于 UMC 的 client_id |
| `invalid_client` | client_id 拼写错误 | 检查 oidcService.ts 与数据库注册的 client_id |
| PKCE `code_verifier` 失败 | state 丢失（页面刷新） | 检查 sessionStorage 是否正常，不要在 authorize 前清空 storage |
