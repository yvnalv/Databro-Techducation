# DataBro — Status

Snapshot of where the project is, what's next, and what's open. Update this with every meaningful
milestone.

Last updated: 2026-07-29.

## Current phase

**Phase 1 — Foundation & Content.** Sub-stage: **Content engine live end-to-end → hardening + Identity**.

## Done

* Locked the load-bearing architecture decisions (ADR-0001 … ADR-0007).
* Authored the foundational documentation set (CLAUDE.md + docs/).
* Scaffolded the **backend** Modular Monolith and **frontend** pnpm monorepo.
* **Local dev infra:** `docker-compose.yml` (PostgreSQL on host port **5439**, Redis, MinIO) +
  `.env.example` / gitignored `.env`.
* **EF Core foundation:** EF 9.0.18 + Npgsql 9.0.4 pinned; `Platform.Persistence` (audit interceptor,
  soft-delete filter, client-generated keys, clock) keeps EF out of the domain-facing `Platform`.
* **Content vertical slice (working):** `Article` aggregate with JSONB blocks + versioning,
  `ContentDbContext` (snake_case, `content` schema), repository, initial migration applied, and public
  read + authoring endpoints. Verified create → publish → public read against Dockerized Postgres.
* Build 0/0; 4 architecture-fitness tests pass.

## In progress

* Content module hardening (see next up).

## Next up (proposed order)

1. Content hardening: request validation (FluentValidation), unit/integration tests for
   publish/versioning/slug rules, unpublish/schedule endpoints, categories & tags.
2. Wire the transactional outbox + `ArticlePublished` handling (Search reindex / cache invalidation).
3. Implement Identity (registration, email verification, login, JWT + refresh, RBAC) and enforce
   authoring authorization; replace `NullCurrentUser`/`SystemAuthorId`.
4. Surface the published article on the `site` app (SSG/ISR) using `@databro/api-client`.
5. PostgreSQL FTS search; SEO metadata/redirects; Media upload to MinIO/Spaces.
6. CI pipeline (build/test + architecture-fitness gate).

## Open questions / to be ADR'd later

* Exact content-block type catalog (finalize before CMS build — see [CONTENT_MODEL.md](CONTENT_MODEL.md)).
* Newsletter provider (Resend vs. ConvertKit) — decide end of Phase 1.
* Playground execution strategy (client WASM vs. server sandbox) — Phase 3 ADR.
* LLM provider(s) and embedding model — Phase 3 ADR.
* Billing provider specifics — Phase 3 ADR.
* Search upgrade trigger (when to move FTS → OpenSearch).

## Risks being watched

* **Solo + in-house CMS scope creep.** Mitigation: Phase 1 CMS is articles-only; no course/quiz
  authoring until Phase 2.
* **Two-app maintenance tax.** Mitigation: shared `packages/*` in a monorepo; no duplicated
  design/auth/API logic.
