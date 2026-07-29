# DataBro — Status

Snapshot of where the project is, what's next, and what's open. Update this with every meaningful
milestone.

Last updated: 2026-07-29.

## Current phase

**Phase 1 — Foundation & Content.** Sub-stage: **design & documentation** (pre-code).

## Done

* Locked the load-bearing architecture decisions (ADR-0001 … ADR-0007).
* Authored the foundational documentation set (CLAUDE.md + docs/).

## In progress

* Nothing in code yet. Design phase.

## Next up (proposed order)

1. Scaffold the backend Modular Monolith solution (`backend/`) with `Platform`, `Identity`, `Content`,
   `Media`, `Search` module skeletons + architecture-fitness test.
2. Scaffold the frontend monorepo (`frontend/` — `apps/site`, `apps/app`, `packages/ui|api-client|types`).
3. Implement Identity (registration, verification, login, JWT, RBAC).
4. Implement the Content engine (Article aggregate, JSONB blocks, draft/publish/versioning).
5. Categories, tags, authors, SEO metadata, redirects.
6. PostgreSQL FTS search; public site article/listing pages.
7. Media upload to DO Spaces.

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
