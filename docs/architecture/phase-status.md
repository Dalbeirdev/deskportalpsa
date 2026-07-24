# Phase & Provider Status

## Delivery phases

| Phase | Name | Status |
|---|---|---|
| 1 | Discovery / architecture | ✅ Complete (approved) |
| 2 | Foundation | ✅ Complete |
| 3 | PSA integration framework | ✅ Complete |
| 4 | Autotask integration | ⏭️ Next |
| 5 | ConnectWise integration | ⬜ Planned |
| 6 | Client portal | ⬜ Planned |
| 7 | Technician & manager dashboards | ⬜ Planned |
| 8 | Administration | ⬜ Planned |
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

## Provider readiness matrix

Statuses: Planned · API Research · Foundation · In Progress · Integration Testing · QA · Limited · **Production Ready** · Blocked · Unsupported.

| Wave | Provider | Status |
|---|---|---|
| 1 | ConnectWise PSA | Planned |
| 1 | Datto Autotask PSA | Planned |
| 2 | HaloPSA | Planned |
| 2 | Syncro | Planned |
| 2 | SuperOps | Planned |
| 3 | Atera / Kaseya BMS / N-able MSP Manager / DeskDay | Planned |
| 4 | ServiceNow / Freshservice / Jira Service Management | Planned |
| 5 | ManageEngine SDP / Zendesk / Zoho Desk | Planned |
