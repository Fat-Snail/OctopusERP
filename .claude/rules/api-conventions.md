# API 约定

## UMC API 端点清单

### 账户接口（Cookie 认证）
```
POST /api/account/register       → 注册用户，返回 201
POST /api/account/login          → 登录，颁发 HttpOnly Cookie
POST /api/account/logout         → 登出，清除 Cookie
GET  /api/account/me             → 返回当前登录用户信息
```

### OIDC 端点（OpenIddict 处理）
```
GET/POST /connect/authorize      → 授权端点（AuthorizationController 处理直通）
POST     /connect/token          → 令牌端点
GET      /connect/userinfo       → 用户信息端点
GET      /connect/logout         → 登出端点
GET      /.well-known/openid-configuration → OIDC 发现文档（自动生成）
```

### 管理端接口（需 Admin 角色）
```
GET    /api/admin/clients          → 列出所有 OIDC 客户端
POST   /api/admin/clients          → 注册新客户端
PUT    /api/admin/clients/{id}     → 更新客户端配置
DELETE /api/admin/clients/{id}     → 删除客户端

GET    /api/admin/roles            → 列出角色
POST   /api/admin/roles            → 创建角色
PUT    /api/admin/roles/{id}       → 更新角色
DELETE /api/admin/roles/{id}       → 删除角色
POST   /api/admin/users/{id}/roles → 为用户分配角色
```

## OA API 端点清单

### 基础接口
```
GET /api/me                        → 返回当前用户 Claims（需 Bearer Token）
```

### 公告模块
```
GET    /api/bulletins              → 分页列表
POST   /api/bulletins              → 创建 [Policy: CanCreateBulletin]
GET    /api/bulletins/{id}         → 详情
```

### 审批模块
```
GET    /api/approvals              → 当前用户相关审批列表
POST   /api/approvals              → 提交申请
PUT    /api/approvals/{id}/approve → 审批通过 [Policy: CanApprove]
PUT    /api/approvals/{id}/reject  → 审批拒绝 [Policy: CanApprove]
```

### 文件模块
```
POST   /api/files/presign          → 获取 OSS 预签名上传 URL
GET    /api/files/{id}             → 文件元数据
```

---

## OpenIddict 关键配置要点

### 必须开启的权限（ApplicationDescriptor）
注册 OA 客户端时，以下 Permissions 缺一不可：
```
Endpoints.Authorization
Endpoints.Token
Endpoints.Logout
GrantTypes.AuthorizationCode
GrantTypes.RefreshToken
ResponseTypes.Code
Scopes.Email
Scopes.Profile
Scopes.Roles
```

### PKCE 强制要求
- 所有 `ClientType = Public` 的客户端必须使用 PKCE
- 服务端配置：`options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()`

### 中间件注册顺序（Program.cs）
```csharp
// 顺序错误会导致 CORS 或认证失败
app.UseCors("OctopusPolicy");        // 1. CORS 必须最先
app.UseAuthentication();             // 2. 认证
app.UseAuthorization();              // 3. 授权
```

### OA.Api JWT 验证
- `Authority` = UMC.Api 的完整地址（含末尾斜杠）
- `Audience` = UMC 中注册的 `client_id`，必须完全一致
- `RequireHttpsMetadata = false` 仅限开发环境

---

## HTTP 状态码约定

| 场景 | 状态码 |
|---|---|
| 创建成功 | 201 Created |
| 操作成功（无返回体） | 204 No Content |
| 参数校验失败 | 400 Bad Request |
| 未认证 | 401 Unauthorized |
| 无权限 | 403 Forbidden |
| 资源不存在 | 404 Not Found |
| 业务冲突（如邮箱重复） | 409 Conflict |
