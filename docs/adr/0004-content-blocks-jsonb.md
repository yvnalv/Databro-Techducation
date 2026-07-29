# ADR-0004 — Typed content blocks stored as versioned JSONB

Status: Accepted
Date: 2026-07-29
Deciders: Project owner

## Context

Content units (articles now, lessons later) need a storage model that supports rich, structured,
extensible content — including future interactive blocks (quizzes, exercises, playground) and
per-block features (analytics, anchors) — while remaining fast to render and query.

## Decision

Store content as an ordered array of **typed blocks** persisted in **PostgreSQL `jsonb`**. Each block
has a stable `id`, a `type`, and a type-specific `data` object. Content is **versioned** with a mutable
`draft_blocks` and an immutable `published_blocks` snapshot, plus append-only `article_versions`
history. Query with GIN indexes where needed.

## Alternatives considered

* **Markdown/MDX string** — simplest and developer-friendly, but boxes us out of interactive blocks,
  per-block analytics, quiz references, and structured querying. Rejected.
* **Normalized block-per-row** — maximally queryable, but heavy CRUD and versioning overhead for little
  Phase 1 benefit; complicates atomic snapshotting. Rejected for now.

## Consequences

* Positive: flexible authoring, clean Nuxt rendering via a shared renderer registry, easy forward
  compatibility (unknown block types degrade gracefully), and content stays parseable for future AI
  embeddings. Atomic publish snapshot is trivial (copy JSONB).
* Trade-offs: schema-on-read for block internals; we enforce a typed block schema in code
  (`packages/types`) and validate on save. Deep querying into blocks relies on GIN/expression indexes.
* Obligates: a documented, versioned block-type catalog (see [CONTENT_MODEL.md](../CONTENT_MODEL.md))
  and sanitization on render (see [SECURITY.md](../SECURITY.md)).

## References

[CONTENT_MODEL.md](../CONTENT_MODEL.md); [DATABASE.md](../DATABASE.md); ADR-0007.
