# DataBro — Architectural Decision Records (ADR) Index

This file indexes all Architectural Decision Records. Each ADR captures the context, the decision, the
alternatives considered, and the consequences of a significant choice. ADRs are immutable once
accepted; a superseding decision gets a new ADR that references the old one.

Individual records live in [adr/](adr/). Template: [adr/0000-template.md](adr/0000-template.md).

| ADR | Title | Status |
|---|---|---|
| [0001](adr/0001-modular-monolith.md) | Modular Monolith with Clean Architecture | Accepted |
| [0002](adr/0002-b2c-not-multitenant.md) | B2C-first — no row-level multi-tenancy | Accepted |
| [0003](adr/0003-in-house-cms.md) | Build the CMS in-house (scoped to articles for Phase 1) | Accepted |
| [0004](adr/0004-content-blocks-jsonb.md) | Typed content blocks stored as versioned JSONB | Accepted |
| [0005](adr/0005-two-app-frontend-monorepo.md) | Two-app frontend in a pnpm monorepo | Accepted |
| [0006](adr/0006-postgres-fts-search.md) | PostgreSQL full-text search for Phase 1 | Accepted |
| [0007](adr/0007-unify-article-lesson.md) | Unify Article and Lesson on one content engine | Accepted |

## Decisions deferred (to be ADR'd when their phase begins)

* Playground code-execution strategy (client WASM vs. server sandbox) — Phase 3.
* LLM/embedding provider selection and abstraction specifics — Phase 3.
* Payment provider specifics and entitlement model — Phase 3.
* Search upgrade trigger and OpenSearch adoption — when FTS limits are hit.
* Newsletter provider — end of Phase 1 / Phase 2.

## Process

* Propose a decision as a new ADR (next number) with status **Proposed**.
* Discuss trade-offs; on agreement, mark **Accepted** and update this index + the CHANGELOG.
* To change an accepted decision, write a new ADR with status **Accepted** that marks the old one
  **Superseded by ADR-NNNN**. Never edit an accepted ADR's decision.
