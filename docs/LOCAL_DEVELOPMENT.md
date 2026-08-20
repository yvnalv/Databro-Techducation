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
| Hangfire dashboard | http://localhost:5158/hangfire (dev only) |

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

### Signing in to the CMS

The API seeds an administrator on startup, so there is nothing to create by hand:

```
http://localhost:3001
admin@databro.local
Databro-Dev-1!
```

Seeded **only** where `IHostEnvironment.IsDevelopment()` is true. The gate sits at the call site in
`IdentityInitializer` rather than inside the seeder, so the decision is visible where it is made — a
seeded admin with a documented password is a back door in any other environment. It is idempotent:
an existing account is left alone, password included, so a local change survives a restart.

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

## Email in development

Every outbound email is captured by **Mailpit** and delivered nowhere. Read them at
**<http://localhost:8025>**.

The API talks real SMTP to it (`Email:Provider=smtp`), so the whole path is exercised locally rather
than stubbed — composing, sending, delivering, opening. Registering a user produces a confirmation
message whose link is clickable and lands on `/verify-email` in the app.

Running the API on the host instead of in Compose defaults to `Email:Provider=log`, which writes the
message to the console. Point it at Mailpit with `Email:Smtp:Host=localhost` and `Port=1025` if you
want the UI.

Nothing relays outward. `MP_SMTP_AUTH_ACCEPT_ANY` means Mailpit accepts anything and forwards
nothing, which is precisely the accident it is there to prevent.


## Social login setup (Google and GitHub)

Social login needs two OAuth app registrations that only the project owner can create. The code can
be written and tested without them, but nobody can actually sign in until the four values below exist
in `.env`. Roughly ten minutes, both free.

### The callback URLs

Register these exactly — they are compared character for character, and a trailing slash is a
different URL:

```
http://localhost:5158/api/v1/auth/oauth/google/callback
http://localhost:5158/api/v1/auth/oauth/github/callback
```

Both providers allow plain `http` **for localhost only**, so local development needs no TLS.

### 1. Google

At <https://console.cloud.google.com>:

1. Create or select a project — project dropdown, top left → **New Project** → `DataBro`.
2. **APIs & Services → OAuth consent screen.** Do this *before* creating credentials; the credential
   option stays greyed out until a consent screen exists.
   * User type **External**
   * App name `DataBro`; your address for support and developer contact
   * **Scopes** → add `openid`, `.../auth/userinfo.email`, `.../auth/userinfo.profile`
   * **Test users** → add your own Google address. While the app is unpublished only listed test
     users can sign in. That is expected, not a misconfiguration.
3. **APIs & Services → Credentials → Create credentials → OAuth client ID**
   * Application type **Web application**
   * Authorised redirect URI: the Google callback above
4. Copy the **Client ID** and **Client secret**.

### 2. GitHub

At <https://github.com/settings/developers> → **OAuth Apps** → **New OAuth App**:

* Application name `DataBro (local)`
* Homepage URL `http://localhost:3000`
* Authorization callback URL: the GitHub callback above

Then **Generate a new client secret** and copy it — GitHub shows a secret once and never again.

> **A GitHub OAuth App accepts exactly one callback URL.** Deploying therefore needs a *second* app
> (`DataBro (production)`) rather than an edit to this one — repointing the existing app at the VPS
> breaks local sign-in the moment you save. Google is the opposite: one client, many redirect URIs,
> so the deployed callback is simply added to the same client.

### 3. Where the values go

`.env` is gitignored and `.env.example` is the tracked template (see §2). Add to **`.env`**:

```bash
GOOGLE_CLIENT_ID=…apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=…
GITHUB_CLIENT_ID=…
GITHUB_CLIENT_SECRET=…
```

**Never put a real value in `.env.example`, in any `appsettings*.json`, or in anything else git
tracks.** The four key names appear empty in `.env.example` as labelled slots to fill rather than
names to guess.

### 4. Applying the values

* **Host-run API** (`dotnet run`): `.env` is loaded into the process at startup by `DotNetEnv`, so a
  restart is enough.
* **Containerised API** (`docker compose --profile apps up`): environment variables are fixed when a
  container is **created**, not when its code hot-reloads. After adding or changing any value in
  `.env`, recreate the container with `docker compose --profile apps up -d` — otherwise the API keeps
  the environment it started with, and Google answers `Error 400: invalid_request — Missing required
  parameter: client_id` because it received an empty `client_id`. The hot-reload volume refreshes code,
  never environment.

### A note on why GitHub needs an extra scope

`ID-3` links a social account to an existing one by **verified email**. Google returns `email` and
`email_verified` in its userinfo response, so one call is enough. GitHub's `/user` endpoint returns
`null` for email whenever the user has kept it private — which is common — so the implementation
requests `read:user user:email` and calls `/user/emails` to find the primary address that is marked
verified. Without that second call a GitHub sign-in would silently create a duplicate account instead
of linking to the existing one.

## 6. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `A compatible .NET SDK was not found` | `backend/global.json` pins the 9.0.3xx band. Install a 9.0.3xx SDK. |
| `Npgsql … connection refused` on port 5439 | Infra is not up, or `POSTGRES_PORT` in `.env` drifted from `appsettings.Development.json`. |
| Port already in use on `up` | Another local stack owns it. Change the port in `.env` — nothing else hardcodes it. |
| Integration tests hang or fail to start | Docker Desktop is not running; Testcontainers needs it. |
| `403` on an authoring endpoint | The role is right but the token predates the grant. Log in again. |
| Container edits do not trigger a reload | Polling watch is slow on Windows bind mounts. Prefer option A, or restart the service. |
