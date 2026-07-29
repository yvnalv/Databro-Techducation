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
**redirects** (id, from_path unique, to_path, status_code default 301, reason, created_at) — populated
when a slug changes or content moves.

### media

**media_assets** (id, storage_key, url, mime_type, byte_size, width, height, alt_text, checksum,
uploaded_by, + audit).
**media_variants** (id, media_asset_id, variant, width, height, url) — responsive sizes.

### search

**search_documents** (id, article_id, locale, title, summary, body_text, tags text[], tsv tsvector,
visibility, published_at). Denormalized, owned by Search, populated from content events.
Index: GIN(`tsv`), GIN(`tags`), trigram index on `title` for fuzzy fallback.

### platform

**outbox_messages** (id, occurred_at, type, payload jsonb, processed_at null, attempts, error null).

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
