# ADR-0014 — Searching across modules

Status: **Accepted**
Date: 2026-08-17
Deciders: Project owner
Resolves the open question left by [ADR-0010](0010-fts-lives-in-content.md).

## Context

ADR-0010 put full-text search inside Content and said plainly when that would expire: *"the moment a
second module owns searchable content (Learning, Phase 2), this decision expires — a union across
modules cannot live inside one of them."*

That moment has arrived, and with real consequences rather than architectural tidiness. Public course
pages shipped; a learner searching for "retrieval" gets articles and **nothing about the course of
the same name**. It is the most visible defect in the product.

What is searchable now, and who owns it:

| Content | Learning |
|---|---|
| Article titles, summaries, bodies | Course titles, summaries |
| Lesson **bodies** (title, summary, blocks) | Learning path titles, summaries |
| | Lesson objectives, difficulty |

So the split is genuinely across two modules, and CLAUDE.md rule 10 forbids either reading the
other's tables.

Current scale: 34 published articles, 1 course, 2 lesson bodies. Worth stating, because it changes
which costs are real and which are imagined.

## Options

### A. Extend Content's search across schemas

One index, one query, one ranked list — by having Content read `learning.courses`.

**Rejected outright.** It is exactly what rule 10 forbids, and the rule is load-bearing: it is what
makes a module extractable later. Recorded only so it is clear it was considered rather than missed.

### B. Each module searches its own content; results are segmented

Learning gains a generated `tsvector` on `courses` — the same pattern Content already proved. The
endpoint queries both modules through their own application services and returns **segmented**
results: courses in one group, articles in another.

* **For:** no new infrastructure, no outbox, no rule violation. Each module owns its own index, so
  extraction stays mechanical.
* **For, and this is the part that makes it work:** segmenting dissolves the hardest problem.
  Merging two independently-computed `ts_rank` scores into one ordering is not meaningful — the
  numbers come from different corpora with different statistics, and any blend is a fabricated
  ordering dressed up as relevance. Segmented results never have to make that comparison.
* **For:** it is arguably the better interface anyway. A course and an article are different
  commitments, and a learner deciding between them is served by seeing which is which.
* **Against:** no single blended relevance ranking, and no cross-type facets. If those are ever
  wanted, this does not grow into them — it is replaced.
* **Against:** two queries per search, and the `tsvector` technique is repeated per module (the
  *code* can be shared through `Platform.Persistence`; the schema cannot).

### C. The real Search module, fed by integration events

ADR-0006's original design: a `Search`-owned denormalized index, kept current by consuming events,
rebuildable from source. Requires the **transactional outbox**, which is currently one marker
interface.

* **For:** one index, one query, genuine cross-type ranking and facets. The only option that reaches
  OpenSearch without another rewrite.
* **Against:** the outbox has to be built first, and building it now means designing it around
  whichever consumer happens to need it first — the mistake ADR-0010 already named. It deserves a
  second consumer before its shape is fixed.
* **Against:** it is a large slice to buy a ranking property that segmented results make unnecessary
  at 35 documents.

### D. A shared index table, written synchronously by each module

Search owns an index table; each module writes to it through a Platform contract on publish. No
outbox.

* **Against:** it has the consistency hole ADR-0010 rejected, just moved. Two DbContexts means two
  transactions, so a failed index write leaves the index disagreeing with the catalogue — silently,
  because nothing surfaces the disagreement. Sharing one transaction across contexts is possible and
  fiddly, and buys a design nobody wants to keep.

## Decision

**Option B.** It fixes the actual defect — a learner cannot find a course — with no new
infrastructure and no rule violation, and the property it gives up is one that segmentation makes
unnecessary rather than merely tolerable. Blending `ts_rank` scores across two corpora would produce
an ordering that looks authoritative and is not; declining to compute it is the honest choice, not a
compromise.

C stays the target for the day cross-type ranking, facets, or scale actually demand it — and by then
the outbox will have a second consumer to shape it, which is a better time to build it than now.

## Consequences

* Positive: courses become findable this slice, not after an outbox slice.
* Positive: each module keeps its own index, so nothing about extraction gets harder.
* Trade-off: the endpoint composes two sources, so `matchMode` and paging become per-segment. The
  response shape changes, which is a breaking change to `/api/v1/search` — worth doing now, while
  the only consumer is our own site.
* Trade-off: a future blended ranking is a replacement, not an extension. Accepted knowingly.
* Obligates: revisit when either a blended list is genuinely wanted or a third module owns
  searchable content — two modules can be segmented in a UI, five cannot.

## Not settled here

* **The transactional outbox** is still unbuilt and still needed for cross-module effects generally.
  This ADR removes search as its forcing function; it does not remove the need.
* **Lesson bodies** stay out of search. They have no public URL — a lesson is reached through its
  course — so a search result pointing at one would have nowhere to go. When lesson pages exist,
  this needs revisiting.

## References

[ADR-0006](0006-postgres-fts-search.md); [ADR-0010](0010-fts-lives-in-content.md);
[ADR-0013](0013-learning-curriculum-invariants.md); [CLAUDE.md](../../CLAUDE.md) rule 10.
