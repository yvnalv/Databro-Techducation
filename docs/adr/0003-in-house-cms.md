# ADR-0003 — Build the CMS in-house (scoped to articles for Phase 1)

Status: Accepted
Date: 2026-07-29
Deciders: Project owner

## Context

DataBro needs a content authoring system. Options ranged from a headless CMS (Payload/Strapi/Directus/
Sanity), a git/Markdown workflow, or an in-house .NET module. A full production CMS is one of the
largest things a solo developer can build, and can dwarf the rest of the roadmap.

## Decision

Build the CMS **in-house as a `.NET` `Content` module**, for full control over the domain model,
native fit with the stack, and no external service dependency. **Scope Phase 1 strictly to article
authoring** — no course/lesson/quiz authoring UI until Phase 2. Because DataBro is articles-first, an
article CMS is a fraction of a full course-authoring CMS, which makes in-house tractable for a solo
builder.

## Alternatives considered

* **Headless CMS (buy)** — fastest to real content, authoring solved on day one; but adds another
  service and data store, and cedes control of the core content domain model that everything else
  (lessons, AI, search) builds on. Rejected given long-term ownership goals.
* **Git/Markdown (MDX)** — great for developer-authored articles, but weak for structured blocks,
  per-block features, versioning UX, and non-technical authoring. Rejected; also conflicts with the
  typed-block model (ADR-0004).

## Consequences

* Positive: full control of the content domain; one engine reused for lessons later; no vendor lock-in.
* Trade-offs: authoring UX is our responsibility. Mitigated by ruthless Phase 1 scoping (articles
  only) and reusing the same engine for lessons (ADR-0007).
* Risk: scope creep is the top solo risk — tracked in [STATUS.md](../STATUS.md).

## References

[MODULES.md](../MODULES.md) → Content; [CONTENT_MODEL.md](../CONTENT_MODEL.md); ADR-0004; ADR-0007.
