# Backup & Disaster Recovery Runbook

Covers the self-hosted stack: PostgreSQL, MinIO (attachments), Vault (PSA credentials), and
Keycloak (identity). **Vault and Postgres are the crown jewels** — losing either is unrecoverable
without a backup.

## What to back up

| Component | Contains | Backup method | RPO target |
|---|---|---|---|
| PostgreSQL | All portal data (tenants, tickets, mappings, audit) | `pg_dump` (logical) + WAL archiving (PITR) | ≤ 5 min (with WAL) |
| Vault | PSA credentials (the only copy) | Vault snapshot (`vault operator raft snapshot`) or KV export | ≤ 1 h |
| MinIO | Attachment bytes | `mc mirror` to offsite bucket | ≤ 1 h |
| Keycloak | Realm, clients, users config | Realm export (`kc.sh export`) | on change |

Encrypt every backup at rest and in transit; store offsite; restrict access. **Never** store the
Vault unseal keys in the same location as the Vault snapshot.

## Daily backup (scripted)

```bash
infrastructure/scripts/backup.sh          # see script below
```

- Postgres → `desk-YYYYMMDD.dump` (custom format, `pg_dump -Fc`)
- MinIO → mirrored to the offsite bucket
- Vault → raft snapshot (production Vault runs in server mode, not dev)
- Keycloak realm → `desk-realm-YYYYMMDD.json`

## Restore procedure (drill quarterly)

1. **Provision** a clean stack: `docker compose -f infrastructure/docker/docker-compose.yml up -d`.
2. **Postgres**: `pg_restore --clean --if-exists -d desk_portal desk-YYYYMMDD.dump`.
   Then apply any newer migrations: `dotnet ef database update`.
3. **Vault**: restore the snapshot, then unseal with the sealed key shares. Verify a PSA credential
   reads back: the connection's `CredentialSecretRef` must resolve.
4. **MinIO**: `mc mirror offsite/desk-attachments local/desk-attachments`.
5. **Keycloak**: import the realm export (`--import-realm`).
6. **Verify** (acceptance):
   - API `/health/ready` is green.
   - A known ticket loads with its public conversation.
   - A PSA connection test succeeds (credentials resolved from restored Vault).
   - The audit log shows history (append-only chain intact).
   - Tenant isolation spot-check: a client sees only their company's tickets.

## Disaster-recovery scenarios

| Scenario | Action |
|---|---|
| Postgres corruption | Restore latest dump + replay WAL to just before the incident (PITR). |
| Vault loss | Restore snapshot; if unseal keys are also lost, **all PSA credentials must be re-entered** by MSP admins (documented as the worst case — hence offsite key custody). |
| Full region loss | Rebuild stack from IaC in the DR region; restore all four components from offsite backups. |
| Ransomware | Restore from the most recent *immutable* offsite backup predating the compromise. |

## Targets
- **RTO**: 4 hours (full stack restore + verification).
- **RPO**: 5 minutes for Postgres (WAL), 1 hour for object/secret stores.
- Restore drills are run quarterly and the result recorded in this doc's changelog.

> Status: procedure documented and scripted. A live restore drill requires a running stack and is
> tracked as a production-readiness gate (not executable in the current stack-less environment).
