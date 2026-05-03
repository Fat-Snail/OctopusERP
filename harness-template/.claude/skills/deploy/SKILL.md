# Deploy Review Skill

Pre-flight checks before deploying {{PROJECT_NAME}}.

## Trigger
- Before running `/deploy`
- After modifying `Dockerfile` or `docker-compose.yml`
- After adding or changing environment variables

## Checks

### 1. Dockerfile

```bash
find . -name "Dockerfile" -not -path "*/node_modules/*" -not -path "*/.git/*"
```

For each Dockerfile verify:
- [ ] Multi-stage build (separate build stage and runtime stage)
- [ ] Base image version pinned (no `latest`)
- [ ] App listens on `EXPOSE 8080`
- [ ] Runs as non-root user

### 2. docker-compose.yml

```bash
docker compose config   # validates syntax and resolves variables
```

Verify:
- [ ] DB service uses the correct character set / collation
- [ ] `depends_on` with health-check conditions (not just service names)
- [ ] Named volumes defined to persist DB data across restarts
- [ ] Port mappings correct: `host:container` — e.g. `5001:8080`

### 3. Environment variables

Required variables (never hardcode these):
- `DATABASE_URL` / `ConnectionStrings__Default`
- Any signing secrets, API keys, OAuth client secrets

Check that `.env.example` lists every required variable.

### 4. Port conflicts

```bash
lsof -i :{{API_PORT}}
lsof -i :{{WEB_PORT}}
lsof -i :{{DB_PORT}}
```

### 5. Post-start smoke test

```bash
docker compose ps

curl -f http://localhost:{{API_PORT}}/health
# or: curl -f http://localhost:{{API_PORT}}/.well-known/openid-configuration | jq .
```

## Common issues

| Symptom | Cause | Fix |
|---|---|---|
| Container exits immediately | App crash on boot | `docker compose logs <service>` |
| DB connection refused | DB not ready | Wait for health check |
| Port conflict | Local process on same port | Kill it or change mapping |
| Migrations not applied | Step skipped | Re-run migration command |
