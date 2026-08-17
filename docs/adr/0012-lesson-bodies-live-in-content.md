# ADR-0012 — Where a Lesson's body lives

Status: **Proposed**
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

*(Pending — this ADR is Proposed. Recommendation below.)*

**Recommended: option B.** ADR-0007's purpose is that the hard parts are written once, and a shared
base class delivers exactly that — the block model, version history, draft/publish transition and
reading-time derivation are one implementation either way. What option B additionally buys is that
the most likely Phase 2 defect, a lesson surfacing where only articles belong, becomes impossible by
construction instead of something three tests have to keep catching. Given that this project has
already shipped one leak of precisely that shape (CT-6: a draft title reaching the public page,
listings, sitemap, feed *and* search, because several read paths each needed the same predicate),
buying that out is worth a second table.

The cross-table slug check is the one thing option B adds that A gets for free, and it is a single
guard in one service rather than a predicate repeated across every read path.

## Consequences (if B is accepted)

* Positive: no article surface can serve a lesson.
* Positive: no rename, no change to `/api/v1/articles`.
* Trade-off: `Content.Domain` grows a shared base class, and getting that boundary right is the
  design work of the first Phase 2 slice.
* Obligates: slug uniqueness checked across both tables before publish; a `IContentUnitReader`
  contract in Platform (ADR-0008 pattern) so Learning can read a body without touching Content's
  tables.

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
