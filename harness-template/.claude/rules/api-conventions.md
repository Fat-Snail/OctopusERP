# API Conventions — {{PROJECT_NAME}}

## Response envelope

Every endpoint returns this JSON shape:

```json
// Success
{ "code": 200, "msg": "ok", "data": { ... } }

// Paginated list
{ "code": 200, "msg": "ok", "data": { "rows": [...], "total": 100 } }

// Business error
{ "code": 500, "msg": "User not found", "data": null }

// Validation error
{ "code": 400, "msg": "Email is required", "data": null }
```

## HTTP status codes

| Scenario | Status |
|---|---|
| Created | 201 |
| Updated / deleted (no body) | 204 |
| Validation failure | 400 |
| Unauthenticated | 401 |
| Unauthorized (has identity, lacks permission) | 403 |
| Not found | 404 |
| Duplicate / conflict | 409 |
| Unexpected error | 500 |

## REST resource naming

```
GET    /api/{{resource}}              list (paginated)
GET    /api/{{resource}}/:id          single item
POST   /api/{{resource}}              create
PUT    /api/{{resource}}/:id          replace
PATCH  /api/{{resource}}/:id          partial update
DELETE /api/{{resource}}/:id          delete
DELETE /api/{{resource}}/:id,:id,...  batch delete
PUT    /api/{{resource}}/:id/status   status toggle
```

## Pagination query parameters

```
?pageNum=1&pageSize=20&sortBy=createdAt&sortOrder=desc
```

## Authentication

- Session-based APIs (e.g. admin backend): `Cookie` with `HttpOnly; Secure; SameSite`
- Token-based APIs (e.g. mobile, third-party): `Authorization: Bearer <token>`
- Webhook endpoints: `X-Signature: sha256=<HMAC>` (shared-secret verification)

## Endpoint catalogue

> Fill this in as you build. One line per endpoint.
> Format: `METHOD PATH — description`

### Auth
```
POST /api/auth/login    — issue session / token
POST /api/auth/logout   — revoke session / token
GET  /api/auth/me       — current user info
```

### {{MODULE_A}} (fill in)
```
GET    /api/{{module_a}}
POST   /api/{{module_a}}
PUT    /api/{{module_a}}/:id
DELETE /api/{{module_a}}/:id
```

### {{MODULE_B}} (fill in)
```
...
```

## Webhook security (cross-service)

Incoming webhooks from trusted systems use HMAC-SHA256 signature verification:

```
Header: X-Signature: sha256=<hex>
Body:   raw JSON payload

Verification: HMAC-SHA256(body, shared_secret) == header_value
```

Reject with 401 if signature is invalid or missing.
