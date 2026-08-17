# DataBro — Modules

Catalog of modules, their responsibilities, boundaries, and the events/contracts they expose. Each
module follows Clean Architecture (Domain / Application / Infrastructure / Api) and owns its own EF
Core schema.

Legend: **P1** = Phase 1, **P2** = Phase 2, etc.

---

## Platform (P1) — shared kernel

Cross-cutting concerns shared by all modules. Deliberately thin; contains no business domain.

* Standard audit fields and base entity/aggregate types.
* Result / error envelope and problem-details mapping.
* `ICurrentUser`, `IClock`, `IUnitOfWork` abstractions.
* Caching abstraction (Redis) and cache-key helpers.
* Transactional outbox scaffolding and the in-process event mediator.
* Base EF Core conventions (GUID keys, soft delete, snake_case mapping).

Owns: `outbox_messages`, shared conventions. Exposes: base abstractions. Emits: nothing.

---

## Identity (P1)

Authentication, authorization, user profiles.

* Registration + email verification; password login; Google/GitHub social login.
* JWT access + refresh tokens; password reset.
* RBAC: roles (Reader, Author, Editor, Admin) and permissions.
* User profile (display name, bio, avatar) — also serves as author byline source.

Owns: `users`, `roles`, `user_roles`, `permissions`, `role_permissions`, `refresh_tokens`,
`external_logins`, `email_verifications`.

Exposes (contracts): `IUserDirectory` — ids → `UserSummary(Id, DisplayName, AvatarUrl)`, batched.
Declared in `Platform.Abstractions` and implemented here (ADR-0008); `ICurrentUser` (also in
`Platform`) is implemented by `HttpCurrentUser` over the JWT.

Emits: `UserRegistered`, `UserEmailVerified`.

---

## Content (P1) — the CMS (core domain)

The heart of Phase 1. Authoring, versioning, publishing of Content units (Articles now; Lessons reuse
this in P2). See [CONTENT_MODEL.md](CONTENT_MODEL.md).

* `Article` aggregate composed of typed **content blocks** stored as JSONB.
* Draft / published version snapshots; version history; scheduling.
* Taxonomy: hierarchical **Categories**, flat **Tags**.
* SEO metadata per unit (slug, meta, canonical, OG, JSON-LD) and `visibility` (Public/Premium).
* Slug uniqueness and immutability-after-publish; 301 redirect records.

Owns: `articles`, `article_versions`, `lesson_contents`, `lesson_content_versions`, `categories`,
`tags`, `article_tags`, `redirects`.

**Content owns lesson bodies as well as articles** ([ADR-0012](adr/0012-lesson-bodies-live-in-content.md)).
Both are `ContentUnit`s sharing one engine — blocks, versioning, draft/publish — in separate tables,
so no query over articles can return a lesson. Learning owns the curriculum around them.

Consumes (contracts): `IUserDirectory` (author byline), `IMediaDirectory` (image blocks and
`og:image`).

Exposes (contract): **`ILessonContentReader`** in Platform — batch id → published lesson body, for
Learning. Deliberately narrower than "any content unit": a reader that resolved article ids too
would let Learning attach an article as a lesson and undo the separation ADR-0012 exists to enforce.

Emits: `ArticlePublished`, `ArticleUnpublished`, `LessonContentPublished`,
`LessonContentUnpublished`. Lesson bodies raise their own events rather than reusing the article
ones — a subscriber reacting to `ArticlePublished` would otherwise act on something with no public
article URL.

---

## Media (P1) — built

Asset upload and delivery ([ADR-0011](adr/0011-media-storage-and-image-processing.md)).

* Uploads images to S3-compatible storage: MinIO in development, DigitalOcean Spaces in production,
  through one adapter behind `IMediaStorage`.
* **Re-encodes every image before storing it.** The stored bytes are always ours, which closes the
  polyglot-file class of attack by construction and strips EXIF (GPS coordinates) as a side effect.
  Format comes from magic bytes; the `Content-Type` header and the file extension are never trusted.
* Storage keys are generated (`media/{yyyy}/{MM}/{assetId}/{variant}.{ext}`), never derived from the
  client's filename.
* Generates responsive variants (640/960/1280/1920, never upscaling) in a **Hangfire job**, so the
  upload request returns immediately. An asset is usable at full size while `Pending`.
* Stores metadata: dimensions, alt text, and a checksum of the **stored** bytes.

Owns: `media_assets`, `media_variants`.

Exposes (contract): **`IMediaDirectory`** in Platform — batch id → URL + variants + alt. Named for
symmetry with `IUserDirectory` and batch-shaped for the same reason (ADR-0008): one article can
carry a dozen figures, and a per-item lookup would be an N+1 on the cached public read path.

Consumed by: **Content**, which resolves the media ids in its image blocks and `og:image` and ships
the resolved map with the article DTO.

Not built yet: no `MediaUploaded` event (nothing consumes one, and the outbox does not exist), no
orphan sweep — deleting an asset is a soft delete that deliberately leaves the stored objects, since
a published article may still reference them.

---

## Search (reserved — not built)

> **Status: four empty marker projects.** Phase 1 full-text search is implemented **inside Content**
> ([ADR-0010](adr/0010-fts-lives-in-content.md)), over a generated `tsvector` column on `articles`.
> The design below is the *target* for the event-fed OpenSearch index; it depends on a transactional
> outbox that does not exist yet, and building it without one would ship a search index that can
> silently disagree with the catalogue.

Planned: indexing and query over published content across modules.

* Rebuilds its index reactively from content events; never reads Content's tables directly for writes
  beyond an initial backfill contract.

Will own: `search_documents` (denormalized, module-owned).

Will consume: `ArticlePublished` / `ArticleUnpublished` / `ArticleUpdated` / `ArticleDeleted` events.

Will expose (contract): `ISearchService`. Until then the stable seam is the HTTP endpoint
`GET /api/v1/search`, which does not change when the engine does.

**Trigger to build this:** the outbox landing, or Learning (Phase 2) introducing a second searchable
aggregate — a union across modules cannot live inside one of them.

---

## Notification (P1-lite / P2)

Outbound messaging. Minimal in P1 (transactional email: verification, password reset) via a
provider-abstracted `IEmailSender`. Expands in P2 (newsletter, digests).

Owns: `notifications`, `email_log`.

---

## Learning (P2) — domain built

Structured learning over the Content engine ([ADR-0013](adr/0013-learning-curriculum-invariants.md)).

* `LearningPath` → `Course` → `CourseModule` → `Lesson`. A `Lesson` references a Content unit by id
  and adds learning metadata (objectives, prerequisites, estimated time, difficulty, ordering).
* **`Course` is the aggregate root**, owning its modules and their lessons — because reordering is
  the operation an authoring UI performs constantly, and one root makes a whole rearrangement a
  single atomic save.
* **`LearningPath` is a separate root** holding an ordered list of course *ids*. A course belongs to
  several paths, so a path owning its courses would put the same course in two aggregates.
* **A course publishes independently of its lessons.** A published course shows only its published
  lessons; requiring every lesson first would make a large curriculum unpublishable until the last
  one was written. A lesson whose body is unpublished simply disappears — Content cannot refuse the
  unpublish, because it must not depend on Learning.
* Ordering is a contiguous integer normalised on every change, so `Order` is always `0..n-1`.

Owns: `learning_paths`, `path_courses`, `courses`, `course_modules`, `lessons` *(persistence not yet
written — the domain and its tests are).*

Consumes (contract): **`ILessonContentReader`** from Content, for lesson bodies.

Emits: `CoursePublished`, `CourseUnpublished`. `LessonCompleted`, `CourseCompleted` and `Enrolled`
arrive with progress.

---

## Assessment (P2)

Quizzes and (later) projects.

* Quiz definitions bound to lessons; question types; attempts; scoring.
* P3: project submissions and review.

---

## Billing (P3)

Subscriptions and entitlements.

* Provider-abstracted (`IPaymentProvider`, e.g. Stripe): plans, checkout, webhooks, invoices.
* Entitlements engine that resolves whether a user may access `Premium` content — activates the
  `visibility` gate reserved since P1.

Exposes (contract): `IEntitlementService`.

---

## AI (P3)

* AI Tutor, code review, quiz/exercise generation, recommendations, semantic search.
* All model access behind `ILlmProvider` / `IEmbeddingProvider`. Embeddings stored via **pgvector**.

---

## Playground (P3)

* Sandboxed Python/SQL execution. Execution strategy (client WASM vs. server sandbox) decided by a
  dedicated ADR at module start.

---

## Community (P4), Enterprise (P4), Analytics (P4)

* **Community:** discussions, comments, Q&A.
* **Enterprise:** `Organization` aggregate — seats, cohorts, private progress dashboards over shared
  content. This is where any org-scoping enters the model (never before).
* **Analytics:** learning + content analytics, funnels.

---

## Boundary rules (all modules)

* No cross-module table access — ever.
* Cross-module reads via published contracts; cross-module reactions via integration events.
* **Contract interfaces are declared in `Platform.Abstractions`, not in the owning module** — a module
  cannot reference another module's assembly, so a contract defined inside its owner would be
  unconsumable (ADR-0008). The owner supplies the implementation via DI.
* Contracts are batch-shaped and tolerate partial results: a missing referent must degrade, not throw.
* Each module's events are its public API; treat them as versioned contracts.
