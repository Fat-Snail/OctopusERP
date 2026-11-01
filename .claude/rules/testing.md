# 测试规范

## 后端：xUnit

### 项目结构
```
tests/
├── OctopusUMC.Core.Tests/           # 领域逻辑单元测试
├── OctopusUMC.Infrastructure.Tests/ # 仓储集成测试（真实数据库）
└── OctopusUMC.Api.Tests/            # API 集成测试（WebApplicationFactory）
```

### 命名规范
```csharp
// 方法名：被测方法_场景_预期结果
[Fact]
public async Task RegisterUser_WithDuplicateEmail_ThrowsDomainException() { }

[Theory]
[InlineData("", "邮箱不能为空")]
[InlineData("invalid", "邮箱格式无效")]
public async Task RegisterUser_WithInvalidEmail_ReturnsValidationError(string email, string expectedMsg) { }
```

### 禁止 Mock 数据库
- 集成测试必须连接真实 MySQL（用 Docker 起测试库）
- 只 Mock 外部第三方服务（OSS、短信等）
- 理由：Mock DB 导致测试与生产行为不一致，曾踩坑

### 关键测试场景

| 模块 | 必测场景 |
|---|---|
| 注册 | 正常注册、邮箱重复、密码哈希正确性 |
| 登录 | 正确凭据发 Cookie、错误密码返回 401、账号禁用返回 403 |
| OIDC | authorize 端点重定向逻辑、token 端点颁发 JWT、PKCE 校验失败 |
| JWT 验证（OA） | 有效 Token 放行、过期 Token 返回 401、篡改签名返回 401 |
| 角色授权 | 有权限角色成功、无权限角色返回 403 |

---

## 前端：Vitest

### 安装
```bash
npm install -D vitest @vue/test-utils jsdom @testing-library/vue
```

### 配置（vite.config.ts）
```typescript
test: {
  environment: 'jsdom',
  globals: true,
  setupFiles: ['./src/test/setup.ts'],
}
```

### 测试范围
- **Services**：mock axios，验证请求参数和响应处理
- **Stores**：验证 action 调用后 state 变化
- **关键组件**：Login 表单校验、Callback 路由跳转逻辑

### 不需要测试
- 纯 UI 布局组件（改动频繁，维护成本高）
- Element Plus 组件本身的行为
