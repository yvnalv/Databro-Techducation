# DataBro — Architecture

## 1. Architectural style

DataBro is a **Modular Monolith** built with **Clean Architecture** inside each module. One deployable
backend, internally partitioned into independent modules that could be extracted into services later
if scale demands it. See [ADR-0001](DECISIONS.md).

Why not microservices now: a solo, long-term project cannot afford the operational overhead
(distributed transactions, network failure modes, multi-repo deploys) of microservices while the
product surface is still being discovered. The modular monolith gives us clean boundaries without the
distribution tax, and keeps extraction *mechanical* if it's ever justified.

## 2. Read-heavy shape

DataBro is fundamentally read-heavy (content delivery + SEO). This shapes the architecture more than
the write model does:

* Public content is rendered statically (SSG/ISR) and served via CDN.
* Read APIs are cached in Redis with explicit invalidation on publish.
* The write path (authoring/publishing) is comparatively low-volume and can be simpler/stricter.

## 3. Layered structure per module

```
Module/
├── Domain/          Entities, value objects, domain events, business rules. Depends on nothing.
├── Application/     Use cases (commands/queries), DTOs, validators, port interfaces (I…Repository,
│                    I…Provider). Depends only on Domain.
├── Infrastructure/  EF Core DbContext + mappings, repository implementations, external adapters
│                    (storage, email, LLM). Depends on Application + Domain.
└── Api/             Thin controllers / minimal endpoints, request/response contracts, mapping.
                     Depends on Application.
```

Dependency rule: dependencies point inward. Domain never references Infrastructure or Api. This is
enforced in CI by an architecture-fitness test (NetArchTest or equivalent).

## 4. Module boundaries

Modules in Phase 1: `Platform` (shared kernel), `Identity`, `Content`, `Media`, `Search`. Later:
`Learning`, `Assessment`, `Billing`, `AI`, `Playground`, `Community`, `Enterprise`, `Analytics`,
`Notification`. See [MODULES.md](MODULES.md).

Rules:

* A module owns its own tables under its own EF Core schema / table prefix.
* **No module reads or writes another module's tables.** Cross-module data is obtained via public
  application-service contracts or integration events.
* Shared cross-cutting concerns (audit fields, error envelope, caching abstractions, outbox, clock,
  current-user accessor) live in `Platform` and are referenced by all modules.

## 5. Inter-module communication

Two mechanisms:

1. **Application-service contracts** — a synchronous read/command interface one module implements for
   others to call in-process. The **interface is declared in `Platform.Abstractions`, not in the
   owning module** (ADR-0008): the architecture-fitness test forbids any `DataBro.Modules.X` →
   `DataBro.Modules.Y` reference, so a contract living inside the owning module could not be consumed
   without breaking the boundary it exists to protect. The owning module registers its implementation
   through DI; consumers never learn which module satisfies the contract.

   Implemented: `IUserDirectory` (Identity → any module needing a display name; used by Content for
   article bylines).

   Contract shape rules: batch-oriented, so list endpoints cannot degrade into N+1 lookups; and
   tolerant of partial results, so a missing referent degrades rather than failing the caller.
2. **Integration events** — a module publishes a domain-meaningful event (e.g. `ArticlePublished`)
   through an in-process mediator. Subscribers in other modules react (e.g. `Search` reindexes,
   `Notification` queues an email). *Not yet implemented — `IIntegrationEvent` is currently a marker
   interface with no dispatcher.*

Choosing between them: use a **contract** when the caller needs data now and correctness depends on it
being current (a byline on a page being rendered). Use an **event** when the effect may be eventually
consistent (reindexing, cache invalidation, email). A contract that is called on a hot cached read
path is a candidate for later replacement by an event-fed denormalized copy — see ADR-0008.

Reliability:

* Effects that may be eventually consistent (reindex, send email, warm cache) use a **transactional
  outbox**: the event is persisted in the same DB transaction as the state change, then dispatched by
  a background worker (Hangfire). This survives crashes and avoids dual-write inconsistency.
* Effects that must be atomic with the originating change happen inside the same transaction.

This design means a future extraction to services swaps the in-process mediator for a message broker
(RabbitMQ) without touching domain logic.

## 6. Caching strategy

* **CDN / ISR** for public content pages (the `site` app).
* **Redis** for hot read APIs (published article by slug, category/tag listings, sitemap fragments).
* **Invalidation on publish:** publishing or unpublishing a content unit emits `ArticlePublished` /
  `ArticleUnpublished`, which busts the relevant cache keys and triggers ISR revalidation.
* Never cache authenticated, user-specific responses in shared caches.

## 7. Background jobs

**Hangfire** handles: outbox dispatch, search (re)indexing, scheduled publishing, email sending, image
variant generation, sitemap regeneration. Jobs are idempotent and safe to retry.

## 8. Request lifecycle (write example: publish an article)

1. `POST /api/v1/articles/{id}/publish` hits a thin controller in `Content.Api`.
2. Controller sends `PublishArticleCommand` to the Application layer.
3. Application loads the `Article` aggregate, invokes domain logic (`article.Publish(now)`), which
   validates business rules and snapshots `draft_blocks` → `published_blocks`, sets a new version.
4. In one transaction: persist the article + write `ArticlePublished` to the outbox.
5. Hangfire dispatches the event → `Search` reindexes, cache is invalidated, sitemap regenerates.

## 9. Configuration & secrets

* `appsettings.json` + environment-specific overrides + environment variables.
* Secrets (DB, JWT signing key, OAuth client secrets, Spaces keys) are injected via environment, never
  committed. See [DEPLOYMENT.md](DEPLOYMENT.md).

## 10. Observability

* Structured logging (Serilog) with correlation ids.
* Health checks (`/health`) for DB, Redis, storage.
* Error tracking hook (e.g. Sentry) — provider-abstracted.

## 11. Frontend architecture

Two Nuxt apps in a pnpm monorepo. Full detail in [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md).

## 12. Extraction readiness

A module is "extraction-ready" when: it owns its schema, exposes only contracts/events across its
boundary, has no direct DB coupling to other modules, and its integration events already model the
cross-boundary interactions. We keep every module at this bar from day one.
