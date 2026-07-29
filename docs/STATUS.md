# DataBro — Status

Snapshot of where the project is, what's next, and what's open. Update this with every meaningful
milestone.

Last updated: 2026-07-29.

## Current phase

**Phase 1 — Foundation & Content.** Sub-stage: **scaffolding complete → implementing modules**.

## Done

* Locked the load-bearing architecture decisions (ADR-0001 … ADR-0007).
* Authored the foundational documentation set (CLAUDE.md + docs/).
* Scaffolded the **backend** Modular Monolith (`backend/DataBro.sln`): `Platform` shared kernel +
  `Identity`/`Content`/`Media`/`Search` modules across Domain/Application/Infrastructure/Api, API host
  wiring all modules, and NetArchTest architecture-fitness tests. Builds green (0/0); 4 arch tests
  pass; host serves `/health` and per-module `/_ping`.
* Scaffolded the **frontend** pnpm monorepo (`frontend/`): `apps/site`, `apps/app`, and shared
  `packages/ui|api-client|types`. Packages typecheck; `site` builds end-to-end.

## In progress

* Nothing actively coding — ready to start the first real vertical slice.

## Next up (proposed order)

1. Add local dev infra: `docker-compose.yml` (PostgreSQL, Redis, MinIO) + wire EF Core in `Platform`.
2. Implement Identity (registration, email verification, login, JWT + refresh, RBAC).
3. Implement the Content engine (Article aggregate, JSONB blocks, draft/publish/versioning) — the
   core domain and first end-to-end feature (API + `site` article page).
4. Categories, tags, authors, SEO metadata, redirects.
5. PostgreSQL FTS search; public site article/listing pages.
6. Media upload to DO Spaces.
7. CI pipeline (build/test + architecture-fitness gate).

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
