# Deploy Review Skill

部署前检查：验证 Docker 配置、端口、环境变量是否正确。

## 触发场景
- 执行 `/deploy` 命令前
- 修改了 Dockerfile 或 docker-compose.yml
- 新增了配置项或环境变量

## 检查步骤

### 1. Dockerfile 检查

```bash
# 列出所有 Dockerfile
find . -name "Dockerfile" -not -path "*/node_modules/*"
```

逐个检查：
- [ ] 是否为多阶段构建（build 阶段 + final 阶段）
- [ ] 基础镜像版本是否固定（无 `latest`）
- [ ] .NET 运行时镜像：`mcr.microsoft.com/dotnet/aspnet:8.0`
- [ ] 应用监听端口是否为 `8080`（`EXPOSE 8080`）
- [ ] 是否以非 root 用户运行（`USER app`）

### 2. docker-compose.yml 检查

```bash
# 验证 compose 文件语法
docker compose config
```

检查项：
- [ ] MySQL 服务启动命令包含 `--character-set-server=utf8mb4`
- [ ] 服务间依赖关系 `depends_on` 正确（umc-api 依赖 mysql）
- [ ] 数据卷已定义（`mysql_data`），避免容器重启后数据丢失
- [ ] 端口映射正确：`7001:8080`（UMC）、`7002:8080`（OA）

### 3. 环境变量检查

**必须通过环境变量注入（不能硬编码）**：
- `ConnectionStrings__Default` — 数据库连接串
- `Jwt__Audience` — JWT audience（如果单独配置）

**检查连接串格式（MySQL）**：
```
Server=mysql;Database=octopus;User=octopus;Password=octopus123;
```
注意：容器内 MySQL 的 host 是服务名 `mysql`，不是 `localhost`。

### 4. 端口冲突检查

```bash
# 检查宿主机端口是否被占用
lsof -i :7001
lsof -i :7002
lsof -i :3306
```

### 5. 启动后验证

```bash
# 检查所有容器状态
docker compose ps

# UMC OIDC 发现文档（服务启动成功的标志）
curl http://localhost:7001/.well-known/openid-configuration | jq .

# OA API 健康检查
curl http://localhost:7002/health
```

## 常见问题速查

| 现象 | 原因 | 解决 |
|---|---|---|
| umc-api 立即退出 | DB 连接失败 | 检查 MySQL 是否已启动，连接串是否正确 |
| OIDC discovery 404 | OpenIddict 未正确配置 | 检查 `UseOpenIddict()` 中间件顺序 |
| 3306 端口冲突 | 宿主机本地 MySQL 占用 | 停止本地 MySQL 或修改 compose 端口映射 |
| 迁移表不存在 | 未执行 `dotnet ef database update` | 进入容器手动执行迁移 |
