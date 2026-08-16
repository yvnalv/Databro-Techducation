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
| title | text | |
| summary | text | |
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

**article_versions** (id, article_id, version, blocks jsonb, title, summary, seo jsonb, created_at,
created_by) — immutable history; one row per published/saved version.

**categories** (id, parent_id null, name, slug unique, description, order). Hierarchical.
**tags** (id, name, slug unique).
**article_tags** (article_id, tag_id) — join.
**redirects** (id, from_path, to_path, status_code default 301, reason, + audit) — populated when a
slug changes or content moves (CT-3). `from_path` carries a **filtered** unique index
(`WHERE is_deleted = false`) so a path redirected away, freed, then moved again does not collide with
the tombstone row. Chains are collapsed on write, so a live redirect always points at a real page.

### media

**media_assets** (id, storage_key, url, mime_type, byte_size, width, height, alt_text, checksum,
uploaded_by, + audit).
**media_variants** (id, media_asset_id, variant, width, height, url) — responsive sizes.

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

### platform

**outbox_messages** (id, occurred_at, type, payload jsonb, processed_at null, attempts, error null).

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

* **Learning (P2):** `learning_paths`, `courses`, `course_modules`, `lessons` (lesson → content unit
  ref), `enrollments`, `lesson_progress`, `bookmarks`.
* **Assessment (P2):** `quizzes`, `questions`, `quiz_attempts`.
* **Billing (P3):** `plans`, `subscriptions`, `invoices`, `entitlements`.
* **AI (P3):** `embeddings` (pgvector), `ai_conversations`.
* **Enterprise (P4):** `organizations`, `org_members`, `seats`, `cohorts` — the only place org-scoping
  columns appear.
