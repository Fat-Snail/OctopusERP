# Testing Rules — {{PROJECT_NAME}}

## Backend — {{TEST_FRAMEWORK}}

### Project layout
```
tests/
├── {{PROJECT_NAME}}.Unit.Tests/        # Domain / pure logic
├── {{PROJECT_NAME}}.Integration.Tests/ # DB + external services (real infra)
└── {{PROJECT_NAME}}.Api.Tests/         # HTTP integration (WebApplicationFactory / supertest)
```

### Test naming
```
<MethodUnderTest>_<Scenario>_<ExpectedOutcome>

Examples:
RegisterUser_WithDuplicateEmail_Returns409
Login_WithCorrectCredentials_SetsSessionAndReturns200
GetOrders_AsUnauthorizedUser_Returns401
```

### Do not mock the database
Integration tests must hit a real database (use Docker or an in-memory provider
with the same SQL dialect). Mocking the DB layer masks real query failures — this
has caused production incidents before.

Only mock:
- External third-party HTTP services (payment, SMS, email)
- Clock / time (for deterministic date tests)

### Critical paths that must have tests

| Path | Test type |
|---|---|
| User registration + login | API integration |
| Auth flow (token issuance / validation) | API integration |
| Permission denied for wrong role | API integration |
| Data isolation (user A can't see user B's data) | Integration |
| Password hashing correctness | Unit |

---

## Frontend — Vitest (or Jest)

### Setup
```bash
{{PACKAGE_MANAGER}} install -D vitest @vue/test-utils jsdom
# or: jest @testing-library/react jest-environment-jsdom
```

### What to test
- **API / service modules**: mock HTTP, verify request params and response mapping
- **Stores / state management**: verify state changes after actions
- **Critical forms**: validation logic, error display, submit flow

### What not to test
- Pure UI layout (high churn, low signal)
- Third-party component library internals

---

## Coverage targets

| Layer | Target |
|---|---|
| Domain / business logic | ≥ 90 % |
| API endpoints (happy path + auth failure) | 100 % of endpoints |
| Frontend services | ≥ 80 % |
| Frontend components | critical paths only |
