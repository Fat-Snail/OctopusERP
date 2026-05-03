# Security Review Skill

Triggered review of the auth and data-security chain in {{PROJECT_NAME}}.

## Trigger
- Any change to auth, session, token, cookie, or CORS code
- New endpoint added to a protected resource
- Changes to the auth server / identity provider configuration

## Review dimensions

### 1. Token security

- [ ] Access tokens are NOT in `localStorage` (use `sessionStorage` or
      framework-managed secure storage)
- [ ] JWT signing uses an asymmetric algorithm (RS256 / ES256)
- [ ] Token expiry: access ≤ 1 h, refresh ≤ 30 d
- [ ] Tokens are not written to logs or console

```bash
grep -r "localStorage.*token\|console\.log.*token" \
  --include="*.ts" --include="*.vue" --include="*.js" .
```

### 2. Cookie security

Expected config (adapt to framework):
```
HttpOnly:   true
Secure:     Always  (production)
SameSite:   Lax  (or None + Secure for cross-site SSO)
```

### 3. Auth server / OIDC

- [ ] Production uses a real signing certificate (not a dev / self-signed cert)
- [ ] PKCE required for all public/browser clients
- [ ] `redirect_uris` are exact-match only — no wildcards
- [ ] Client secrets are not committed to source control

### 4. CORS

Forbidden pattern:
```
AllowAnyOrigin()  +  AllowCredentials()   ← rejects at browser level + security hole
```

Required pattern:
```
WithOrigins("https://app.example.com").AllowCredentials()
```

### 5. Password / secret storage

- [ ] Passwords are hashed with bcrypt (work factor ≥ 12) or argon2
- [ ] Login error message does not distinguish "user not found" from "wrong password"
- [ ] Rate limiting / lockout on login attempts

### 6. Authorization coverage

- [ ] Every endpoint that modifies data has an explicit permission check
- [ ] "Fetch other user's data" path checks ownership before returning
- [ ] Admin-only operations use a role/scope check, not just authentication

## Output format

```markdown
## Security Review

### Critical
- [issue] → [fix]

### Medium
- [issue] → [fix]

### Passed
- Token storage ✓
- Cookie config ✓
- CORS ✓
- Password hashing ✓
- Auth coverage ✓
```
