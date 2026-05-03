# /migrate — Database migration management

Manage schema migrations for {{PROJECT_NAME}} using {{ORM_TOOL}}.

## Usage
- `/migrate add <Name>` — create a new migration
- `/migrate run` — apply all pending migrations
- `/migrate status` — list applied vs pending
- `/migrate rollback <Name>` — revert to a named migration
- `/migrate remove` — delete the latest unapplied migration

## Commands

### Create migration
```bash
{{MIGRATE_CREATE_CMD}}
# e.g. dotnet ef migrations add <Name> --project ../MyApp.Infrastructure
# e.g. alembic revision --autogenerate -m "<Name>"
# e.g. npx prisma migrate dev --name <Name>
```

Migration naming convention:
- `InitialCreate` — first schema setup
- `AddUserEmailIndex` — add an index
- `AddRefreshTokenTable` — new table
- `RenameOrderStatusColumn` — rename a column

### Apply migrations
```bash
{{MIGRATE_RUN_CMD}}
# e.g. dotnet ef database update --project ../MyApp.Infrastructure
# e.g. alembic upgrade head
# e.g. npx prisma migrate deploy
```

### Check status
```bash
{{MIGRATE_STATUS_CMD}}
```

### Rollback
```bash
{{MIGRATE_ROLLBACK_CMD}} <Name>
# Reverts all migrations after the named one.
```

### Remove latest (unapplied only)
```bash
{{MIGRATE_REMOVE_CMD}}
```

## Rules
- **Never auto-migrate in production** — always run manually after a backup.
- Migration files must be committed to version control.
- To fix a mistake in an already-applied migration, create a new corrective migration.
- If the ORM CLI cannot find the DbContext / model, you may need a
  design-time factory class — add one to the data-access project.
