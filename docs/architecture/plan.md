# Desk Portal — Architecture & Delivery Plan (approved)

This is the Phase-1 requirements/architecture document. Decisions locked with the product owner:
**self-hosted / VPS** deployment (Keycloak + Vault, no Azure), **ASP.NET Core .NET 9** backend,
**greenfield** (independent of the existing PHP osTicket plugins).

## 1. Objective
One professional, multi-tenant portal over many PSA tenants (ConnectWise, Autotask, + future
providers). Each PSA stays the system of record; the portal normalizes, synchronizes, reports.

## 2. Logical architecture
Next.js web → ASP.NET Core API (REST + OpenAPI) → Application services (tenant context, ticket
normalization, mapping engine, sync engine, analytics) → **Connector factory** (per-provider
`IServiceManagementConnector`) + Background worker (webhooks, polling, retries, DLQ, reconcile).
Backing services: PostgreSQL, Redis, RabbitMQ, MinIO, Keycloak, Vault.

## 3. Multi-tenancy
`Platform → MSP Org → PSA Connection (provider + tenant) → Client Company → Client User → Ticket`.
Isolation enforced in five layers: EF Core global query filter (DB), authorization middleware
(API), tenant-prefixed Redis keys (cache), queue tenant stamping (queue), server-side scoping (UI).

## 4. Connector model
Single `IServiceManagementConnector` contract; each provider also declares a `ProviderCapabilities`
matrix so the UI/sync degrade gracefully instead of forcing feature parity. Provider-specific code
lives only behind the interface.

## 5. Field-mapping engine
Rules resolve most-specific-first across 8 scopes: platform → provider → connection → client →
queue/board → ticket-type → custom-field → conditional. Versioned with rollback and audit.

## 6. Sync correctness
Loop prevention via correlation IDs, source markers, idempotency keys, payload/update hashes, and
optimistic versioning. Duplicate deliveries dropped by the unique `(connection, idempotency_key)`.

## 7. Security
Keycloak OIDC (staff + client realms), MFA, brute-force protection; permission-claim RBAC (7 roles);
PSA secrets in Vault only; append-only audit; webhook signature + timestamp validation;
attachment scan + quarantine + signed URLs; strict CI vuln/secret gates.

## 8. Delivery phases
1. Discovery (this doc) · 2. Foundation · 3. Integration framework · 4. Autotask · 5. ConnectWise ·
6. Client portal · 7. Technician/manager dashboards · 8. Administration · 9. Security & performance ·
10. Final QA & production readiness. Connector build order follows the Integration Plan waves
(Wave 1 = ConnectWise + Autotask as reference implementations).

Each phase ends at an approval checkpoint. See [phase-status.md](phase-status.md).

## 9. Risks
Provider feature gaps → capability model + documented limitations · sync loops → correlation/idempotency ·
cross-tenant leakage → 5-layer isolation + dedicated test suite · rate limits → per-connection config +
backoff + circuit breaker + DLQ · secret exposure → Vault-only + masking + secret-scan gate.
