# /review — Checklist-driven code review

Run against `git diff` for every PR before merge.

---

## Backend checklist

### Architecture
- [ ] Route/controller handlers are ≤ 10 lines; business logic is in services/use-cases
- [ ] Data access is isolated to the repository / data-access layer
- [ ] No cross-layer dependency violations (see `{{ARCH_LAYERS}}`)

### Security
- [ ] No plaintext secrets, passwords, or tokens in source or logs
- [ ] All protected endpoints have explicit auth guards
- [ ] No dangerous CORS combination (`AllowAnyOrigin` + `AllowCredentials`)

### Database
- [ ] New entities/models have a corresponding migration
- [ ] Queries do not bypass the repository layer
- [ ] Migration file name is meaningful (not `Migration1`)

### API contract
- [ ] Response format matches the project standard (see `rules/api-conventions.md`)
- [ ] HTTP status codes follow the project convention
- [ ] New endpoints are documented in `rules/api-conventions.md`

---

## Frontend checklist

### Components
- [ ] Uses the project's preferred component style (e.g. `<script setup lang="ts">`)
- [ ] Props and emits have explicit types
- [ ] Components do not call the HTTP layer directly — requests go through an API/service module

### State management
- [ ] Cross-component state lives in a store, not prop-drilled
- [ ] Stores own API calls; components call store actions

### Security
- [ ] Access tokens are not written to `localStorage`
- [ ] No tokens or sensitive data logged to the console

---

## Docker checklist

- [ ] Image tags are pinned versions (no `latest`)
- [ ] Multi-stage build: separate build and runtime stages
- [ ] Secrets injected via environment variables, not baked into the image
- [ ] Container listens on the project's standard port

---

## Output format

```
## Review result

### Blockers (must fix)
- [file:line] description

### Suggestions (optional)
- [file:line] description

### Passed
- Architecture ✓
- Security ✓
- API contract ✓
- TypeScript ✓
```
