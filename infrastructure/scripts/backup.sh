#!/usr/bin/env bash
# Desk Portal daily backup. Run from a host with the stack reachable and the CLIs installed
# (pg_dump, mc, vault, kc.sh). Intended for cron; encrypt + ship the OUT dir offsite afterwards.
set -euo pipefail

OUT="${OUT:-./backups/$(date +%Y%m%d)}"
mkdir -p "$OUT"

: "${PGHOST:=localhost}" "${PGPORT:=5432}" "${PGDATABASE:=desk_portal}" "${PGUSER:=desk}"

echo "==> PostgreSQL"
pg_dump -Fc -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" "$PGDATABASE" -f "$OUT/desk.dump"

echo "==> MinIO attachments"
# Requires an 'mc alias' configured for both the local and offsite endpoints.
mc mirror --overwrite local/desk-attachments "$OUT/attachments"

echo "==> Vault snapshot (production Vault runs in server/raft mode)"
if command -v vault >/dev/null; then
  vault operator raft snapshot save "$OUT/vault.snap" || \
    echo "   (skipped: dev-mode Vault has no raft snapshot; production must)"
fi

echo "==> Keycloak realm export"
# Adjust to your Keycloak container/exec path.
docker exec desk-portal-keycloak-1 /opt/keycloak/bin/kc.sh export \
  --dir /tmp/kc-export --realm desk >/dev/null 2>&1 || true
docker cp desk-portal-keycloak-1:/tmp/kc-export "$OUT/keycloak" 2>/dev/null || true

echo "==> Done: $OUT"
echo "REMINDER: encrypt and ship '$OUT' offsite; store Vault unseal keys separately."
