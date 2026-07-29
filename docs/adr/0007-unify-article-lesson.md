# ADR-0007 — Unify Article and Lesson on one content engine

Status: Accepted
Date: 2026-07-29
Deciders: Project owner

## Context

Phase 1 ships **Articles**; Phase 2 introduces **Lessons** inside courses. Both are long-form,
block-based learning content. A naive approach builds an article system now and a separate lesson
system later — duplicating the hard parts (block model, versioning, draft/publish, rendering, SEO).

## Decision

Treat **Article and Lesson as the same primitive — a Content Unit** composed of typed content blocks
with shared versioning, draft/publish, rendering, and SEO. A Lesson (Phase 2) **reuses** the Content
engine and adds course context (belongs to a Course Module) plus learning metadata (objectives,
prerequisites, estimated time, difficulty, ordering, related lessons). Build the hard part once.

## Alternatives considered

* **Separate Article and Lesson systems** — simpler to reason about in isolation now, but guarantees a
  second content/versioning/rendering implementation and long-term drift between them. Rejected.
* **Force everything into one table/type immediately** — over-generalizes before Lessons exist. We
  instead build the reusable *engine* (blocks/versioning/rendering) now and layer Lesson context in
  Phase 2 without a rewrite.

## Consequences

* Positive: one content engine, one renderer, one versioning model, one SEO story — reused across
  articles and lessons; less code, no drift.
* Trade-offs: the content engine must be designed as context-agnostic (no article-only assumptions in
  the core), which requires a little foresight now.
* Obligates: the Phase 1 Content module keeps article-specific concerns (e.g. category/tag) separable
  from the shared content core, so Lessons can reuse the core cleanly.

## References

[CONTENT_MODEL.md](../CONTENT_MODEL.md); [MODULES.md](../MODULES.md); ADR-0004.
