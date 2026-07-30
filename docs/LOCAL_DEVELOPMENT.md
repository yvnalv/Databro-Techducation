# DataBro — Local Development

How to run DataBro on a developer machine and verify a change end to end. For environments and
delivery, see [DEPLOYMENT.md](DEPLOYMENT.md); for the test strategy, see [TESTING.md](TESTING.md).

---

## 1. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| Docker Desktop | 4.x (Compose v2+) | Required — integration tests use Testcontainers too. |
| .NET SDK | 9.0.3xx | Pinned to the feature band in `backend/global.json`. |
| Node.js | 22+ | |
| pnpm | 10+ | `corepack enable` |

---

## 2. First run

```powershell
cp .env.example .env      # ports and dev credentials; gitignored
./scripts/dev-up.ps1
```

`dev-up.ps1` starts the infrastructure and waits for every healthcheck to pass.

`.env` owns the published host ports. The defaults avoid the ports commonly taken by other local
stacks — **PostgreSQL is on 5439**, not 5432, and `backend/src/Api/DataBro.Api/appsettings.Development.json`
matches. Change one, change the other.

| Service | Host port | Purpose |
|---|---|---|
| PostgreSQL 16 | `5439` | Application database. |
| Redis 7 | `6379` | Caching, sessions, rate limiting. |
| MinIO | `9000` / `9001` | S3-compatible object storage; stands in for DigitalOcean Spaces. |

---

## 3. The two ways to run

### A. Infrastructure in Docker, apps on the host — **the default**

The fastest inner loop, and what you want for day-to-day work: native file watching, native
debugger attach, no bind-mount overhead.

```powershell
./scripts/dev-up.ps1

# terminal 2 — API on http://localhost:5158, hot reload
dotnet watch --project backend/src/Api/DataBro.Api run

# terminal 3 — public site on http://localhost:3000, HMR
pnpm --dir frontend install
pnpm --dir frontend dev:site

# terminal 4 (optional) — learner app on http://localhost:3001
pnpm --dir frontend dev:app
```

In Development the API self-provisions: per-module startup initializers apply pending EF migrations
and seed the RBAC roles, so a fresh clone works immediately after `dev-up.ps1`.

### B. Everything in Docker

One command for the whole stack, with hot reload preserved — source is bind-mounted and both
`dotnet watch` and `nuxt dev` run inside the containers. Use it to verify container wiring, or when
you want a clean-machine reproduction.

```powershell
./scripts/dev-up.ps1 -Apps
```

| Service | URL |
|---|---|
| API | http://localhost:5158/health |
| Site | http://localhost:3000 |
| App | http://localhost:3001 |

Trade-offs to know before you pick this one:

* First start is slow (image build, then a cold compile inside the container).
* Bind-mounted file watching on Windows relies on polling, so reloads lag behind option A.
* The container writes its .NET build output to a `/artifacts` volume, never to the host's
  `bin`/`obj` — those hold Windows paths that are meaningless inside a Linux container. The same
  applies to `node_modules`, which are masked by anonymous volumes.
* **After changing frontend dependencies, rebuild and recreate**, or the containers keep the
  `node_modules` baked into the old image:
  ```powershell
  docker compose --profile apps down      # also drops the anonymous node_modules volumes
  docker compose --profile apps build
  ./scripts/dev-up.ps1 -Apps
  ```

Both options read the same `.env`, so the two never disagree about ports or credentials.

### The API has two addresses, and both are needed

In a containerised run the Nuxt apps reach the API at **two different addresses**, because SSR and
the browser are on different networks:

| | Config key | Value in Docker |
|---|---|---|
| Browser (hydration, client fetches) | `NUXT_PUBLIC_API_BASE_URL` | `http://localhost:5158` |
| Nuxt server (SSR, prerender) | `NUXT_API_INTERNAL_BASE_URL` | `http://api:8080` |

Inside the site container `localhost` is *the site container*, so using the public URL for SSR fails
with a connection refused. Running everything on the host needs only the public URL — leave the
internal one empty and it falls back.

This is exactly the class of bug the containerised profile exists to catch: it cannot reproduce on
the host, because there `localhost` happens to be right for both.

---

## 4. Verifying a change

Three layers, cheapest first:

```powershell
# 1. Build + unit/integration tests. Testcontainers spins up a throwaway Postgres,
#    so this is isolated from your dev database.
dotnet test backend/DataBro.sln

# 2. Frontend typecheck.
pnpm --dir frontend typecheck

# 3. End-to-end smoke test against the stack you are actually running.
./scripts/dev-smoke.ps1
```

`dev-smoke.ps1` walks the whole Phase 1 slice — register → grant Editor → login → create draft →
404 while unpublished → publish → public read by slug → unpublish → 404 — and fails loudly on the
first unexpected status. It is idempotent: every run uses a fresh user and slug.

---

## 5. Working with local data

```powershell
# psql shell
docker compose exec postgres psql -U databro -d databro

# grant a role — self-registration only ever assigns Reader
./scripts/dev-grant-role.ps1 -Email you@databro.local -Role Editor

# tail logs
docker compose logs -f postgres
docker compose --profile apps logs -f api

# stop, keeping data
docker compose --profile apps down

# wipe everything and start clean
./scripts/dev-up.ps1 -Reset
```

Permissions are stamped into the JWT when it is issued, so **log in again after a role change** —
refreshing the existing token is not enough.

### EF Core migrations

Each module owns its own schema and migration history, so migrations are always per-module:

```powershell
dotnet ef migrations add <Name> `
  --project backend/src/Modules/Content/DataBro.Modules.Content.Infrastructure `
  --startup-project backend/src/Api/DataBro.Api
```

Development applies pending migrations on startup. Deployed environments never do — see
[DEPLOYMENT.md](DEPLOYMENT.md).

---

## 6. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `A compatible .NET SDK was not found` | `backend/global.json` pins the 9.0.3xx band. Install a 9.0.3xx SDK. |
| `Npgsql … connection refused` on port 5439 | Infra is not up, or `POSTGRES_PORT` in `.env` drifted from `appsettings.Development.json`. |
| Port already in use on `up` | Another local stack owns it. Change the port in `.env` — nothing else hardcodes it. |
| Integration tests hang or fail to start | Docker Desktop is not running; Testcontainers needs it. |
| `403` on an authoring endpoint | The role is right but the token predates the grant. Log in again. |
| Container edits do not trigger a reload | Polling watch is slow on Windows bind mounts. Prefer option A, or restart the service. |
