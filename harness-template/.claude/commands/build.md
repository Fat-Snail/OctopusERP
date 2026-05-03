# /build — Build commands

Build one or all parts of {{PROJECT_NAME}}.

## Usage
- `/build` — build everything (backend + frontend)
- `/build backend` — backend only
- `/build frontend` — frontend only
- `/build <service>` — a named service (e.g. `/build api`, `/build web`)

## Backend build

```bash
{{BACKEND_BUILD_CMD}}
# e.g. dotnet build MyApp.sln --configuration Release
# e.g. go build ./...
# e.g. ./gradlew build
```

Success criterion: **0 errors, 0 warnings** (treat warnings as errors in CI).

## Frontend build

```bash
cd {{FRONTEND_DIR}}
{{PACKAGE_MANAGER}} install
{{FRONTEND_BUILD_CMD}}
# e.g. npm run build
# Outputs to dist/
```

Success criterion: `dist/` directory created, zero TypeScript type errors.

## Common failures

| Symptom | Fix |
|---|---|
| Missing package / module not found | Run `{{PACKAGE_MANAGER}} install` first |
| TypeScript errors | Fix the types — never use `// @ts-ignore` to silence |
| DB design-time tools missing | Install the ORM CLI tool (e.g. `dotnet tool install dotnet-ef`) |
| Environment variable missing | Copy `.env.example` → `.env` and fill in required values |
