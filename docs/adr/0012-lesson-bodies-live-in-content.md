# ADR-0012 — Where a Lesson's body lives

Status: **Accepted**
Date: 2026-08-16
Deciders: Project owner

## Context

Phase 2 introduces `LearningPath → Course → CourseModule → Lesson`. Three existing commitments
constrain how a Lesson is stored, and they do not immediately agree:

* **[ADR-0007](0007-unify-article-lesson.md)** — an Article and a Lesson are the same primitive, and
  the block/versioning/publish engine is built **once**.
* **[MODULES.md](../MODULES.md)** — Learning owns `Lesson`, and "a `Lesson` **references** a Content
  unit and adds learning metadata".
* **CLAUDE.md rule 10** — a module never reads or writes another module's tables.

Put together: Learning owns the *curriculum* (structure, objectives, ordering, difficulty), and the
*body* it points at must live in Content. Content today has exactly one aggregate, `Article`, in a
table called `articles`. So Content needs somewhere to keep a body that is not an article, and the
question is what shape that takes.

This is worth an ADR because it is expensive to reverse: it decides the schema, the publish path,
and how many places can leak a lesson into a surface meant for articles.

## Options

### A. A `kind` discriminator on the existing table

`articles` gains `kind` (`Article` | `Lesson`). One aggregate, one versioning table, one publish
path, one slug/redirect system. `lessons.content_unit_id` points at a row in it.

* **For:** genuinely one engine — not two parallel ones. Cheapest to build. Slug uniqueness stays
  global across both, which is correct since both are URLs on one origin.
* **Against:** every public article surface must filter `kind = Article` or a lesson appears in
  `/api/v1/articles`, the sitemap and the RSS feed as a standalone post. That is the **same bug
  class as the CT-6 draft leak** — a missing predicate on one of several read paths, invisible until
  someone notices a lesson in the feed. Roughly three places today (`ListPublishedAsync`,
  `SearchPublishedAsync`, the sitemap's catalogue helper), each needing a test.
* **Against:** a table named `articles` holding lessons is a naming lie that will outlive everyone's
  memory of this document.

### B. A sibling aggregate in Content, with its own table

Content gains a second aggregate for lesson bodies (`lesson_contents` + `lesson_content_versions`),
sharing blocks, versioning and publish with `Article` through a common base class in
`Content.Domain`.

* **For:** a lesson **cannot** leak into an article query, because it is not in that table. The leak
  class from option A does not exist rather than being tested for.
* **For:** no naming lie, and no rename.
* **Against:** two history tables and two publish entry points. The *code* is shared, the *schema*
  is duplicated — which is ordinary per-aggregate practice, but it is not literally "one engine".
* **Against:** slug uniqueness across articles and lessons needs a deliberate cross-table check, or
  two lessons and an article can claim the same URL.

### C. Rename `articles` → `content_units` and add `kind`

The most honest naming, and the shape CONTENT_MODEL.md describes.

* **Against:** 359 `Article` references across 38 backend files, plus `ArticleSummary`/`ArticleDto`
  and friends across three frontend packages — and `/api/v1/articles` is a **published contract**
  the site, the sitemap and the feed already depend on. That is a large, risky refactor bought for
  naming purity, before a single Lesson exists.

## Decision

**Option B.** ADR-0007's purpose is that the hard parts are written once, and a shared
base class delivers exactly that — the block model, version history, draft/publish transition and
reading-time derivation are one implementation either way. What option B additionally buys is that
the most likely Phase 2 defect, a lesson surfacing where only articles belong, becomes impossible by
construction instead of something three tests have to keep catching. Given that this project has
already shipped one leak of precisely that shape (CT-6: a draft title reaching the public page,
listings, sitemap, feed *and* search, because several read paths each needed the same predicate),
buying that out is worth a second table.

The cross-table slug check is the one thing option B adds that A gets for free, and it is a single
guard in one service rather than a predicate repeated across every read path.

## Consequences

* Positive: no article surface can serve a lesson.
* Positive: no rename, no change to `/api/v1/articles`.
* Trade-off: `Content.Domain` grows a shared base class, and getting that boundary right is the
  design work of the first Phase 2 slice.
* Obligates: slug uniqueness checked across both tables before publish; a `IContentUnitReader`
  contract in Platform (ADR-0008 pattern) so Learning can read a body without touching Content's
  tables.

## The seam, in detail

The base class is the whole decision, so it is specified here rather than discovered during the
refactor.

**`ContentUnit : AggregateRoot` (abstract) — the engine.** Everything that is true of any renderable,
versioned body:

| | |
|---|---|
| State | `Slug`, `Title`, `Summary`, `Status`, `PublishedAt`, `ScheduledFor` |
| Bodies | `DraftBlocks`, `PublishedBlocks` |
| Published snapshot (CT-6) | `PublishedTitle`, `PublishedSummary`, `SearchText` |
| Derived | `CurrentVersion`, `ReadingTimeMinutes` |
| History | `Versions` |
| Behaviour | `UpdateDraft`, `ChangeSlug`, `Schedule`, `CancelSchedule`, `Publish`, `Unpublish`, `RestoreVersion`, `RebuildSearchText` |

**`Article : ContentUnit`** keeps what is article-only: `Visibility`, `Locale`,
`TranslationGroupId`, `AuthorId`, `CategoryId`, `Seo`, its tag links, `SetCategory`, `SetTags`.

**`LessonContent : ContentUnit`** adds nothing at first — it *is* a body. Learning metadata
(objectives, prerequisites, difficulty, ordering) belongs to Learning's own `Lesson`, per MODULES.md.

Three details that are easy to get wrong and are decided here:

1. **Domain events.** `Publish()` currently raises `ArticlePublishedDomainEvent`. The base performs
   the state transition and calls a `protected abstract void OnPublished()` / `OnUnpublished()` hook;
   each derived type raises its own event. A base that raised an article event for a lesson would be
   a lie the outbox would later deliver.
2. **Version rows.** `ArticleVersion` becomes `ContentVersion` with a `ContentUnitId`, so the base
   can own the history list. This is a rename inside Content with no public-contract impact — the
   version endpoints return DTOs, not the entity.
3. **EF mapping.** Each concrete type maps to its own table via `UseTpcMappingStrategy()`. Not TPH
   (which would put both in one table and reintroduce exactly the leak option B exists to prevent)
   and not TPT (which would add a join to the hottest read path for no benefit).

**Slug uniqueness** moves to a small service in Content that checks both tables before publish — the
one thing option A got for free, deliberately paid for in a single place rather than as a predicate
repeated on every read path.

## Implementation order

1. **Pure refactor, no new behaviour**: extract `ContentUnit`, `ArticleVersion` → `ContentVersion`,
   with `Article` as the only derived type. 180 existing tests are the safety net; nothing else moves
   until they are green.
2. `LessonContent` + its table and the cross-table slug guard.
3. `IContentUnitReader` in Platform, so Learning can read a body without touching Content's tables.
4. Learning domain: `LearningPath → Course → CourseModule → Lesson`.
5. Learning API and the CMS course builder; then the public course/path pages.
6. Enrollment and progress. Assessment is its own slice after that.

## Open question this does not settle

**Search.** [ADR-0010](0010-fts-lives-in-content.md) put full-text search inside Content "until a
second module owns searchable content", naming Learning as the trigger. Under either option the
lesson *bodies* stay in Content, so the trigger fires only partly — but course titles, path
descriptions and learning objectives are Learning-owned and a learner will expect to find them.
That needs its own decision, and with it the **transactional outbox**, once the shape above is
settled.

## References

[ADR-0007](0007-unify-article-lesson.md); [ADR-0008](0008-cross-module-contracts-in-platform.md);
[ADR-0010](0010-fts-lives-in-content.md); [CONTENT_MODEL.md](../CONTENT_MODEL.md);
[MODULES.md](../MODULES.md).
