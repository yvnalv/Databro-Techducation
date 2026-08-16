# ADR-0010 — Phase 1 full-text search lives in Content, not the Search module

Status: Accepted
Date: 2026-08-16
Deciders: Project owner
Supersedes: the *mechanism* in [ADR-0006](0006-postgres-fts-search.md) (PostgreSQL FTS as the Phase 1
engine still stands; the denormalized `search_documents` table fed by integration events does not).

## Context

ADR-0006 chose PostgreSQL FTS for Phase 1 and specified **how**: a `Search` module owning a
denormalized `search_documents` table, kept current by consuming Content's integration events, fully
rebuildable from source.

Implementing it now runs into two facts that were not true when ADR-0006 was written:

1. **The transactional outbox does not exist.** ADR-0006's design assumes reliable event delivery.
   Without it, "publish an article" and "update the search row" are two writes with no atomicity
   between them. The first partial failure leaves the index silently disagreeing with the catalogue —
   an article that is published but unfindable, or findable but gone. A wrong search index is worse
   than a slow one, because nothing surfaces the disagreement.
2. **CLAUDE.md rule 10 forbids Search reading Content's tables.** That rule is right, and it is
   precisely what forces the copy. So the choice is not "copy or query directly" — it is "copy, with
   a synchronization problem" versus "keep it inside the module that owns the data."

Meanwhile the thing ADR-0006 was protecting — the ability to swap in OpenSearch later — does not
actually depend on where the Phase 1 index lives.

## Decision

**Phase 1 full-text search is implemented inside the Content module**, over the `articles` table:

* A `search_text` column holds the plain-text projection of the **published** blocks, written by the
  domain when an article publishes.
* A `search_vector` column is `tsvector GENERATED ALWAYS AS (…) STORED`, weighting `title` **A**,
  `summary` **B**, `search_text` **C**, stemmed with the `english` or `indonesian` configuration
  according to the row's `locale`. A GIN index backs it.
* A `pg_trgm` index on `title` backs a similarity fallback for queries that full-text search cannot
  match (typos).
* The public contract is `GET /api/v1/search`, mapped by Content.

**The `Search` module stays an empty reserved shell through Phase 1.** It is where the event-fed
OpenSearch index will live, and this ADR does not delete it.

**The stable seam is the HTTP endpoint, not a C# interface.** `GET /api/v1/search` is what the `site`
app depends on, and it is what must not change when the engine does. A `Platform.ISearchService`
abstraction with exactly one implementation and one caller — its own endpoint — would be ceremony
that protects nothing extra; it can be introduced at the moment there is a second implementation.

## Alternatives considered

* **Build ADR-0006 as specified (Search module + `search_documents` + events).** Rejected for now:
  the outbox it depends on is not built, so it would ship with a known consistency hole in the last
  Phase 1 exit criterion. It is still the target design — see "Obligates".
* **Build the outbox first, then the Search module.** Defensible, and it is the *ordered* version of
  the above. Rejected because it makes the last Phase 1 deliverable depend on an unrelated
  infrastructure slice, and because the outbox deserves its own design pass rather than being
  shaped by whichever consumer happens to need it first.
* **A `search_documents` table owned by Content** (same denormalization, no module hop). Rejected:
  it inherits the synchronization problem while giving up the one property that makes the generated
  column trustworthy — a `GENERATED ALWAYS … STORED` column *cannot* disagree with its row.

## Consequences

* Positive: the index is exact by construction, with no job, no event, and no rebuild command to
  remember. Per-locale stemming falls out of the same expression. Nothing to operate.
* Positive: the migration path is subtractive. When OpenSearch arrives, Search gains an index and an
  event consumer, the endpoint moves, and Content's implementation is deleted. Consumers see no
  change.
* Trade-off: `Search/` remains four empty marker projects for the rest of Phase 1. That is honest
  scaffolding, but it does mean the module list overstates what is built — recorded in
  [STATUS.md](../STATUS.md).
* Trade-off: search is coupled to Content's schema. Acceptable while Content is the only searchable
  thing. **The moment a second module owns searchable content** (Learning, Phase 2), this decision
  expires — a union across modules cannot live inside one of them.
* Obligates: build the transactional outbox before cross-module search, and revisit this ADR when
  either the outbox lands or Learning introduces the second searchable aggregate.

## References

[ADR-0006](0006-postgres-fts-search.md); [MODULES.md](../MODULES.md) → Search;
[DATABASE.md](../DATABASE.md); [CLAUDE.md](../../CLAUDE.md) rule 10.
