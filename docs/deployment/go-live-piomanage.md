# Going live at piomanage.com

The whole product runs on **one Docker host** from a single compose file:
API, background worker (scheduled sync), web app, Postgres, Keycloak (sign-in),
Vault (PSA credential store) and Caddy, which obtains Let's Encrypt TLS
certificates automatically.

## What the server must be

**Hostinger shared hosting cannot run this** — verified directly over SSH: the
shared server offers PHP only (no .NET, no Node, no Docker). You need one of:

- a **Hostinger VPS** (KVM 1 is enough to start: 1 vCPU / 4 GB) — cleanest, or
- **any machine with Docker + a Cloudflare Tunnel** — including the dev PC.
  No open ports, no public IP, free; the trade-off is the portal is only up
  while that machine is.

## Co-hosting route: a VPS whose nginx already serves other sites

Used for the actual deployment: srv1830041.hstgr.cloud already serves
piodeploy.com and piotask.com from nginx on 80/443, so the stack must not
bring its own edge. The `docker-compose.hostproxy.yml` overlay publishes web
on `127.0.0.1:3100` and Keycloak on `127.0.0.1:8181` (localhost only — the
stack opens nothing public), and nginx proxies to them:

```bash
git clone https://github.com/Dalbeirdev/deskportalpsa.git /opt/deskportal && cd /opt/deskportal
cp infrastructure/docker/.env.prod.example infrastructure/docker/.env.prod   # fill in
docker compose -f infrastructure/docker/docker-compose.prod.yml \
  -f infrastructure/docker/docker-compose.hostproxy.yml \
  --env-file infrastructure/docker/.env.prod up -d --build
cp infrastructure/nginx/piomanage.conf /etc/nginx/sites-available/piomanage
ln -s /etc/nginx/sites-available/piomanage /etc/nginx/sites-enabled/piomanage
nginx -t && systemctl reload nginx
# once both A records resolve to this host:
certbot --nginx -d piomanage.com -d auth.piomanage.com
```

Existing sites are untouched: only new vhost files are added, and certbot's
`--nginx` edits are confined to the new server blocks.

## No-VPS route: this machine + Cloudflare Tunnel

1. Create a free Cloudflare account and add `piomanage.com` as a site; change
   the domain's nameservers (at the registrar) to the pair Cloudflare shows.
2. Zero Trust → Networks → Tunnels → *Create a tunnel* (Cloudflared). Copy the
   **token**, put it in `.env.prod` as `TUNNEL_TOKEN`.
3. In the tunnel's *Public hostnames* tab add two entries:
   | Hostname | Service |
   |---|---|
   | `piomanage.com` | `http://web:3000` |
   | `auth.piomanage.com` | `http://keycloak:8081` |
4. Start the stack with the tunnel profile:
   ```bash
   docker compose -f infrastructure/docker/docker-compose.prod.yml      --env-file infrastructure/docker/.env.prod --profile tunnel up -d --build
   ```
Cloudflare terminates TLS; Caddy is not used on this route (it is the `edge`
profile for hosts with open ports 80/443).

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
