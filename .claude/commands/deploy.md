# /deploy — Docker Compose 部署

构建镜像并启动完整的本地/生产环境。

## 用法
- `/deploy` — 完整部署（构建 + 启动 + 验证）
- `/deploy build` — 只重新构建镜像
- `/deploy up` — 只启动（不重新构建）
- `/deploy down` — 停止并移除容器
- `/deploy logs <service>` — 查看指定服务日志

## 执行步骤

### 完整部署流程

**Step 1：构建镜像**
```bash
docker compose build --no-cache
```

**Step 2：启动服务**
```bash
docker compose up -d
```

**Step 3：等待 MySQL 就绪**
```bash
# 等待 MySQL 健康检查通过（最多 60 秒）
docker compose ps mysql
```

**Step 4：执行数据库迁移**
```bash
docker compose exec umc-api dotnet ef database update
```

**Step 5：验证服务健康**
```bash
# UMC API
curl -f https://localhost:7001/.well-known/openid-configuration

# OA API
curl -f https://localhost:7002/health

# 查看所有容器状态
docker compose ps
```

## 验收标准
- [ ] 所有容器状态为 `running`（非 `exited`）
- [ ] UMC OIDC 发现文档可访问
- [ ] OA API 健康检查返回 200
- [ ] MySQL 数据库中存在 OpenIddict 相关表

## 常见部署问题

| 问题 | 排查命令 |
|---|---|
| 容器启动后立即退出 | `docker compose logs <service>` |
| MySQL 连接失败 | 检查 `ConnectionStrings__Default` 环境变量 |
| 端口被占用 | `lsof -i :7001` 找到并终止占用进程 |
| 迁移失败 | 确认 MySQL 容器已健康，迁移命令在 umc-api 容器内执行 |

## 停止与清理
```bash
# 停止服务（保留数据卷）
docker compose down

# 停止服务并清除数据卷（慎用，会丢失数据库数据）
docker compose down -v
```
