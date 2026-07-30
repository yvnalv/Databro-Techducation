# DataBro Changelog

## [2026-07-30 06:00:00 UTC]

CHG-0004 — Harden the Content module: validation and tests

- Added FluentValidation request validation (create/update article, content document, blocks) enforced
  via a reusable minimal-API `ValidationFilter<T>` that returns the standard `validation_failed`
  envelope with per-field details (docs/ERROR_HANDLING.md).
- Exposed `POST /api/v1/authoring/articles/{id}/unpublish`; added `ArticleService.UnpublishAsync`.
- New test project `DataBro.Modules.Content.Tests`:
  - Domain unit tests for `Slug` (validation/normalization) and `Article`
    (publish/versioning/unpublish business rules CT-1/CT-5/CT-8).
  - Full-stack API integration tests against a throwaway PostgreSQL container (Testcontainers) via
    `WebApplicationFactory<Program>`: happy-path create → publish → public read, publish gating,
    duplicate-slug conflict, invalid-slug/empty-title validation, blockless-publish `422`, and
    unpublish hiding from public read.
- Whole suite green: build 0/0; 29 tests pass (4 architecture + 25 Content).

---

## [2026-07-30 02:30:00 UTC]

CHG-0003 — Local dev infrastructure and the first Content vertical slice

- Added `docker-compose.yml` (PostgreSQL 16, Redis 7, MinIO) with healthchecks, `.env.example`, and a
  gitignored local `.env`. Postgres is mapped to host port 5439 (5432–5434 were occupied locally).
- Wired EF Core 9.0.18 + Npgsql 9.0.4 (pinned; SDK stays on 9.0.309). Introduced a `Platform.Persistence`
  shared infra project so EF never leaks into domain-facing `Platform`: audit `SaveChanges` interceptor,
  soft-delete global query filter, client-generated-key convention (`ValueGeneratedNever`), `SystemClock`,
  and `NullCurrentUser` (until Identity supplies the user).
- Content domain: `Article` aggregate (typed JSONB `draft_blocks`/`published_blocks`, `Slug` value
  object, `SeoMetadata`, `Visibility`), append-only `ArticleVersion` history, `Publish`/`Unpublish`
  with domain events and business-rule enforcement (CT-1, CT-5, CT-6, CT-8).
- Content persistence: `ContentDbContext` (owns the `content` schema, snake_case naming), EF configs
  with JSONB value converters, `ArticleRepository`, DI wiring, design-time factory, and the initial
  migration applied to Postgres.
- Content API: public read (`GET /api/v1/articles`, `/{slug}`) and authoring
  (`POST /api/v1/authoring/articles`, `PATCH`, `/{id}/publish`) behind the standard response envelope.
- Verified end-to-end against Dockerized Postgres: create draft → 404 while unpublished → publish
  (snapshots blocks, writes an immutable version, sets `published_at`) → served publicly by slug;
  empty article rejected with `business_rule_violation` (422). Build 0/0; 4 architecture tests pass.

---

## [2026-07-29 10:30:00 UTC]

CHG-0002 — Scaffold backend modular monolith and frontend monorepo

- Backend (.NET 9, SDK pinned to 9.0.309 via `global.json`): created `DataBro.sln` with the
  `Platform` shared kernel (Entity, AggregateRoot, Result/Error, integration-event building blocks),
  the `Identity`/`Content`/`Media`/`Search` modules each across Domain/Application/Infrastructure/Api
  layers, an API host that composes every module (health endpoint + per-module endpoints), and a
  NetArchTest architecture-fitness test project.
- Enforced boundaries: a scoped `src/Modules/Directory.Build.props` grants the ASP.NET Core framework
  reference only to Infrastructure/Api, keeping Domain/Application free of web/framework dependencies.
  Four architecture tests pass (Domain purity, no cross-module dependencies).
- Verified: full solution builds with 0 warnings/0 errors; architecture tests green; the running host
  serves `/health` and each module's `/_ping` endpoint.
- Frontend (pnpm workspace monorepo): `apps/site` (public, SSG/ISR) and `apps/app` (authenticated
  Nuxt 4 apps), plus shared packages `@databro/ui` (Tailwind preset/tokens), `@databro/api-client`
  (typed envelope client), and `@databro/types` (API + content-block schema). Shared packages
  typecheck clean and the `site` app builds end-to-end.
- Extended `.gitignore` for .NET (`bin`/`obj`) and Node/Nuxt (`node_modules`/`.nuxt`/`.output`)
  artifacts.

---

## [2026-07-29 00:00:00 UTC]

CHG-0001 — Initial project documentation

- Established the foundational documentation set: `CLAUDE.md` master instructions, `README.md`, and
  the `docs/` tree (PRD, ROADMAP, STATUS, ARCHITECTURE, MODULES, DATABASE, CONTENT_MODEL,
  FRONTEND_ARCHITECTURE, SEO, API_SPEC, SECURITY, BUSINESS_RULES, ERROR_HANDLING, DECISIONS,
  CODING_STANDARDS, TESTING, DEPLOYMENT, GLOSSARY).
- Recorded the initial architecture decisions as ADR-0001 through ADR-0007.
- Locked the load-bearing decisions: Modular Monolith + Clean Architecture; B2C-first tenancy;
  articles-first wedge; in-house CMS scoped to articles for Phase 1; unified Article/Lesson content
  engine; typed JSONB content blocks; two-app frontend monorepo; PostgreSQL FTS for initial search.
- No application code yet — design phase.
