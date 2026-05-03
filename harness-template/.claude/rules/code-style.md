# Code Style Rules — {{PROJECT_NAME}}

These rules are always active. Claude must follow them in every response.

---

## Architecture

### Layer dependency direction (strict, one-way)
```
{{ARCH_LAYERS}}
```

- **Core / Domain**: zero external dependencies. Entities, interfaces, domain events, exceptions.
- **Infrastructure**: implements core interfaces. DB context, repositories, external services.
- **API / Presentation**: HTTP orchestration only. Handlers call use-cases/services. No business logic.

### Naming by layer

| Layer | Type | Example |
|---|---|---|
| Domain | Entity | `User`, `Order` |
| Domain | Repository interface | `IUserRepository` |
| Application | Use case | `RegisterUserUseCase` |
| Infrastructure | DB context / repo impl | `AppDbContext`, `UserRepository` |
| API | Controller / handler | `UserController` |
| API | Request DTO | `CreateUserRequest` |
| API | Response DTO | `UserResponse`, `PagedResult<T>` |

### Backend rules
- Handler / controller methods ≤ 10 lines
- All async methods have an `Async` suffix
- No `new Entity()` inside controllers — instantiate in use-cases
- Validate at system boundaries (HTTP input, external API responses)

### Unified response format

```json
{ "code": 200, "msg": "ok",    "data": { ... } }
{ "code": 200, "msg": "ok",    "data": { "rows": [...], "total": 42 } }
{ "code": 500, "msg": "error", "data": null }
```

---

## TypeScript / Frontend

### Strict mode (mandatory)
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

Forbidden: `any`, `@ts-ignore`, `as` casts without explanatory comment.

### DTO mirroring (core rule)

**Frontend TypeScript types must mirror backend DTOs 1-to-1.**
Field names (camelCase), types, and optionality must match exactly.

| Backend type | TypeScript type |
|---|---|
| `string` | `string` |
| `int` / `long` / `number` | `number` |
| `bool` / `boolean` | `boolean` |
| `DateTime` / `date` | `string` (ISO 8601) |
| `T?` (nullable) | `T \| null` |
| `List<T>` / `T[]` | `T[]` |
| `enum` | same-name `enum` or union type |

### Type file organisation

```
src/api/
├── types.ts          # Global: ApiResponse<T>, PagedResult<T>, PageQuery
├── {{MODULE_A}}/
│   ├── index.ts      # Request functions
│   └── types.ts      # Module-specific Request/Response types
└── {{MODULE_B}}/
    ├── index.ts
    └── types.ts
```

### Global types (keep in `src/api/types.ts`)
```typescript
export interface ApiResponse<T = unknown> {
  code: number;
  msg: string;
  data: T;
}

export interface PagedResult<T> {
  rows: T[];
  total: number;
}

export interface PageQuery {
  pageNum: number;
  pageSize: number;
}
```

---

## Docker

- Images: pinned version tags, no `latest`
- Multi-stage builds: build image vs runtime image
- App listens on port `8080` inside container
- Secrets via environment variables only
- Non-root user inside container
