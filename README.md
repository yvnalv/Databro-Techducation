# DataBro

A production-grade online learning platform specializing in **AI, Data Science, Machine Learning,
Deep Learning, LLM Engineering, RAG, AI Agents, Data Engineering, Python, SQL, and Software
Engineering**.

DataBro is not a blog. It is a learning ecosystem: educational articles, structured learning paths,
interactive courses, coding playgrounds, quizzes, projects, an AI tutor, community, certifications,
and enterprise learning.

## Status

**Phase 1 — Foundation & Content**, in progress. Identity (auth + RBAC) and Content (block-based
articles with versioning and publishing) are working, and published articles render on the public
site with full SEO output. Taxonomy, search, and media are next. See [docs/STATUS.md](docs/STATUS.md).

## Quick start

Requires Docker, .NET 9 SDK, Node 22+, and pnpm.

```powershell
cp .env.example .env
./scripts/dev-up.ps1                                      # PostgreSQL, Redis, MinIO
dotnet watch --project backend/src/Api/DataBro.Api run    # API  -> :5158
pnpm --dir frontend install; pnpm --dir frontend dev:site # site -> :3000
```

Or run everything in Docker with `./scripts/dev-up.ps1 -Apps`. Full instructions, including how to
verify a change, are in [docs/LOCAL_DEVELOPMENT.md](docs/LOCAL_DEVELOPMENT.md).

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 9, ASP.NET Core Web API, EF Core |
| Database | PostgreSQL, Redis |
| Jobs | Hangfire |
| Frontend | Vue 3, Nuxt 4, TypeScript, Tailwind CSS, Pinia (pnpm monorepo) |
| Infra | Docker, GitHub Actions, Nginx, DigitalOcean |
| Future | Kubernetes, OpenSearch, pgvector, RabbitMQ |

## Architecture at a Glance

* **Modular Monolith** with Clean Architecture (Domain / Application / Infrastructure / API per module).
* **B2C-first** — global content catalog, global users; Enterprise (Organization scope) is a Phase 4
  bolt-on.
* **One content engine** — Articles and Lessons are the same primitive (typed JSONB blocks, versioned).
* **Two frontend apps** — a public SEO-critical content `site` and an authenticated learner `app`.

## Documentation

Start at [docs/README.md](docs/README.md). The authoritative project instructions live in
[CLAUDE.md](CLAUDE.md).

## Repository Layout

```
/
├── CLAUDE.md
├── README.md
├── CHANGELOG.md
├── docker-compose.yml   (local infra; `apps` profile also runs the API + both Nuxt apps)
├── docs/
├── scripts/             (local dev helpers: dev-up, dev-smoke, dev-seed-article, dev-grant-role)
├── backend/             (.NET solution — Modules/*)
└── frontend/            (pnpm monorepo — apps/site, apps/app, packages/*)
```

## License

Proprietary — all rights reserved (subject to change).
