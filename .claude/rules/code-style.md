# 编码风格规范

## 整洁架构分层规则

### 依赖方向（严格遵守）
```
OctopusUMC.Api  →  OctopusUMC.Infrastructure  →  OctopusUMC.Core
```
- **Core**：零外部依赖，只含实体、接口、值对象、领域异常
- **Infrastructure**：实现 Core 定义的接口（仓储、DbContext、外部服务）
- **Api**：只做 HTTP 编排，调用 Service/UseCase，不写业务逻辑

### 各层命名约定

| 层次 | 类型 | 命名示例 |
|---|---|---|
| Core/Domain/Entities | 实体 | `User`, `Role` |
| Core/Application/Interfaces | 仓储接口 | `IUserRepository` |
| Core/Application/UseCases | 用例 | `RegisterUserUseCase` |
| Infrastructure/Persistence | DbContext/仓储实现 | `ApplicationDbContext`, `UserRepository` |
| Api/Controllers | 控制器 | `AccountController` |
| Api/DTOs/Requests | 请求体 | `CreateUserRequest`, `UpdateRoleRequest` |
| Api/DTOs/Responses | 响应体 | `UserResponse`, `PagedResult<T>` |

### 后端 C# 规范
- Controller 方法体不超过 10 行，复杂逻辑下沉到 UseCase/Service
- 返回 `IActionResult` 或 `ActionResult<T>`，统一包装响应格式
- 异步方法一律加 `Async` 后缀，使用 `async/await`
- 禁止在 Controller 直接 `new` 领域对象或调用 DbContext
- 所有公开方法参数做空值检查，使用 `ArgumentNullException.ThrowIfNull`

### 统一响应格式（前后端共同遵守）

后端统一返回：
```csharp
// 成功
{ "code": 200, "msg": "操作成功", "data": { ... } }

// 分页列表
{ "code": 200, "msg": "ok", "data": { "rows": [...], "total": 100 } }

// 失败
{ "code": 500, "msg": "用户不存在", "data": null }
```

对应前端泛型类型（见下方 TypeScript 规范）：
```typescript
ApiResponse<T>       // 通用响应
PagedResult<T>       // 分页响应
```

---

## Vue 3 前端规范（基于 vue-pure-admin）

### TypeScript 严格模式（强制）

`tsconfig.json` 必须开启严格模式：
```json
{
  "compilerOptions": {
    "strict": true,
    "noImplicitAny": true,
    "strictNullChecks": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true
  }
}
```

**禁止使用 `any`**。确实无法确定类型时使用 `unknown` 并做类型收窄。

---

### DTO 镜像原则（核心规范）

**前端 TypeScript 类型必须与后端 C# DTO 一一对应，字段名、类型、可选性完全镜像。**

#### 对应关系规则

| C# 类型 | TypeScript 类型 |
|---|---|
| `string` | `string` |
| `int` / `long` | `number` |
| `bool` | `boolean` |
| `DateTime` | `string`（ISO 8601，如 `"2024-01-01T00:00:00Z"`） |
| `Guid` | `string` |
| `T?`（可空） | `T \| null` 或 `T?`（可选属性） |
| `List<T>` | `T[]` |
| `Dictionary<string, T>` | `Record<string, T>` |
| `enum` | 同名 `enum` 或 `union type` |

#### 命名映射

后端 C# 使用 `PascalCase`，序列化为 JSON 时使用 `camelCase`（`System.Text.Json` 默认行为）。
前端 TypeScript 全部用 `camelCase` 对接，与 JSON 字段名一致。

```csharp
// 后端 DTO（C#）
// Api/DTOs/Responses/UserResponse.cs
public class UserResponse
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int Status { get; set; }           // 0=禁用 1=启用
    public string DeptName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public DateTime CreateTime { get; set; }
}
```

```typescript
// 前端类型（TypeScript）
// src/api/system/types.ts
export interface UserResponse {
  userId: number;
  userName: string;
  email: string;
  phoneNumber: string | null;
  status: number;
  deptName: string;
  roles: string[];
  createTime: string;
}
```

#### 请求体同样镜像

```csharp
// Api/DTOs/Requests/CreateUserRequest.cs
public class CreateUserRequest
{
    [Required] public string UserName { get; set; } = string.Empty;
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public long DeptId { get; set; }
    public long PostId { get; set; }
    public List<long> RoleIds { get; set; } = new();
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}
```

```typescript
// src/api/system/types.ts
export interface CreateUserRequest {
  userName: string;
  email: string;
  phoneNumber?: string;
  deptId: number;
  postId: number;
  roleIds: number[];
  status: number;
  remark?: string;
}
```

---

### 类型文件组织规范

每个 API 模块对应一个类型文件，**类型定义与请求函数分开**：

```
src/api/
├── system/
│   ├── user.ts        # 请求函数（调用 http，引用 types.ts 中的类型）
│   ├── role.ts
│   ├── menu.ts
│   ├── dept.ts
│   ├── post.ts
│   ├── dict.ts
│   └── types.ts       # 本模块所有 Request/Response 类型定义
├── monitor/
│   ├── online.ts
│   ├── operlog.ts
│   └── types.ts
└── types.ts           # 全局通用类型（ApiResponse、PagedResult 等）
```

**全局通用类型（`src/api/types.ts`）**：
```typescript
/** 对应后端统一响应包装 */
export interface ApiResponse<T = unknown> {
  code: number;
  msg: string;
  data: T;
}

/** 对应后端分页响应 */
export interface PagedResult<T> {
  rows: T[];
  total: number;
}

/** 对应后端分页请求参数基类 */
export interface PageQuery {
  pageNum: number;
  pageSize: number;
}

/** 通用状态枚举（0=禁用, 1=启用） */
export type Status = 0 | 1;
```

---

### API 请求函数规范

```typescript
// src/api/system/user.ts
import { http } from "@/utils/http";
import type { ApiResponse, PagedResult, PageQuery } from "../types";
import type {
  UserResponse,
  CreateUserRequest,
  UpdateUserRequest,
  UserQuery
} from "./types";

/** 查询用户列表 */
export function getUserList(params: UserQuery & PageQuery) {
  return http.get<ApiResponse<PagedResult<UserResponse>>>("/api/system/user/list", { params });
}

/** 新增用户 */
export function createUser(data: CreateUserRequest) {
  return http.post<ApiResponse<null>>("/api/system/user", { data });
}

/** 修改用户 */
export function updateUser(data: UpdateUserRequest) {
  return http.put<ApiResponse<null>>("/api/system/user", { data });
}

/** 删除用户（支持批量，逗号分隔 id） */
export function deleteUser(ids: string) {
  return http.delete<ApiResponse<null>>(`/api/system/user/${ids}`);
}
```

---

### 组件内类型规范

```vue
<script setup lang="ts">
import type { UserResponse, CreateUserRequest } from "@/api/system/types";
import { getUserList, createUser } from "@/api/system/user";

// Props 必须有明确类型
const props = defineProps<{
  deptId: number;
  visible: boolean;
}>();

// Emits 必须有明确类型
const emit = defineEmits<{
  (e: "update:visible", val: boolean): void;
  (e: "success"): void;
}>();

// ref 变量明确泛型
const tableData = ref<UserResponse[]>([]);
const formData = ref<CreateUserRequest>({
  userName: "",
  email: "",
  deptId: 0,
  postId: 0,
  roleIds: [],
  status: 1,
});

// 函数参数和返回值明确类型
async function loadUsers(page: number, size: number): Promise<void> {
  const res = await getUserList({ pageNum: page, pageSize: size, deptId: props.deptId });
  if (res.code === 200) {
    tableData.value = res.data.rows;
  }
}
</script>
```

---

### 禁止事项

- 禁止 `any`，用 `unknown` + 类型收窄代替
- 禁止在组件中内联定义接口（接口统一放 `types.ts`）
- 禁止用 `as` 强转绕过类型检查（除非有明确注释说明原因）
- 禁止 `// @ts-ignore`，修复类型问题而不是忽略
- 禁止直接操作 `response.data.data`（通过泛型 `ApiResponse<T>` 访问）

---

### 优先使用 pure-admin 内置能力

| 需求 | 使用方式 |
|---|---|
| 发送 HTTP 请求 | `import { http } from "@/utils/http"` |
| 按钮权限控制 | `v-auth="'system:user:add'"` 指令 |
| 权限判断（JS） | `hasAuth("system:user:add")` |
| 消息提示 | `message()` 工具函数 |
| 表格增删改查 | 参考 pure-admin 内置示例页面结构 |

---

### 动态路由接入

后端 `/api/system/menu/tree` 返回菜单路由表，格式对应前端类型：

```typescript
// src/api/system/types.ts
export interface MenuRoute {
  path: string;
  name: string;
  component: string;   // 前端组件路径，如 "system/user/index"
  meta: {
    title: string;
    icon?: string;
    rank?: number;
    roles?: string[];
    auths?: string[];  // 按钮权限标识
    showLink?: boolean;
    keepAlive?: boolean;
  };
  children?: MenuRoute[];
}
```

---

## Docker 规范

- 所有镜像使用明确的版本 tag，禁用 `latest`
- .NET 镜像：`mcr.microsoft.com/dotnet/aspnet:8.0`（运行时），`mcr.microsoft.com/dotnet/sdk:8.0`（构建）
- Node 镜像：`node:20-alpine`
- 容器内应用统一监听 `8080` 端口（非 root）
- 敏感配置通过环境变量注入，禁止硬编码在 Dockerfile 或镜像层
- 多阶段构建：build 阶段用 SDK 镜像，final 阶段用 runtime 镜像
