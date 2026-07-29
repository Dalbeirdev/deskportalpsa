# Security Review (Phase 9)

Independent review of the implemented platform against the OWASP Top 10 and the spec's security
requirements. "Verified" = covered by automated tests or a runnable scan; "Pending (live)" = needs a
running stack (DAST/pen-test/load) not available in the current environment.

## Dependency & secret posture (runnable now)
- **.NET**: `dotnet list package --vulnerable --include-transitive` → **0 vulnerable packages**.
  Build gate: `TreatWarningsAsErrors` + NuGetAudit (already caught & remediated an OTLP-exporter CVE).
- **Web (production bundle)**: `npm audit --omit=dev` → **0 vulnerabilities**. sharp/postcss pinned via
  overrides. Residual 9 highs are **dev-only** (ESLint `brace-expansion` DoS in a glob matcher) — not
  bundled, not a production surface. Remediation: ESLint 10 major upgrade (deferred, low risk).
- **Secrets**: gitleaks in CI; repo scan for key patterns → clean. Dev fixtures allow-listed.

## OWASP Top 10 mapping

| # | Risk | Status | Notes |
|---|---|---|---|
| A01 | Broken access control | ✅ Verified | Permission-claim authz on every endpoint; portal detail returns `null` (not 403) cross-tenant/company to avoid existence leaks; 10 tenant/company-isolation tests. |
| A02 | Cryptographic failures | ✅ / Pending | HTTPS+HSTS; PSA secrets in Vault only. Field-level encryption for extra-sensitive columns: **deferred**. |
| A03 | Injection | ✅ Verified | EF Core parameterizes all queries; **no raw SQL / string-built queries** in the codebase. React auto-escapes; no `dangerouslySetInnerHTML`. |
| A04 | Insecure design | ✅ | Connector capability model, sync loop-prevention, append-only audit, fail-closed tenant default. |
| A05 | Security misconfiguration | ✅ | CSP, X-Frame-Options DENY, nosniff, Referrer-Policy, HSTS; CORS allowlist; 25 MB request cap; prod refuses the in-memory secret store. |
| A06 | Vulnerable components | ✅ Verified | See dependency posture above. |
| A07 | Auth failures | ✅ / Pending | Keycloak OIDC; web login is auth-code + **PKCE (S256)** with tokens in **httpOnly cookies** (BFF — never in client JS) and refresh-on-401; short-lived tokens; brute-force + lockout in realm. Full round-trip test: **pending (needs a running Keycloak)**. |
| A08 | Integrity failures | ✅ Verified | Webhook signature + timestamp (replay) validation; idempotency + update-hash echo suppression. |
| A09 | Logging/monitoring | ✅ | Structured Serilog + correlation IDs; **immutable audit log** (modify/delete throws); admin actions audited. |
| A10 | SSRF | ✅ Verified | Connector base URLs are admin-configured. An opt-in `EgressGuard` blocks connector calls to loopback/private/link-local/reserved hosts (incl. the 169.254.169.254 metadata endpoint); the IP classifier is unit-tested. Enable via `Connectors:BlockPrivateEgress` with an optional host allowlist for self-hosted PSA. |

## Multi-tenant isolation (defense in depth) — Verified
1. DB global query filter on every `ITenantScoped` entity + write guards (cross-tenant insert/modify throws).
2. `AuditLog` and `AppUser` are **not** globally filtered by design — their services scope explicitly;
   dedicated adversarial tests confirm no leak (both tenants in one shared store).
3. Fail-closed: an unresolved scope matches `Guid.Empty` → zero rows.
4. Platform super-admins operate under an explicit, opt-in platform scope only.

## Findings & recommendations
| Severity | Finding | Recommendation |
|---|---|---|
| ~~Medium~~ Resolved | SSRF surface via admin-configured connection URLs. | **Implemented**: opt-in `EgressGuard` blocks private/reserved egress (tested). Enable in production. |
| ~~Low~~ Resolved | Attachment malware scanning / quarantine / signed URLs. | **Implemented**: extension/MIME/size validation, EICAR/PE scan, quarantine (bytes never stored), randomized keys, HMAC time-limited signed URLs, audited downloads (7 tests). Production binds ClamAV + MinIO. |
| Low | Field-level encryption for PII columns not implemented. | Add column encryption for requester PII if required by the data-classification policy. |
| Info | Dev-only ESLint advisory (`brace-expansion`). | Upgrade to ESLint 10 at a convenient major-version bump. |

## Pending a live environment (production-readiness gates)
- DAST (OWASP ZAP) against the running API.
- Authenticated authorization fuzzing across roles.
- Penetration test.
- Load/performance test to the §13 targets (see `tests/load/k6-smoke.js`).
- Backup restore drill (see `docs/deployment/backup-and-recovery.md`).

**Verdict:** No critical or high findings in code. One medium (SSRF hardening) and low items are
tracked with remediations. Production sign-off remains contingent on the live-environment gates above.
