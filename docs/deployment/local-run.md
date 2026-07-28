# Running the full stack locally

End-to-end bring-up of the Desk Portal — backing services, API, worker, web, and Keycloak — plus how
to run the live validations (load test, backup). Prerequisites: Docker, .NET 9 SDK, Node 22.

Ports: API `5080` · web `3000` · Keycloak `8081` · Postgres `5432` · Vault `8200` · MinIO `9000/9001`
· RabbitMQ `5672/15672`.

## 1. Backing services

```bash
docker compose -f infrastructure/docker/docker-compose.yml up -d
```

This starts Postgres, Redis, RabbitMQ, MinIO, **Vault** (dev mode — KV v2 mounted at `secret/`, root
token `desk-root-token`), and **Keycloak** with the `desk` realm auto-imported (clients `desk-api`
bearer-only and `desk-web` public+PKCE, redirect `http://localhost:3000/*`).

## 2. API (auto-migrates + seeds roles)

```bash
dotnet run --project apps/api
```

In Development the API applies EF migrations and seeds the seven built-in roles on startup, then
serves on `http://localhost:5080` (`/health/ready`, `/swagger`). The dev `appsettings.Development.json`
already points at local Postgres, Keycloak, and Vault.

## 3. Worker + web

```bash
dotnet run --project apps/worker
```
```bash
cd apps/web && cp .env.example .env.local && npm install && npm run dev   # http://localhost:3000
```

The web `.env.local` (server-side only) sets `APP_URL`, `DESK_API_BASE`, `KEYCLOAK_ISSUER`,
`KEYCLOAK_CLIENT_ID`. Tokens are held in httpOnly cookies via the BFF proxy — nothing in the browser.

## 4. Create a login + link it to a user

1. **Keycloak user**: Keycloak admin (`http://localhost:8081`, admin/admin) → realm `desk` → Users →
   add a user with a password; copy its **User ID** (the `sub`).
2. **Link it** so the API can resolve permissions. `DeskClaimsTransformation` matches the token `sub`
   to an `app_users` row, so create a platform super-admin for that subject:

   ```sql
   -- psql -h localhost -U desk -d desk_portal
   INSERT INTO app_users (id, "MspOrganizationId", "Email", "DisplayName", "IdpSubject", "IsActive", "CreatedAt", "UpdatedAt")
   VALUES (gen_random_uuid(), NULL, 'you@example.com', 'You', '<KEYCLOAK_SUB>', true, now(), now());

   INSERT INTO user_roles (id, "AppUserId", "RoleId", "CreatedAt", "UpdatedAt")
   SELECT gen_random_uuid(), u.id, r.id, now(), now()
   FROM app_users u, roles r
   WHERE u."IdpSubject" = '<KEYCLOAK_SUB>' AND r."IsSystemRole" AND r."BuiltInType" = 1; -- PlatformSuperAdministrator
   ```

   (Client-portal users go in `client_users` instead, tied to a `client_companies` row.)
3. Visit `http://localhost:3000` → **Continue with SSO** → Keycloak login → back to the dashboard,
   now authenticated (the header shows your name + Sign out).

## 5. Connect a PSA

Dashboard → **PSA Connections** → Add connection. Credentials are written to Vault (never the DB).
For Autotask/ConnectWise, use a real sandbox; the connectors are certified against fakes and ready for
a live integration pass.

## 6. Live validations (production-readiness gates)

```bash
# Load test to the §13 latency targets (grab a token from the web session or a direct grant)
k6 run -e BASE=http://localhost:5080 -e TOKEN=<jwt> tests/load/k6-smoke.js

# Backup (Postgres/MinIO/Vault/Keycloak) — see the runbook for restore + DR drill
bash infrastructure/scripts/backup.sh
```

DAST/ZAP and a penetration test run against `http://localhost:5080` once the stack is up. See
[backup-and-recovery.md](backup-and-recovery.md) and [release-checklist.md](release-checklist.md).

## Production notes
- Run migrations as an explicit deploy step (or set `RunMigrationsOnStartup=true`).
- Vault runs in server (not dev) mode with a real unseal workflow; set `Vault:Address`/`Vault:Token`.
- Turn on the SSRF guard: `Connectors:BlockPrivateEgress=true` (+ allowlist for self-hosted PSA).
- Set `KEYCLOAK_CLIENT_SECRET` only if you switch `desk-web` to a confidential client.
