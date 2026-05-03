# {{PROJECT_NAME}} — Claude Code Guide

## Project overview

<!-- One paragraph: what this system does, who uses it, why it exists. -->

| System | Role |
|---|---|
| **{{SERVICE_A}}** | <!-- e.g. Identity Provider (IdP), manages auth for the platform --> |
| **{{SERVICE_B}}** | <!-- e.g. Core business app, SSO client of Service A --> |

**Key constraints**:
- <!-- e.g. TDD required: tests first, then implementation -->
- <!-- e.g. Service B sessions must sync-logout with Service A -->

---

## Directory layout

```
/
├── {{SERVICE_A}}/
│   ├── src/
│   │   └── {{SERVICE_A}}.Api/
│   └── tests/
│       └── {{SERVICE_A}}.Api.Tests/
├── {{SERVICE_B}}/
│   ├── src/
│   │   └── {{SERVICE_B}}.Api/
│   └── tests/
│       └── {{SERVICE_B}}.Api.Tests/
├── docker-compose.yml
├── CLAUDE.md
└── TODO.md
```

---

## Tech stack

| Layer | Technology | Notes |
|---|---|---|
| Backend runtime | {{BACKEND_RUNTIME}} | |
| Auth framework | {{AUTH_TECH}} | |
| ORM | {{ORM}} | |
| DB | {{DB_TECH}} | |
| Frontend | {{FRONTEND_TECH}} | |
| UI components | {{UI_LIB}} | |
| Backend tests | {{TEST_FRAMEWORK}} | |
| Frontend tests | {{FRONTEND_TEST}} | |

---

## Default ports

| Service | URL | Notes |
|---|---|---|
| {{SERVICE_A}} API | http://localhost:{{PORT_A_API}} | |
| {{SERVICE_A}} Web | http://localhost:{{PORT_A_WEB}} | |
| {{SERVICE_B}} API | http://localhost:{{PORT_B_API}} | |
| {{SERVICE_B}} Web | http://localhost:{{PORT_B_WEB}} | |
| Database | localhost:{{PORT_DB}} | |

---

## Development commands

```bash
# Start all services (choose one)
{{START_CMD}}
# e.g. cd aspire/AppHost && dotnet run
# e.g. docker compose up -d

# Run backend tests
{{TEST_CMD}}
# e.g. dotnet test

# Run frontend tests
cd {{FRONTEND_DIR}} && {{PACKAGE_MANAGER}} test

# Type check frontend
cd {{FRONTEND_DIR}} && npx tsc --noEmit

# Rebuild after seed data change
{{SEED_REBUILD_CMD}}
# e.g. rm src/MyApp.Api/app.db && dotnet run
```

---

## Architecture principles

### Clean architecture (backend)

```
{{ARCH_LAYERS}}
```

- **Core**: no external dependencies. Entities, interfaces, domain events.
- **Infrastructure**: implements Core interfaces. DB, external services.
- **API**: HTTP only. Controllers ≤ 10 lines. No business logic.

### TypeScript strict mode (frontend)

- `strict: true` in `tsconfig.json` — no exceptions.
- No `any`. No `@ts-ignore`. No `as` cast without an explanatory comment.
- All frontend types mirror backend DTOs exactly (see `rules/code-style.md`).

### TDD workflow

```
1. Write failing test (Red)
2. Write minimal implementation to pass (Green)
3. Refactor — tests must stay green
```

Every new feature requires a test file before the implementation file.

---

## API conventions

Unified response envelope for all endpoints:
```json
{ "code": 200, "msg": "ok", "data": { ... } }
{ "code": 200, "msg": "ok", "data": { "rows": [...], "total": 100 } }
{ "code": 500, "msg": "error message", "data": null }
```

See `.claude/rules/api-conventions.md` for the full endpoint catalogue.

---

## Feature checklist (fill in per project)

### {{SERVICE_A}} features
- [ ] <!-- Authentication -->
- [ ] <!-- User management -->
- [ ] <!-- ...etc -->

### {{SERVICE_B}} features
- [ ] <!-- Core business feature A -->
- [ ] <!-- Core business feature B -->
- [ ] <!-- ...etc -->

---

## Known pitfalls

| Problem | Cause | Fix |
|---|---|---|
| <!-- e.g. CORS preflight 401 --> | <!-- middleware order wrong --> | <!-- UseCors before UseAuthentication --> |
| <!-- e.g. Token audience mismatch --> | <!-- client_id ≠ Audience --> | <!-- must match exactly --> |
| <!-- add project-specific pitfalls here --> | | |

---

## Collaboration style

When asking Claude for help, include: **phase + component + specific task**

```
"Implement the password-reset flow in {{SERVICE_A}}.Api using TDD"
"Add the order history page in {{SERVICE_B}}.Web, wire to real API"
"Debug the 401 on /api/orders — token is present but rejected"
```
