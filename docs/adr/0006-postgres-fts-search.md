# ADR-0006 — PostgreSQL full-text search for Phase 1

Status: Accepted — mechanism superseded by [ADR-0010](0010-fts-lives-in-content.md) (2026-08-16)
Date: 2026-07-29
Deciders: Project owner

> The core choice below — PostgreSQL FTS for Phase 1, OpenSearch as the upgrade path — stands.
> The *implementation* described here (a `Search`-owned `search_documents` table fed by integration
> events) was not built: it depends on a transactional outbox that does not exist yet, and shipping
> it without one would have meant a search index that can silently disagree with the catalogue.
> ADR-0010 records what was built instead and what would return this design to the table.

## Context

The articles-first platform needs search from Phase 1. A dedicated engine (OpenSearch/Elasticsearch) is
in the long-term stack but adds an operational component that is unjustified at launch volumes.

## Decision

Use **PostgreSQL full-text search** for Phase 1: a `Search` module maintains a denormalized
`search_documents` table with a weighted `tsvector` over title/summary/body/tags, plus a trigram index
for fuzzy fallback. The index is updated reactively from content integration events and is fully
rebuildable from source content. **OpenSearch** is the ADR'd upgrade path when relevance/faceting/scale
demand it.

## Alternatives considered

* **OpenSearch/Elasticsearch from day one** — better relevance/faceting/scale, but a whole extra
  service to run, secure, and sync. Premature. Rejected for Phase 1.
* **Third-party search SaaS (Algolia/Meilisearch Cloud)** — fast to integrate, but adds cost and a
  vendor dependency for a capability PostgreSQL covers well at current scale. Rejected for now.

## Consequences

* Positive: no new infrastructure; transactional consistency options; good-enough relevance for launch;
  the `Search` module boundary means swapping to OpenSearch later is an implementation change behind
  `ISearchService`, not an API change.
* Trade-offs: weaker relevance tuning, faceting, and typo-tolerance than a dedicated engine; large-scale
  performance ceilings.
* Obligates: keep `Search` behind a contract and event-driven so the engine can be swapped without
  touching consumers.

## References

[MODULES.md](../MODULES.md) → Search; [CLAUDE.md](../../CLAUDE.md) → Search.
