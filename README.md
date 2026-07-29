# DataBro

A production-grade online learning platform specializing in **AI, Data Science, Machine Learning,
Deep Learning, LLM Engineering, RAG, AI Agents, Data Engineering, Python, SQL, and Software
Engineering**.

DataBro is not a blog. It is a learning ecosystem: educational articles, structured learning paths,
interactive courses, coding playgrounds, quizzes, projects, an AI tutor, community, certifications,
and enterprise learning.

## Status

Early design phase — architecture and documentation first, code second. See
[docs/STATUS.md](docs/STATUS.md).

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

## Repository Layout (planned)

```
/
├── CLAUDE.md
├── README.md
├── CHANGELOG.md
├── docs/
├── backend/        (.NET solution — Modules/*)
└── frontend/       (pnpm monorepo — apps/site, apps/app, packages/*)
```

## License

Proprietary — all rights reserved (subject to change).
