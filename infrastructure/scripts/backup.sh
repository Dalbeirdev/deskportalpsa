#!/usr/bin/env bash
# Desk Portal daily backup. Run from a host with the stack reachable and the CLIs installed
# (pg_dump, mc, kc.sh). Intended for cron; encrypt + ship the OUT dir offsite afterwards.
#
# PSA credentials are encrypted rows in the Postgres dump below (secret_blobs table), not a
# separate store — but SECRET_ENCRYPTION_KEY (in .env.prod) is what makes them readable, and it is
# deliberately NOT captured here. Back it up once, by hand, to wherever this host's other root
# secrets live — never into the same directory as the database dump it decrypts.
set -euo pipefail

OUT="${OUT:-./backups/$(date +%Y%m%d)}"
mkdir -p "$OUT"

: "${PGHOST:=localhost}" "${PGPORT:=5432}" "${PGDATABASE:=desk_portal}" "${PGUSER:=desk}"

echo "==> PostgreSQL (includes encrypted PSA credentials)"
pg_dump -Fc -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" "$PGDATABASE" -f "$OUT/desk.dump"

echo "==> MinIO attachments"
# Requires an 'mc alias' configured for both the local and offsite endpoints.
mc mirror --overwrite local/desk-attachments "$OUT/attachments"

echo "==> Keycloak realm export"
# Adjust to your Keycloak container/exec path.
docker exec desk-portal-keycloak-1 /opt/keycloak/bin/kc.sh export \
  --dir /tmp/kc-export --realm desk >/dev/null 2>&1 || true
docker cp desk-portal-keycloak-1:/tmp/kc-export "$OUT/keycloak" 2>/dev/null || true

echo "==> Done: $OUT"
echo "REMINDER: encrypt and ship '$OUT' offsite. SECRET_ENCRYPTION_KEY is NOT in this backup —"
echo "          store it separately or this dump's PSA credentials are unrecoverable."
