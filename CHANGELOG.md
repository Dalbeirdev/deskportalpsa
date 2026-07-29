# Changelog

All notable changes to the Desk Portal. Format loosely follows Keep a Changelog.

## [0.1.0] — Unreleased (feature-complete, pre-GA)

Multi-tenant PSA ticket portal, delivered across 10 phases. Self-hosted .NET 9 + Next.js.

### Added
- **Foundation**: monorepo, EF Core multi-tenant schema, DbContext global tenant filter + write
  guards, Keycloak OIDC auth, permission-claim RBAC (7 roles), Vault-backed secret store,
  structured logging + correlation IDs, RFC-7807 errors, CI (build/test/gitleaks/dep-scan).
- **Integration framework**: `IServiceManagementConnector` + capability model, connector factory/
  resolver, mock connector + certification suite, 8-scope mapping engine, retry + circuit breaker,
  sync loop-prevention (idempotency + echo suppression), webhook ingress, job retry/backoff/DLQ.
- **Autotask connector** (REST v1.0) + ticket sync engine (upsert with mapping + idempotency).
- **ConnectWise connector** (REST 3.0) + cross-provider normalization parity.
- **Client portal**: ticket list/detail/create/comment (company-scoped, public-notes-only),
  notifications, profile; Next.js UI.
- **Dashboards**: configurable weighted productivity score (with the operational-indicator
  guardrail), metrics, team comparison, trend, CSV export.
- **Administration**: PSA connection management (secrets to Vault), mapping versioning + rollback
  (audited), user/role management, audit log, integration health, job monitor with DLQ reprocess.
- **Security & performance**: SSRF egress guard, adversarial cross-tenant tests, performance
  indexes, k6 load scripts, backup/DR runbook, OWASP-mapped security review.

### Fixed (final QA)
- WCAG AA text contrast; visible keyboard focus ring; mobile navigation; reduced-motion support.

### Pending GA (require a running stack)
- DAST/ZAP, penetration test, k6 load run, DR restore drill, full Keycloak login flow, attachment
  service (upload + malware scanning). See `docs/testing/qa-report.md` (K-1…K-5).

### Verified
- 136/136 automated tests green; web build + production dependency audit clean.
