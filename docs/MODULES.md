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

Owns: `articles`, `article_versions`, `categories`, `tags`, `article_tags`, `redirects` (`redirects`
not yet implemented — see the slug-immutability note in [CONTENT_MODEL.md](CONTENT_MODEL.md) §5b).

Consumes (contracts): `IUserDirectory` (author byline — implemented), `IMediaReadService` (asset URLs
— not yet; image blocks render a placeholder until Media exists).

Emits: `ArticlePublished`, `ArticleUnpublished`, `ArticleUpdated`, `ArticleDeleted`.

---

## Media (P1)

Asset upload and delivery.

* Upload images to DigitalOcean Spaces (S3-compatible).
* Generate responsive variants; store metadata (dimensions, alt text, checksum).
* Serve stable URLs referenced by content blocks.

Owns: `media_assets`, `media_variants`.

Exposes (contract): `IMediaReadService` (id → URL + variants + alt).

Emits: `MediaUploaded`.

---

## Search (P1)

Indexing and query over published content.

* PostgreSQL full-text search (weighted tsvector over title/summary/body/tags; trigram fuzzy fallback).
* Rebuilds its index reactively from content events; never reads Content's tables directly for writes
  beyond an initial backfill contract.

Owns: `search_documents` (denormalized, module-owned).

Consumes: `ArticlePublished` / `ArticleUnpublished` / `ArticleUpdated` / `ArticleDeleted` events.

Exposes (contract): `ISearchService`.

---

## Notification (P1-lite / P2)

Outbound messaging. Minimal in P1 (transactional email: verification, password reset) via a
provider-abstracted `IEmailSender`. Expands in P2 (newsletter, digests).

Owns: `notifications`, `email_log`.

---

## Learning (P2)

Structured learning over the Content engine.

* `LearningPath` → `Course` → `CourseModule` → `Lesson`. A `Lesson` references a Content unit and adds
  learning metadata (objectives, prerequisites, estimated time, difficulty, ordering).
* Enrollment; per-user progress, completion, resume, streaks; bookmarks.

Emits: `LessonCompleted`, `CourseCompleted`, `Enrolled`.

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
