# DataBro — Deployment

Deployment strategy, environments, and infrastructure. Optimized for a solo operator on DigitalOcean,
designed to scale on the read path.

## 1. Environments

| Environment | Purpose | Notes |
|---|---|---|
| Local | Development | Docker Compose (API, PostgreSQL, Redis, MinIO as Spaces stand-in) |
| Staging | Pre-production verification | Mirrors production config; seeded sample content |
| Production | Live | DigitalOcean |

Config precedence: `appsettings.json` → `appsettings.{Environment}.json` → environment variables
(highest). Secrets come only from environment/secret store.

## 2. Topology (Phase 1)

```
                 ┌── CDN (static assets, ISR pages) ──┐
Internet ── Nginx (TLS, routing) ──┬── site (Nuxt SSG/ISR)
                                   ├── app  (Nuxt SSR/SPA)
                                   └── /api → .NET API (Modular Monolith)
                                                 ├── PostgreSQL (DO Managed)
                                                 ├── Redis (cache/sessions/rate-limit)
                                                 └── DO Spaces (media)  + Hangfire (jobs)
```

* **Nginx** terminates TLS and routes by host/path to `site`, `app`, and the API.
* **CDN** fronts static/ISR content and Spaces-served media.
* **PostgreSQL** via DO Managed Database (backups, PITR).
* **Redis** for cache, sessions, and rate limiting.
* **DO Spaces** (S3-compatible) for media; local dev uses MinIO.
* **Hangfire** runs in-process (P1); can move to a dedicated worker later.

## 3. Containerization

* Each deployable has a multi-stage Dockerfile with both a `dev` and a `runtime` target:
  `backend/Dockerfile` (`api`) and `frontend/Dockerfile` (`site` and `app`, selected by the `APP`
  build arg — one file, because both are built from the same pnpm workspace).
* `runtime` is what deploys: the API publishes to `mcr.microsoft.com/dotnet/aspnet` and runs as
  `$APP_UID`; the Nuxt apps ship the Nitro node-server output and run as `node`. Neither runs as root.
* `dev` targets exist only for local work (`dotnet watch` / `nuxt dev` over bind-mounted source) and
  are never deployed. See [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md).
* Local: `docker-compose.yml` brings up infrastructure by default; the `apps` profile adds the API and
  both Nuxt apps.
* Images built in CI, tagged by commit SHA, pushed to a registry.

Migrations are applied on startup **in Development only** (per-module `IHostedService` initializers),
so a fresh clone self-provisions. Deployed environments never auto-migrate — see §4.

## 4. CI/CD (GitHub Actions)

* **CI (every PR):** restore/build, unit + integration tests (Testcontainers), architecture-fitness,
  lint/typecheck, vulnerability scan. Frontend: build affected workspaces (pnpm filter), typecheck,
  component tests.
* **CD:** on merge to the release branch → build images → deploy to **staging** automatically →
  **production** on manual approval (solo-friendly gate).
* **Migrations:** applied as an explicit, ordered deploy step (never auto-migrate on app start in prod);
  forward-only, reviewed.

## 5. Secrets & configuration

* Never in source or images. Injected via environment / DO app-level secrets.
* Required secrets: DB connection, Redis connection, JWT signing key, Google/GitHub OAuth client
  id/secret, Spaces key/secret, email provider key.
* Rotate keys on a schedule; separate credentials per environment with least privilege.

## 6. Observability & ops

* Structured logs (Serilog) shipped to a central sink; correlation via `traceId`.
* `/health` checks (DB, Redis, Spaces) wired to the platform's health monitoring.
* Error tracking (e.g. Sentry) — provider-abstracted.
* Uptime monitoring on `site` content pages and the API health endpoint.

## 7. Backups & recovery

* Managed PostgreSQL automated backups + point-in-time recovery.
* Spaces media is durable; content JSONB lives in PostgreSQL (covered by DB backups + version history).
* Document and periodically test a restore procedure.

## 8. Scaling path

* Read path scales horizontally: CDN/ISR absorb most content traffic; the API scales behind Nginx.
* Redis offloads hot reads.
* Later: move Hangfire to dedicated workers, introduce OpenSearch, and — only if justified — Kubernetes
  and RabbitMQ (per roadmap). None are required for Phase 1.

## 9. Zero-downtime & rollback

* Rolling deploys behind Nginx; health-gated.
* Rollback = redeploy the previous image tag; DB migrations are forward-only, so destructive changes are
  done in expand/contract steps to keep rollback safe.
