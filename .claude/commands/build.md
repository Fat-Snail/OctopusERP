# /build — 构建命令

构建指定项目或全部项目。

## 用法
- `/build` — 构建所有后端项目 + 前端
- `/build umc` — 只构建 OctopusUMC（后端 + 前端）
- `/build oa` — 只构建 OctopusOA（后端 + 前端）
- `/build backend` — 只构建两个后端 API
- `/build frontend` — 只构建两个前端

## 执行步骤

### 后端构建
```bash
# UMC
dotnet build OctopusUMC/OctopusUMC.sln --configuration Release

# OA
dotnet build OctopusOA/src/OctopusOA.Api/OctopusOA.Api.csproj --configuration Release
```

### 前端构建
```bash
# UMC Web
cd OctopusUMC/web/OctopusUMC.Web && npm run build

# OA Web
cd OctopusOA/web/OctopusOA.Web && npm run build
```

## 构建成功标准
- 后端：`Build succeeded. 0 Error(s)`，无警告视为干净构建
- 前端：`dist/` 目录生成，无 TypeScript 类型错误

## 常见构建失败原因
- EF Core 设计包未安装 → `dotnet add package Microsoft.EntityFrameworkCore.Design`
- 依赖未安装 → 先执行 `npm install`
- TypeScript 类型错误 → 修复后重新构建，不得使用 `// @ts-ignore` 绕过
