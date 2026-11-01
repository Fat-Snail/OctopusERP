# Security Review Skill

对 OctopusUMC/OctopusOA 项目执行安全专项审查。

## 触发场景
- 改动涉及认证、授权、Token、Cookie 相关代码
- 新增或修改 OIDC 配置
- 新增对外暴露的 API 接口

## 审查维度

### 1. Token 安全

**检查项**：
- access_token 是否存入 `localStorage`（应由 `oidc-client-ts` 的 `WebStorageStateStore` 统一管理，或存 `sessionStorage`）
- JWT 签名算法是否为 RS256（非 HS256，避免对称密钥泄漏风险）
- Token 过期时间是否合理（access_token ≤ 1小时，refresh_token ≤ 30天）
- Token 是否被打印到日志或 console

**验证命令**：
```bash
# 检查是否有 token 相关的 console.log
grep -r "console.log.*token\|console.log.*access" --include="*.ts" --include="*.vue" .

# 检查 localStorage 直接操作
grep -r "localStorage.setItem.*token\|localStorage.setItem.*access" --include="*.ts" .
```

### 2. Cookie 安全

**检查项**：
- UMC 认证 Cookie 是否设置 `HttpOnly`（防 XSS 窃取）
- 是否设置 `Secure`（生产环境强制 HTTPS）
- `SameSite` 策略：同域用 `Strict`，跨域 SSO 场景用 `None`（必须配合 `Secure`）

**期望配置**：
```csharp
options.Cookie.HttpOnly = true;
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
options.Cookie.SameSite = SameSiteMode.Lax; // 或 None（跨域时）
```

### 3. OpenIddict 配置安全

**检查项**：
- 生产环境是否替换了 `AddDevelopmentEncryptionCertificate`（应使用真实证书）
- `RequireProofKeyForCodeExchange` 是否已开启（公共客户端必须）
- `redirect_uris` 是否精确匹配（不允许通配符或宽松匹配）
- 客户端类型：浏览器端应为 `Public`（无 client_secret）

### 4. CORS 安全

**危险配置**（禁止）：
```csharp
// 危险：任意来源 + 允许凭据
policy.AllowAnyOrigin().AllowCredentials()
```

**正确配置**：
```csharp
policy.WithOrigins("http://localhost:5174").AllowCredentials()
```

### 5. 密码安全

**检查项**：
- 密码哈希是否使用 BCrypt（work factor ≥ 12）
- 登录失败是否返回统一错误（不暴露"用户不存在"vs"密码错误"的区别）
- 是否有防暴力破解机制（速率限制或账户锁定）

## 输出格式

```
## 安全审查报告

### 高危（立即修复）
- [问题描述] → [修复建议]

### 中危（近期修复）
- [问题描述] → [修复建议]

### 低危/建议
- [问题描述] → [修复建议]

### 通过项
- Token 管理 ✓
- Cookie 配置 ✓
- CORS 策略 ✓
```
