# /migrate — EF Core 迁移管理

管理 OctopusUMC 的数据库迁移。

## 用法
- `/migrate add <名称>` — 创建新迁移
- `/migrate run` — 应用所有待执行迁移
- `/migrate status` — 查看迁移状态
- `/migrate rollback <名称>` — 回滚到指定迁移
- `/migrate remove` — 删除最新一条未应用的迁移

## 执行步骤

### 创建迁移
```bash
# 在 Api 项目目录下执行，--project 指向 Infrastructure
cd OctopusUMC/src/OctopusUMC.Api
dotnet ef migrations add <迁移名称> \
  --project ../OctopusUMC.Infrastructure \
  --output-dir Migrations
```

迁移命名规范：
- `InitialCreate` — 初始建表
- `AddUserEmailIndex` — 添加索引
- `AddRefreshTokenTable` — 新增表
- `RenameUserNameColumn` — 重命名字段

### 应用迁移
```bash
cd OctopusUMC/src/OctopusUMC.Api
dotnet ef database update \
  --project ../OctopusUMC.Infrastructure
```

### 查看迁移状态
```bash
cd OctopusUMC/src/OctopusUMC.Api
dotnet ef migrations list \
  --project ../OctopusUMC.Infrastructure
```
输出中 `[applied]` 表示已执行，无标记表示待执行。

### 回滚迁移
```bash
# 回滚到指定迁移（该迁移之后的所有变更被撤销）
dotnet ef database update <迁移名称> \
  --project ../OctopusUMC.Infrastructure
```

### 删除最新迁移（未应用时才可用）
```bash
dotnet ef migrations remove \
  --project ../OctopusUMC.Infrastructure
```

## 注意事项
- **生产环境禁止自动迁移**，必须手动执行并在部署前备份数据库
- 迁移文件需提交到 git，不要加入 .gitignore
- 修改已应用的迁移文件是危险操作，应创建新迁移来修正
- 如果 `dotnet ef` 命令找不到 DbContext，检查是否缺少 `IDesignTimeDbContextFactory` 实现

## IDesignTimeDbContextFactory 模板
```csharp
// src/OctopusUMC.Infrastructure/Persistence/ApplicationDbContextFactory.cs
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseMySql(
            "Server=localhost;Database=octopus_dev;User=root;Password=root123;",
            ServerVersion.Parse("8.0.x"));
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
```
