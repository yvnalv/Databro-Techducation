# ADR-0013 — Curriculum shape and its three invariants

Status: Accepted
Date: 2026-08-17
Deciders: Project owner (decision delegated — see "How this was decided")

## Context

Phase 2 builds `LearningPath → Course → CourseModule → Lesson` over the content engine
([ADR-0012](0012-lesson-bodies-live-in-content.md)). Three questions had to be answered before any
of it could be modelled, and each is a product decision as much as a technical one:

1. Does a Course have a publish lifecycle of its own, separate from its lessons'?
2. How is ordering stored, given a drag-and-drop builder will rewrite it constantly?
3. What happens to a live course when a lesson's body is unpublished underneath it?

## How this was decided

These were put to the project owner with a recommendation for each and an explicit offer to build
with those recommendations if no preference came back. None did, so the recommendations were taken.
They are recorded here as decisions rather than left as assumptions in code comments, precisely
because they are reversible only at the cost of a migration and an authoring-flow change.

## Decision

### 1. A Course publishes independently of its lessons

`Course` carries its own `Draft` / `Published` / `Unpublished` status. A published course shows
**only its published lessons**; an unpublished lesson is simply absent from the learner's view.

The alternative — refusing to publish a course until every lesson is finished — was rejected because
it makes a large curriculum unpublishable until the very last lesson is written, and because courses
grow after launch. Publishing a course and then adding lessons to it over the following weeks is the
normal case, not an edge case.

This also matches how the platform already behaves everywhere else: draft content is not public, and
its absence is the mechanism rather than an error.

### 2. Ordering is a contiguous integer, normalised on every change

`CourseModule.Order` within a course, `Lesson.Order` within a module, `0..n-1` with no gaps. Every
reordering operation renumbers the whole sibling set as its final step.

A linked list reorders in O(1) and queries badly — every read walks the chain, and a broken link
loses the tail silently. Sparse integers or floats avoid the rewrite but drift: repeated insertions
between neighbours converge on the precision limit, and the failure mode is a reorder that silently
does nothing. Renumbering is O(n) on a set of at most a few dozen siblings, which is nothing, and it
makes "the third lesson" mean exactly `Order == 2` forever.

**The domain owns the normalisation.** A caller supplies a desired sequence and gets back a
contiguous one; there is no way to construct a gap or a duplicate from outside the aggregate.

### 3. A lesson whose body is unpublished disappears from the course

Not refused — **cannot** be refused. Content has no way to ask Learning whether a body is used by a
published course, because modules do not read each other's tables and Content must not depend on
Learning (CLAUDE.md rule 10). So this one is settled by the architecture rather than by preference:
the unpublish succeeds, and the lesson drops out of the learner's view because
`ILessonContentReader` reports it with no blocks and a null `PublishedAt`.

What the product owes in exchange is a warning where the author can act on it: the CMS shows a
course's lessons with their body state, so "published course, three lessons, one of them dark" is
visible rather than something a learner discovers.

## Aggregate boundaries

* **`Course`** is an aggregate root owning its `CourseModule`s, which own their `Lesson`s.
  Reordering modules and lessons is one transaction against one root, which is exactly the operation
  a builder UI performs most.
* **`LearningPath`** is a separate root holding an **ordered list of course ids** — a reference, not
  a navigation. A course belongs to any number of paths ("Intro to Python" sits in several tracks),
  and making a path own its courses would put the same course inside two aggregates.
* **`Lesson` references its body by `ContentUnitId`**, never by navigation. That id crosses a module
  boundary and is resolved through `ILessonContentReader` (ADR-0008).

## Consequences

* Positive: a course is one consistency boundary, so the drag-and-drop reorder that dominates
  authoring is a single atomic save.
* Positive: `Order` is trustworthy — every read can rely on contiguity rather than defending against
  gaps.
* Trade-off: renumbering writes every sibling row on a reorder. Irrelevant at curriculum sizes, and
  the alternative trades that for a silent-failure mode.
* Trade-off: a published course can contain a lesson a learner cannot see. That is a deliberate
  authoring affordance, and it obliges the CMS to surface body state per lesson — without that, this
  decision is a trap rather than a feature.
* Obligates: the public course read must filter unpublished lessons out; a test has to pin that, in
  the same way the article surfaces are pinned against draft leakage (CT-6).

## Not settled here

* **Prerequisites and related lessons** are modelled as ids but not enforced — nothing yet blocks a
  learner from starting a lesson whose prerequisite is incomplete. That is a progress concern and
  arrives with enrollment.
* **Exercises** wait for the Assessment module.
* **Search over curriculum metadata** — course titles, path descriptions, objectives — is still the
  open question ADR-0010 flagged, and it drags the transactional outbox with it.

## References

[ADR-0007](0007-unify-article-lesson.md); [ADR-0008](0008-cross-module-contracts-in-platform.md);
[ADR-0012](0012-lesson-bodies-live-in-content.md); [CONTENT_MODEL.md](../CONTENT_MODEL.md);
[MODULES.md](../MODULES.md) → Learning.
