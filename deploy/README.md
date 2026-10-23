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

## Maintenance (the Worker's cleanup)

The Worker runs the cleanup every hour (first one minute after it starts). With several Worker replicas, a Postgres
advisory lock lets one of them work per run. Each job only touches rows past its age, works in batches of 1,000, and
logs its count at Information; a job that fails logs an Error and is retried on the next run while the others still run.

| Job | What it does | Setting (`Maintenance` section) |
|---|---|---|
| `sent-email-bodies` | Drops the HTML and text bodies of mail sent more than 30 days ago; the row (status, dates, recipient) stays. Failed mail keeps its body (it can be resent). | `SentEmailBodyRetentionDays` = 30 |
| `expired-auth-records` | Deletes sessions and one-time tokens 30 days after they expired; sessions also 30 days after they were revoked. | `ExpiredAuthRecordRetentionDays` = 30 |
| `abandoned-uploads` | Deletes uploads never confirmed within 60 minutes, with their blobs (public images by URL, product documents in the private container by path). A blob that cannot be deleted keeps its row and is retried; the run then logs an Error. Needs the `Media__*` settings, which the Worker reads from the shared `luma.env`. | `AbandonedUploadMinutes` = 60, at least `Media:SasExpiryMinutes` + 15 |
| `stale-pending-orders` | Marks checkouts still Pending 25 hours after they started as Failed (their Stripe session expired after 24 hours; the extra hour covers an order created just before its session). A payment that still arrives later is booked normally: the webhook pays a Failed order. An order paid while the job runs stays Paid. | `StalePendingOrderHours` = 25, at least 24 |

Also: `Enabled` (true), `IntervalMinutes` (60), `StartDelaySeconds` (60), `BatchSize` (1000). Invalid values stop the
Worker at start. Override any of them in `luma.env`, e.g. `Maintenance__IntervalMinutes=30`.

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

**Stripe (live mode).** Two webhook endpoints, each with its own signing secret in `luma.env`:

- Platform: `https://<domain>/webhooks/stripe` (`Stripe__WebhookSecret`). Besides the events it already receives, it
  must subscribe to **`checkout.session.expired`**: an abandoned checkout then turns its Pending order into Failed (PR 2).
- Connect: `https://<domain>/webhooks/stripe/connect` (`Stripe__ConnectWebhookSecret`).
- **Refunds made by hand in the Stripe Dashboard** for a Stripe Connect order must tick **"Reverse transfer"** and
  **"Refund application fee"**. Without them the platform pays the refund while the creator keeps the money, and the
  platform keeps a fee for a sale that no longer exists.

## Still open (later chapters)

- CI: build both images on `main`, push to a registry (GHCR), deploy by tag.
- Backups (U5): a daily `pg_dump` to object storage, plus a restore drill.
- Observability (U4): ship the JSON logs, alert on errors, a Worker heartbeat.
- The proxy config is a draft: `www` redirect, request size limits and the final security headers come with PR 1 (S8) and the domain.
