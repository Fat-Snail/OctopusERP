# /review — 代码审查

对当前改动执行系统性审查，输出问题清单。

## 执行方式

运行 `/review` 时，Claude 按以下清单逐项检查 `git diff` 中的变更。

---

## 后端审查清单

### 架构合规
- [ ] Controller 方法体 ≤ 10 行，业务逻辑已下沉到 UseCase/Service
- [ ] Core 层没有引用 Infrastructure 或 Api 层的命名空间
- [ ] 没有在 Controller 里直接 `new` 实体或调用 DbContext

### 安全
- [ ] 没有明文存储密码（应使用 `BCrypt.HashPassword`）
- [ ] 没有将敏感信息（密码、Token、连接字符串）写入日志
- [ ] `[Authorize]` 注解没有遗漏在需要保护的接口上
- [ ] CORS 没有使用 `AllowAnyOrigin` + `AllowCredentials` 的危险组合

### 数据库
- [ ] 新实体已添加 DbSet 并创建对应迁移
- [ ] 没有在 Controller 或 UseCase 里直接写 LINQ 查询（应在 Repository）
- [ ] 迁移文件命名有意义（非 `Migration1`）

### API 契约
- [ ] 返回格式符合统一响应规范 `{ success, data }` / `{ success, error }`
- [ ] 状态码符合 api-conventions.md 约定
- [ ] 新接口已在 api-conventions.md 中补充文档

---

## 前端审查清单

### 组件规范
- [ ] 使用 `<script setup lang="ts">`，无 Options API
- [ ] Props 和 Emits 均有类型声明
- [ ] 组件内没有直接调用 axios（应通过 service 层）

### 状态管理
- [ ] 跨组件状态放入 Pinia store，非直接 prop drilling
- [ ] store action 中调用 service，组件只调用 store action

### 安全
- [ ] access_token 没有存入 `localStorage`（应由 oidc-client-ts 的 UserStore 管理）
- [ ] 没有将 Token 打印到 console

---

## Docker 审查清单

- [ ] 镜像 tag 无 `latest`，已固定版本号
- [ ] 多阶段构建：build 阶段和 runtime 阶段分离
- [ ] 敏感配置通过环境变量传入，非硬编码
- [ ] 容器监听端口为 `8080`

---

## 输出格式

审查完成后，按如下格式输出结果：

```
## 审查结果

### 阻断项（必须修复）
- [文件:行号] 问题描述

### 建议项（可选优化）
- [文件:行号] 建议描述

### 通过项
- 架构合规 ✓
- 安全检查 ✓
- ...
```
