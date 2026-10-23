# Deploy (Hetzner, Docker Compose) — draft

One VPS runs the whole stack with Docker Compose. Written for PR 3 (`feat/infrastructure`, 2026-10-23). Not yet covered
here: CI that builds and pushes the images, backups (U5), monitoring and alerts (U4); see "Still open" below.

## What runs

| Service | Image | Role |
|---|---|---|
| `proxy` | `caddy:2.10-alpine` | The only published ports (80, 443). TLS from Let's Encrypt, HTTP to HTTPS, routes `/api/*`, `/webhooks/stripe*` and `/health` to the API and everything else to the web app. Config: `Caddyfile`. |
| `web` | `luma-web` | The Vite build (`dist/`) served by Caddy on :8080: SPA fallback, hashed `/assets/*` cached for a year, everything else revalidated. |
| `api` | `luma-backend` | ASP.NET Core on :8080. |
| `worker` | `luma-backend` | The email outbox and scheduled campaigns (`CreatorPlatform.Worker.dll`). |
| `migrate` | `luma-backend` | One-shot: `--migrate` applies the pending EF Core migrations and exits. |
| `postgres` | `postgres:16` | Volume `postgres_data`. |
| `redis` | `redis:7.4-alpine` | Password, no persistence, 256 MB, `allkeys-lru` (as in development, see REDIS.md). |

**One backend image, three processes.** The API, the Worker and the migration step share all module code, and must run
the same build: a Worker one version behind the API (or a migration from another commit) is a class of bug one image rules
out. The image holds both published apps (`/app/api`, `/app/worker`); compose picks the entry point. The duplicated DLLs
cost a few MB.

- The image is **chiseled** (`aspnet:10.0-noble-chiseled-extra`): no shell, no package manager, non-root user `app`.
  "extra" carries ICU and tzdata, which the creator time zones need.
- `appsettings.Development.json` and every `.env` file are excluded from the build context (`.dockerignore`), so no
  development secret can end up in an image.

**Networks.** `edge` (proxy, web, api) has a fixed subnet so the proxy has a fixed address; the API trusts
`X-Forwarded-For` only from that address (`ForwardedHeaders:KnownProxies`, `ForwardLimit=1`), so rate limits see the
real client IP. `data` (postgres, redis, api, worker, migrate) is internal: no published port and no internet access.
The Worker reaches the email provider through `outbound`.

## Migrations

`migrate` runs before the API and the Worker (`depends_on: service_completed_successfully`). It is the API binary with
`--migrate`: it builds the API's own service registrations (so the EF model is the full one the API maps), applies the
pending migrations, logs which ones, and exits 0. Nothing pending is a quick 0. Any failure is logged as Critical and exits 1, and
then compose does not start the API or the Worker. `Database:AutoMigrate` stays `false` in production.

On an **empty** database EF Core first logs one `Failed executing DbCommand` error for its own
`__EFMigrationsHistory` lookup (the table does not exist yet), then creates it and applies everything. That line is
expected once, on the first deploy only; alerting (chapter 10) can ignore it from the `migrate` container.

Run it alone, e.g. to check a release before switching:

```bash
docker compose -f docker-compose.prod.yml run --rm migrate
```

Migrations must stay backward compatible with the running version for the length of a deploy (add, backfill, then
remove in a later release), because the old API serves requests until its container is replaced.

## First deploy

1. Server: Docker Engine + the compose plugin, a firewall allowing only 22, 80 and 443, DNS A/AAAA records for the
   domain pointing at the server.
2. Copy the `deploy/` folder to the server.
3. `cp .env.example .env` and `cp luma.env.example luma.env`, fill both in (every secret from `openssl rand -hex 32`),
   `chmod 600 .env luma.env`.
4. Build the images (below) or pull them from the registry, with the tag in `LUMA_TAG`.
5. `docker compose -f docker-compose.prod.yml up -d`, then check `docker compose ... ps` (migrate: exited 0) and
   `curl https://<domain>/health`.

## Build the images

From the backend checkout (the `Dockerfile` at its root):

```bash
docker build --build-arg GIT_SHA=$(git rev-parse --short HEAD) -t luma-backend:$(git rev-parse --short HEAD) .
```

The web app is built from the **frontend** checkout, with the Dockerfile kept here:

```bash
docker build -f deploy/frontend/Dockerfile -t luma-web:<tag> --build-arg VITE_PUBLIC_SITE_URL=https://<domain> --build-arg VITE_SUPPORT_EMAIL=<support address> ../frontend
```

- The `VITE_*` values are baked into the JavaScript bundle and are public; never pass a secret.
- `VITE_API_BASE_URL` stays empty: the SPA calls `/api` on its own origin.
- `deploy/frontend/Dockerfile.dockerignore` keeps `node_modules`, `dist` and the local `.env*` files (developer logins)
  out of the context.
- For a Hetzner ARM server (CAX), build for it: `docker buildx build --platform linux/arm64 ...`. Both Dockerfiles
  cross-compile on the build machine's own architecture.

## Update

```bash
LUMA_TAG=<new tag> docker compose -f docker-compose.prod.yml up -d
```

The migration runs first. The Worker gets 45 s to finish its batch on stop, the API 30 s.

## Settings

Compose sets the environment, the database and Redis connections, the trusted proxy, `Database:AutoMigrate=false` and JSON
console logs. Everything else is in `luma.env` (template: `luma.env.example`, the U3 list from PRE-DEPLOY-AUDIT.md).

- The Worker's tracked `appsettings.json` points `Email:FrontendBaseUrl` at `https://localhost:3000`. `luma.env` must
  override it, or links in sent emails go to localhost.
- The Marketing secrets must be the same for the API and the Worker. One `luma.env` for both makes sure of it.
- Passwords that go into connection strings: hex only. A `;` or `,` would break the connection string.

## Still open (later chapters)

- CI: build both images on `main`, push to a registry (GHCR), deploy by tag.
- Backups (U5): a daily `pg_dump` to object storage, plus a restore drill.
- Observability (U4): ship the JSON logs, alert on errors, a Worker heartbeat.
- The proxy config is a draft: `www` redirect, request size limits and the final security headers come with PR 1 (S8) and the domain.
