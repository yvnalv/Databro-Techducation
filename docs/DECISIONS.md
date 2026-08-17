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
| [0006](adr/0006-postgres-fts-search.md) | PostgreSQL full-text search for Phase 1 | Accepted; mechanism superseded by [0010](adr/0010-fts-lives-in-content.md) |
| [0007](adr/0007-unify-article-lesson.md) | Unify Article and Lesson on one content engine | Accepted |
| [0008](adr/0008-cross-module-contracts-in-platform.md) | Cross-module read contracts live in Platform | Accepted |
| [0009](adr/0009-inline-rich-text-node-tree.md) | Inline rich text as a ProseMirror-compatible node tree | Accepted |
| [0010](adr/0010-fts-lives-in-content.md) | Phase 1 full-text search lives in Content, not the Search module | Accepted |
| [0011](adr/0011-media-storage-and-image-processing.md) | S3-compatible media storage, with images re-encoded on upload | Accepted |
| [0012](adr/0012-lesson-bodies-live-in-content.md) | Lesson bodies live in Content, in their own table beside articles | Accepted |

## Decisions deferred (to be ADR'd when their phase begins)

* Playground code-execution strategy (client WASM vs. server sandbox) — Phase 3.
* LLM/embedding provider selection and abstraction specifics — Phase 3.
* Payment provider specifics and entitlement model — Phase 3.
* Search upgrade trigger and OpenSearch adoption — when FTS limits are hit.
* Newsletter provider — end of Phase 1 / Phase 2.
* ~~Syntax highlighting strategy for code blocks (build-time e.g. Shiki vs. client-side)~~ —
  **decided and built.** Shiki, run on the server, with the result travelling in the page payload
  and the renderer doing a lookup rather than a computation. No highlighter reaches the browser
  (verified against the built image: the client bundle contains no Shiki or Oniguruma). Too small
  for its own ADR; the reasoning is in the CHANGELOG and `apps/site/server/utils/highlight.ts`.
* Inline rich-text marks (bold/italic/link) inside a paragraph block — the `marks` field is reserved
  but unspecified; it must be a structured renderer, never raw HTML.

## Process

* Propose a decision as a new ADR (next number) with status **Proposed**.
* Discuss trade-offs; on agreement, mark **Accepted** and update this index + the CHANGELOG.
* To change an accepted decision, write a new ADR with status **Accepted** that marks the old one
  **Superseded by ADR-NNNN**. Never edit an accepted ADR's decision.
