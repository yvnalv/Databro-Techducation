# DataBro — Database Design

Database: **PostgreSQL** (Npgsql + EF Core). Each module owns its own schema; no cross-schema foreign
keys across module boundaries (references across modules are by id + contract, not FK).

## Conventions

* **Primary keys:** GUID (UUID v7 preferred for index locality) for business entities.
* **Naming:** snake_case tables and columns; plural table names (`articles`, `content` mapped by EF).
* **Schemas per module:** `identity`, `content`, `media`, `search`, `platform` (extended per phase).
* **Audit fields on every table:** `created_at`, `created_by`, `updated_at`, `updated_by`,
  `deleted_at`, `deleted_by`, `is_deleted`.
* **Soft delete:** business data is never physically deleted; `is_deleted` + global query filter.
* **Timestamps:** `timestamptz`, always UTC.
* **Money (P3+):** stored as minor units (integer) with an explicit currency code; never floats.
* **JSONB:** used for content blocks and flexible metadata; indexed with GIN where queried.

## Global query filters

* Soft delete filter (`is_deleted = false`) applied globally; bypass (`IgnoreQueryFilters`) is
  restricted to reviewed admin/maintenance paths only.
* No tenant filter — DataBro is B2C-first (see [ADR-0002](DECISIONS.md)).

---

## Phase 1 schema (essentials)

### identity

**users**
| column | type | notes |
|---|---|---|
| id | uuid PK | |
| email | citext unique | |
| email_verified | bool | |
| password_hash | text null | null for social-only accounts |
| display_name | text | |
| avatar_media_id | uuid null | references media asset by id (no cross-module FK) |
| bio | text null | |
| status | text | active / suspended |
| + audit fields | | |

**roles**, **permissions**, **role_permissions**, **user_roles** — standard RBAC join tables.

**refresh_tokens** (id, user_id, token_hash, expires_at, revoked_at, replaced_by).
**external_logins** (id, user_id, provider, provider_key). provider ∈ {google, github}.
**email_verifications** / **password_resets** (id, user_id, token_hash, expires_at, used_at).

### content

**articles** (aggregate root)
| column | type | notes |
|---|---|---|
| id | uuid PK | |
| slug | citext unique | immutable once published |
| title | text | the **draft** title — what an editor is working on |
| summary | text | the **draft** summary |
| published_title | text null | title as last published; null until first publish |
| published_summary | text null | summary as last published |
| status | text | draft / scheduled / published / unpublished / archived |
| visibility | text | public / premium (reserved; P1 = public) |
| locale | text | e.g. en, id |
| translation_group_id | uuid null | links locale variants |
| author_id | uuid | references identity.users by id |
| category_id | uuid null | references content.categories |
| cover_media_id | uuid null | references media asset by id |
| draft_blocks | jsonb | working copy (see CONTENT_MODEL) |
| published_blocks | jsonb null | snapshot at publish |
| current_version | int | |
| reading_time_minutes | int | derived |
| seo | jsonb | meta title/description, canonical, og, robots |
| published_at | timestamptz null | |
| scheduled_for | timestamptz null | |
| + audit fields | | |

Indexes: unique(`slug`), GIN on `draft_blocks`/`published_blocks` where queried, btree(`status`,
`published_at`), (`category_id`), (`translation_group_id`).

**lesson_contents** — a lesson's renderable body ([ADR-0012](adr/0012-lesson-bodies-live-in-content.md)).
Exactly the engine's columns and nothing else: no author, category, tags, SEO or locale, and **no
search vector**. Its own table beside `articles` so no query over articles can return one. Learning's
`Lesson` references it by id.

**lesson_content_versions** — the same shape as `article_versions`. Separate because two owner tables
cannot share one foreign-key column, which is also why `ContentVersion` is an abstract type with one
concrete class per unit type.

> **Slug uniqueness spans both tables** and is enforced by `IContentSlugRegistry` on the write path,
> because a unique index cannot. That is the single cost of separate tables, deliberately paid in one
> guard rather than as a `kind = Article` predicate repeated on every public read path — where
> forgetting one is silent and public.

> **Constraint naming wart.** `articles` and `article_versions` carry PascalCase primary keys
> (`PK_articles`) while everything else is snake_case. EFCore.NamingConventions cannot derive a table
> name for a key declared on the abstract `ContentUnit`, which has no table of its own. Cosmetic;
> renaming them in place was the only safe migration, since PostgreSQL refuses to drop a primary key
> that foreign keys depend on.

**article_versions** (id, content_unit_id, version, blocks jsonb, title, summary, + audit) — immutable
history, one row **per publish** (not per save). Append-only: restoring copies a row into the draft
and never rewrites one, so publishing a restored version appends a *new* version rather than
reverting the sequence (CT-8).

> **`published_title` / `published_summary` are the reason these two exist.** `title` and `summary`
> were originally single columns shared by the draft and the published copy, exactly as if
> `published_blocks` had never been split from `draft_blocks`. Editing a published article's draft
> title therefore changed the live page, the listings, the sitemap, the RSS feed and the search index
> the moment it was saved — a half-written headline going public as it was typed. The body was
> protected from day one; these two never were. Added by migration `AddPublishedTitleAndSummary`,
> which backfills `published_title = title` for every article with a `published_at` — correct
> precisely because, until then, the draft title *was* the published title.

**categories** (id, parent_id null, name, slug unique, description, order). Hierarchical.
**tags** (id, name, slug unique).
**article_tags** (article_id, tag_id) — join.
**redirects** (id, from_path, to_path, status_code default 301, reason, + audit) — populated when a
slug changes or content moves (CT-3). `from_path` carries a **filtered** unique index
(`WHERE is_deleted = false`) so a path redirected away, freed, then moved again does not collide with
the tombstone row. Chains are collapsed on write, so a live redirect always points at a real page.

### media

**media_assets** (id, storage_key, file_name, mime_type, byte_size, width, height, alt_text,
checksum, processing_status, processing_error, uploaded_by, + audit).

* `storage_key` — generated, never derived from the uploader's filename (ADR-0011). Unique, filtered
  on `is_deleted = false`.
* `file_name` — the uploader's name, kept for display in the picker only.
* `checksum` — SHA-256 of the **stored** bytes, not the uploaded ones: after re-encoding, the upload
  is no longer what we hold, so hashing the input would describe a file that does not exist.
  Indexed but **not unique** — two articles legitimately use the same image.
* `processing_status` — `Pending` / `Ready` / `Failed`. Variants arrive from a background job, so an
  asset is usable at full size before it is responsive. `Failed` still serves the original: a failed
  resize must not cost an author their upload.
* No `url` column: a URL is composed from the key and current storage configuration. Persisting one
  would go stale the moment the CDN or bucket moves.

**media_variants** (id, media_asset_id, name, storage_key, width, height, byte_size, + audit) —
responsive sizes. Unique on `(media_asset_id, name)`, **filtered on `is_deleted = false`**: deletes
are soft, so an unfiltered unique index would make a regenerated variant collide with its own
tombstone.

### search

> **Not created.** Phase 1 search lives on `content.articles` instead
> ([ADR-0010](adr/0010-fts-lives-in-content.md)) — see the two columns below. The schema described
> here arrives with the event-fed index.

**search_documents** (id, article_id, locale, title, summary, body_text, tags text[], tsv tsvector,
visibility, published_at). Denormalized, owned by Search, populated from content events.
Index: GIN(`tsv`), GIN(`tags`), trigram index on `title` for fuzzy fallback.

### Full-text search columns on `content.articles`

**`search_text`** (`text`, default `''`) — the plain-text projection of the *published* blocks,
written by the domain on publish. Only published, because search returns published content; indexing
a draft would make unreleased text findable. Derived in C# (`ContentText`) because the body is typed
JSONB that SQL cannot flatten meaningfully.

**`search_vector`** (`tsvector GENERATED ALWAYS AS (…) STORED`) —

```
setweight(to_tsvector(<config>, coalesce(title,   '')), 'A') ||
setweight(to_tsvector(<config>, coalesce(summary, '')), 'B') ||
setweight(to_tsvector(<config>, coalesce(search_text, '')), 'C')
```

where `<config>` is `CASE WHEN locale = 'id' THEN 'indonesian' ELSE 'english' END::regconfig`.

* **Generated, not application-written.** PostgreSQL recomputes it on every write to the row, so it
  cannot fall out of step with the title, summary or body. There is no reindex job and no drift.
* Both branches are literal `regconfig` casts because only `to_tsvector(regconfig, text)` is
  `IMMUTABLE` — the one-argument form reads a session setting, which a generated column may not
  depend on.
* Weights A/B/C use PostgreSQL's default multipliers (1.0/0.4/0.2), so a title match outranks a
  passing mention deep in the body.

Index: GIN(`search_vector`). The `pg_trgm` extension is installed for the `word_similarity()` typo
fallback, but **no trigram index on `title`** — a `gin_trgm_ops` index answers the `<%` operator,
whose threshold comes from a session GUC (0.6) rather than the explicit 0.3 the fallback needs, so
the index could not serve the only query that would use it.

Backfill: `ContentInitializer` fills `search_text` for articles published before the column existed.
Idempotent and self-limiting — after the first run it selects nothing.

### learning

Curriculum structure. Lesson **bodies** are not here — they live in `content.lesson_contents` and are
reached through `ILessonContentReader` (ADR-0012).

**learning_paths** (id, slug unique, title, summary, status, difficulty, published_at).
**path_courses** (id, learning_path_id → learning_paths, course_id, "order").

**courses** (id, slug unique, title, summary, status, difficulty, published_at, search_vector).
**course_modules** (id, course_id → courses cascade, title, summary, "order").
**lessons** (id, course_module_id → course_modules cascade, content_unit_id, "order",
estimated_minutes, difficulty, objectives jsonb, prerequisite_lesson_ids jsonb).

`content_unit_id` has **no foreign key**: it crosses a module boundary, and a database constraint
there would couple the two schemas exactly as tightly as rule 10 forbids.

The `(parent, "order")` indexes are deliberately **not unique**, though contiguity is an invariant
(LN-3). EF rewrites sibling positions one UPDATE at a time, so an intermediate state legitimately
holds a duplicate; a deferrable constraint would fix that but cannot be partial, and this one must be
filtered on `is_deleted`. The invariant is enforced in the aggregate, which normalises after every
structural change.

`search_vector` on `courses` is a stored generated column (title A, summary B), the same pattern as
articles but with no locale `CASE` — a course has no locale column (ADR-0014).

#### Progress

**enrollments** (id, user_id, course_id, enrolled_at, completed_at null, last_lesson_id null,
last_accessed_at null).
**lesson_progress** (id, enrollment_id → enrollments cascade, lesson_id, completed_at null).

The platform's first write-heavy tables, and shaped for it:

* `user_id` and `course_id` carry no foreign keys — the first crosses a module boundary, and the
  second is left unconstrained because a course is authoring-owned while an enrollment is
  learner-owned, and a cascade between them is not a behaviour to discover at delete time.
* `ix_enrollments_user_course` is unique, filtered on `is_deleted = false`. Unlike the ordering
  indexes this one *can* be unique: nothing legitimately writes a second row, so it only ever fires
  on the race it exists to stop — two concurrent enrol clicks. The service catches the violation and
  returns the winner (LN-9). The filter lets an un-enrolled learner enrol again.
* `ix_lesson_progress_enrollment_lesson` is unique on the same reasoning (two devices, one lesson).
* `(user_id, last_accessed_at)` serves the dashboard.
* Progress rows are **sparse** — written when a learner first touches a lesson, never pre-seeded from
  the curriculum. Seeding would multiply every enrollment by its lesson count to record, almost
  entirely, that nothing has happened yet.
* `completed_at` on `enrollments` is **stored, not derived** (LN-6). Percent complete is the
  opposite: derived at read time, never stored.

### assessment

**quizzes** (id, lesson_id, title, passing_score, status, published_at) — `lesson_id` is unique,
filtered on `is_deleted = false`: one quiz per lesson. No foreign key to `learning.lessons`; it
crosses a module boundary.

**questions** (id, quiz_id → quizzes cascade, prompt, type, "order", points, explanation).
**choices** (id, question_id → questions cascade, text, is_correct, "order").

`is_correct` is the answer key, stored plainly. Nothing hides it at the column level — the guarantee
is that no learner-facing DTO can carry it, enforced by having two DTO types rather than one with a
nullable field ([ADR-0018](adr/0018-assessment-scoring-and-the-answer-key.md)).

**quiz_attempts** (id, quiz_id, user_id, started_at, submitted_at null, score, total_points, passed).
**attempt_answers** (id, attempt_id → quiz_attempts cascade, question_id, selected_choice_ids jsonb,
points_earned).

`passed` and `total_points` are **stored**, not derived, for the reason LN-6 stores course
completion: an author who later raises the passing score must not retroactively fail people who
passed under the old one.

The `(user_id, quiz_id, started_at)` index is deliberately **not unique** — retakes are the point.

### Outbox — one table per module, not one shared

**{schema}.outbox_messages** (id, type, payload jsonb, occurred_at, processed_at null, attempts,
next_attempt_at null, error null, is_dead_lettered) — currently in `learning`
([ADR-0017](adr/0017-transactional-outbox.md)).

Per-module rather than a single `platform.outbox_messages`, because the row must be written by the
**same `DbContext`** as the state change or it is not in the same transaction. Two contexts mapping
one physical table would also leave "whose migration creates it" unanswerable. Per-module keeps rule
10 intact and makes extraction mechanical.

`ix_outbox_messages_pending` is filtered on `processed_at IS NULL AND is_dead_lettered = false`, so
it stays the size of the backlog rather than of all history — processed rows are kept for audit and
would otherwise dominate it. Retention is owed.

### platform

### hangfire

Owned and migrated by **Hangfire.PostgreSql**, not EF Core — its tables (jobs, servers, locks,
recurring jobs) live in a dedicated `hangfire` schema created automatically at host startup. Backs the
scheduled-publish sweep (CT-7). Do not write EF migrations against it.

---

## Versioning model (content)

* `draft_blocks` is the mutable working copy edited in the CMS.
* On **publish**, `draft_blocks` is snapshotted into `published_blocks`, a new `article_versions` row
  is written, `current_version` increments, and `published_at`/`status` update — all in one
  transaction, with `ArticlePublished` written to the outbox.
* Public reads only ever serve `published_blocks`. Drafts are visible only to authorized authors/editors.

See [CONTENT_MODEL.md](CONTENT_MODEL.md) for block structure.

---

## Future (reference)

* ~~**Learning (P2):** `learning_paths`, `courses`, `course_modules`, `lessons`, `enrollments`,
  `lesson_progress`~~ — **built**; see the `learning` schema above. `bookmarks` is still to come.
* **Assessment (P2):** `quizzes`, `questions`, `quiz_attempts`.
* **Billing (P3):** `plans`, `subscriptions`, `invoices`, `entitlements`.
* **AI (P3):** `embeddings` (pgvector), `ai_conversations`.
* **Enterprise (P4):** `organizations`, `org_members`, `seats`, `cohorts` — the only place org-scoping
  columns appear.
