# Desk Portal

A production-grade, multi-tenant SaaS ticket portal that unifies multiple PSA / service-desk
platforms (ConnectWise Manage, Datto Autotask, and — by wave — HaloPSA, Syncro, ServiceNow,
Freshservice, Zendesk and more) behind one professional experience for clients, technicians,
managers, and administrators. **Each PSA remains the system of record**; the portal normalizes,
synchronizes, and reports on top of it via a connector-first architecture.

> Status: **Phase 2 (Foundation)** complete. See [docs/architecture/plan.md](docs/architecture/plan.md)
> for the full 10-phase plan and [docs/architecture/phase-status.md](docs/architecture/phase-status.md).

## Stack (self-hosted)

| Concern | Choice |
|---|---|
| Frontend | Next.js 15 (App Router) · React 19 · TypeScript · Tailwind · React Query · Zod |
| Backend | ASP.NET Core **.NET 9** Web API + Worker · EF Core · MediatR (later phases) |
| Data | PostgreSQL · Redis · RabbitMQ · MinIO (S3-compatible) |
| Identity | Keycloak (OIDC / OAuth2, MFA, brute-force protection) |
| Secrets | AES-256-GCM, encrypted at rest in Postgres (PSA credentials — plaintext never in DB or code) |
| Observability | Serilog structured logs + correlation IDs (OTLP export in the observability phase) |
| Delivery | Docker Compose · GitHub Actions CI |

## Repository layout

```
apps/
  api/        ASP.NET Core Web API (auth, tenant middleware, health, controllers)
  worker/     Background job processor (sync / polling / reconciliation — grows by phase)
  web/        Next.js frontend (login + dashboard shell)
packages/
  domain/         Entities + enums + permission catalogue (no dependencies)
  application/    Abstractions: tenant context, current user, secret store
  infrastructure/ EF Core DbContext (tenant filter), encrypted secret store, migrations
  psa-core/       IServiceManagementConnector + ProviderCapabilities + unified models
tests/unit/       xUnit tests (tenant isolation, RBAC, audit immutability)
infrastructure/   docker-compose, Keycloak realm, Terraform (VPS)
docs/             architecture, security, setup, integration guides
```

> Full end-to-end bring-up (services + API + worker + web + Keycloak login + live validations):
> [docs/deployment/local-run.md](docs/deployment/local-run.md).

## Quick start (local)

```bash
# 1. Start backing services
docker compose -f infrastructure/docker/docker-compose.yml up -d

# 2. Apply the database schema
export DESK_DB_CONNECTION="Host=localhost;Port=5432;Database=desk_portal;Username=desk;Password=desk"
dotnet ef database update \
  --project packages/infrastructure/Desk.Infrastructure.csproj \
  --startup-project packages/infrastructure/Desk.Infrastructure.csproj

# 3. Run the API and worker
dotnet run --project apps/api
dotnet run --project apps/worker

# 4. Run the web app
cd apps/web && npm install && npm run dev
```

- API health: `GET http://localhost:5080/health`
- Swagger (dev): `http://localhost:5080/swagger`
- Keycloak admin: `http://localhost:8081` (admin/admin) · realm `desk`

## Tests

```bash
dotnet test tests/unit/Desk.Tests.Unit.csproj
```

## Security posture (foundation)

- **Tenant isolation** enforced in the DbContext for every query and write — see
  [TenantIsolationTests](tests/unit/TenantIsolationTests.cs).
- **PSA secrets** are encrypted at rest (AES-256-GCM, key outside the database); connection rows store only an opaque reference, masked in UI/logs.
- **Permission-claim authorization** (not role-name checks).
- **Append-only audit log**, correlation IDs, RFC-7807 error responses.
- CI fails on any known package vulnerability (`TreatWarningsAsErrors` + NuGet audit) and on
  committed secrets (gitleaks).

See [docs/security/security.md](docs/security/security.md).
