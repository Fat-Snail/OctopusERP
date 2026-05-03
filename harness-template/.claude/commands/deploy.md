# /deploy — Docker Compose deployment

Build images and manage the local / production environment for {{PROJECT_NAME}}.

## Usage
- `/deploy` — full cycle: build → start → verify
- `/deploy build` — rebuild images only
- `/deploy up` — start without rebuilding
- `/deploy down` — stop and remove containers (keep volumes)
- `/deploy logs <service>` — tail logs for a service

## Full deployment flow

**Step 1 — Build images**
```bash
docker compose build --no-cache
```

**Step 2 — Start services**
```bash
docker compose up -d
```

**Step 3 — Wait for DB readiness**
```bash
# Poll until healthy (adapt service name and health-check command)
docker compose exec db {{DB_HEALTH_CMD}}
# e.g. mysqladmin ping -h localhost -u root -proot
# e.g. pg_isready -U postgres
```

**Step 4 — Run migrations**
```bash
{{MIGRATION_CMD_IN_CONTAINER}}
# e.g. docker compose exec api dotnet ef database update
# e.g. docker compose exec api flask db upgrade
# e.g. docker compose exec api npx prisma migrate deploy
```

**Step 5 — Smoke test**
```bash
# API health / well-known endpoint
curl -f http://localhost:{{API_PORT}}/health

# Auth discovery (if OAuth server)
curl -f http://localhost:{{API_PORT}}/.well-known/openid-configuration | jq .

docker compose ps
```

## Success criteria
- [ ] All containers status `running`
- [ ] Health endpoint returns 200
- [ ] DB tables exist (migrations applied)

## Teardown
```bash
# Stop, keep data volumes
docker compose down

# Stop and wipe data volumes (destructive — data is lost)
docker compose down -v
```

## Common issues

| Symptom | Cause | Fix |
|---|---|---|
| Service exits immediately | Crash on startup | `docker compose logs <service>` |
| DB connection refused | DB not ready yet | Wait for health check before migrating |
| Port already in use | Local process on same port | `lsof -i :{{API_PORT}}` to find it |
| Migration table not found | Migrations never ran | Run Step 4 |
| Image not updated | Old layer cached | `docker compose build --no-cache` |
