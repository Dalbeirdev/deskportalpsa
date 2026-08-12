# Going live at piomanage.com

The whole product runs on **one Docker host** from a single compose file:
API, background worker (scheduled sync), web app, Postgres, Keycloak (sign-in),
Vault (PSA credential store) and Caddy, which obtains Let's Encrypt TLS
certificates automatically.

## What the server must be

**Hostinger shared hosting cannot run this** — the "All set to go" page you have
today is shared hosting, which serves PHP/static files only. You need one of:

- a **Hostinger VPS** (KVM 1 is enough to start: 1 vCPU / 4 GB), or
- any Linux box with Docker that is reachable from the internet on ports 80/443
  (your own machine works if you forward those ports).

## One-time setup

**1. DNS — two A records at your registrar, both pointing at the server's IP:**

| Record | Host | Value |
|---|---|---|
| A | `piomanage.com` | `<server IP>` |
| A | `auth.piomanage.com` | `<server IP>` |

Caddy will not be able to obtain certificates until these resolve.

**2. On the server:**

```bash
git clone https://github.com/Dalbeirdev/deskportalpsa.git && cd deskportalpsa
cp infrastructure/docker/.env.prod.example infrastructure/docker/.env.prod
nano infrastructure/docker/.env.prod       # fill in every change-me value
docker compose -f infrastructure/docker/docker-compose.prod.yml \
  --env-file infrastructure/docker/.env.prod up -d --build
```

First start takes a few minutes: images build, migrations run, the Keycloak
realm imports, and Caddy fetches certificates.

**3. Create your sign-in user in Keycloak** (the portal account was already
bootstrapped from `BOOTSTRAP_ADMIN_EMAIL`; this creates the credential side):

- Open `https://auth.piomanage.com` → Administration Console → log in with
  `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD`.
- Switch to the **desk** realm → Users → *Add user*. Email must EQUAL
  `BOOTSTRAP_ADMIN_EMAIL`, and tick *Email verified*.
- Credentials tab → set a password (untick *Temporary*).

**4. Sign in:** open `https://piomanage.com` → you are redirected to Keycloak →
log in → your portal account binds to that login on this first sign-in
(the same email-binding invited technicians use).

## After you are in

Everything else happens in the product: add the PSA connections under
*PSA Connections* (credentials go to Vault, never the database), set each
connection's board/queue defaults and time-entry technician, map statuses and
priorities under *Field Mapping*, and add your team under *Users* — they bind
the same way on their first login.

The worker syncs every `SYNC_POLL_MINUTES` (default 5) without anyone pressing
a button.

## Updating

```bash
git pull
docker compose -f infrastructure/docker/docker-compose.prod.yml \
  --env-file infrastructure/docker/.env.prod up -d --build
```

Postgres data, attachments, and certificates live in named volumes and survive
rebuilds. `infrastructure/scripts/backup.sh` covers the database.

## Testing-tier caveats, stated plainly

- **Vault runs in dev mode.** Secrets are real PSA credentials; the unseal
  story is not production-grade. Harden (server-mode Vault) before this holds
  customer data at scale.
- **Attachments are on a local volume**, not object storage. Fine for one
  host; move to S3/MinIO behind `IObjectStorage` before scaling out.
- **Do not reuse the ConnectWise key that was shared in chat** — create fresh
  API members for the live deployment.
