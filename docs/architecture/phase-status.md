# Phase & Provider Status

## Delivery phases

| Phase | Name | Status |
|---|---|---|
| 1 | Discovery / architecture | ✅ Complete (approved) |
| 2 | Foundation | ✅ Complete |
| 3 | PSA integration framework | ✅ Complete |
| 4 | Autotask integration | ✅ Complete |
| 5 | ConnectWise integration | ✅ Complete |
| 6 | Client portal | ✅ Complete |
| 7 | Technician & manager dashboards | ✅ Complete |
| 8 | Administration | ⏭️ Next |
| 9 | Security & performance | ⬜ Planned |
| 10 | Final QA & production readiness | ⬜ Planned |

## Phase 2 — Foundation acceptance

| Criterion | Status | Evidence |
|---|---|---|
| Monorepo + Docker Compose (pg/redis/rabbitmq/minio/keycloak/vault) | ✅ authored | `infrastructure/docker/docker-compose.yml` |
| EF Core schema + initial migration | ✅ | `packages/infrastructure/Persistence/Migrations/*InitialSchema*` |
| Tenant context + global query filter | ✅ | `DeskDbContext`, 6 isolation tests pass |
| Keycloak OIDC auth on API | ✅ | `Program.cs` JwtBearer + realm import |
| Permission-claim authorization (7 roles) | ✅ | `PermissionPolicyProvider`, RBAC tests pass |
| Vault-backed secret provider abstraction | ✅ | `ISecretStore` + `VaultSecretStore` |
| Serilog structured logging + correlation id + RFC-7807 | ✅ | middleware trio |
| CI (build, test, secret scan, dep scan) | ✅ authored | `.github/workflows/ci.yml` |
| Health + Swagger | ✅ | `/health`, `/health/ready`, Swagger (dev) |

**Verified locally:** `dotnet build` clean (warnings-as-errors), 15/15 unit tests green, migration generated.
**Requires Docker Desktop (not on this machine):** `docker compose up`, `dotnet ef database update`,
live Keycloak/Vault, and `npm run build` for the web app.

## Phase 3 — Integration framework acceptance

| Criterion | Status | Evidence |
|---|---|---|
| Connector interface + capability model | ✅ | `IServiceManagementConnector`, `ProviderCapabilities` |
| Connector factory + resolver | ✅ | `IConnectorFactory`, `ConnectorResolver` |
| Mock PSA connector works | ✅ | `MockConnector` — full contract + fault injection |
| Contract / certification suite passes | ✅ | `ConnectorCertificationTests` (18 tests) |
| Field-mapping engine (8-scope resolution) | ✅ | `MappingEngine`, `MappingEngineTests` (10 tests) |
| Retry / backoff / circuit breaker | ✅ | `ResilientExecutor`, `CircuitBreaker`, `ResilienceTests` (9) |
| Failed-job handling (retry + dead-letter) | ✅ | `JobProcessor`, `JobProcessorTests` (3) |
| Webhook validation framework (sig + timestamp) | ✅ | `WebhooksController`, webhook tests |
| Sync loop-prevention (idempotency + hash + echo) | ✅ | `SyncEventStore`, `UpdateHasher`, `SyncTests` (4) |
| Polling / reconciliation framework | ✅ | `PollingSyncService` (skeleton) |

**Verified locally:** Release build clean, **55/55 unit tests green** (15 Phase 2 + 40 Phase 3).

## Phase 6 — Client portal acceptance

| Criterion | Status | Evidence |
|---|---|---|
| Ticket list / detail / create / comment | ✅ | `TicketsController`, `TicketReadService`, `TicketCommandService` |
| Notifications + profile | ✅ | `PortalController` |
| **No internal notes exposed** | ✅ | read service filters `IsPublic`; internal notes never persisted; test |
| **Client-company scoping / tenant isolation** | ✅ | `Visible()` filter; cross-company test returns nothing |
| PSA-first writes with echo suppression | ✅ | create/comment record portal-origin sync events |
| Frontend (list/detail/create/notifications/profile) | ✅ | Next.js pages, React Query, Zod validation |
| Responsive + dark mode + no console errors | ✅ | verified mobile 375px + dark in browser |

**Verified locally:** .NET Release clean, **98/98 unit tests green**; web typecheck + build clean;
client portal pages render with graceful empty states, form validation works, 0 console errors.

## Phase 7 — Dashboards acceptance

| Criterion | Status | Evidence |
|---|---|---|
| Configurable weighted productivity score | ✅ | `ProductivityScorer` (renormalizes over measured components), 6 tests |
| Score calculations pass | ✅ | known-value + clamp + configurable-weights tests |
| Metrics + time calculations correct | ✅ | `TechnicianMetricsService`, counts/SLA/avg-resolution tests |
| Filters work | ✅ | date/technician/company/priority filter tests |
| Team comparison + trend | ✅ | grouped, ranked; per-day trend |
| CSV export accurate | ✅ | `DashboardController` export with escaping + disclaimer header |
| "Operational indicator" guardrail surfaced | ✅ | disclaimer on every API response + shown in UI |

**Verified locally:** .NET Release clean, **110/110 unit tests green**; web build clean; productivity
page renders with the disclaimer, score card, tiles, trend sparkline, team table — 0 console errors.

## Provider readiness matrix

Statuses: Planned · API Research · Foundation · In Progress · Integration Testing · QA · Limited · **Production Ready** · Blocked · Unsupported.

| Wave | Provider | Status |
|---|---|---|
| 1 | ConnectWise PSA | **Ready for Integration Testing** (connector complete; certified vs fake server; needs a live instance) |
| 1 | Datto Autotask PSA | **Ready for Integration Testing** (connector + sync engine complete; certified vs fake server; needs a live sandbox) |
| 2 | HaloPSA | Planned |
| 2 | Syncro | Planned |
| 2 | SuperOps | Planned |
| 3 | Atera / Kaseya BMS / N-able MSP Manager / DeskDay | Planned |
| 4 | ServiceNow / Freshservice / Jira Service Management | Planned |
| 5 | ManageEngine SDP / Zendesk / Zoho Desk | Planned |
