# DataBro — Frontend Architecture

Two Nuxt 4 applications in a single **pnpm workspace monorepo**, sharing a design system, API client,
and types. See [ADR-0005](DECISIONS.md).

## 1. Layout

```
frontend/
├── pnpm-workspace.yaml
├── apps/
│   ├── site/        Public content — SEO + cache critical
│   └── app/         Authenticated learner app
└── packages/
    ├── ui/          Design system (components, Tailwind preset, content-block renderers)
    ├── api-client/  Typed client for /api/v1 (generated/maintained from API contracts)
    └── types/       Shared TS types (DTOs, block schema)
```

## 2. Why two apps (and the guardrail)

The public content surface and the authenticated app have different rendering needs, caching profiles,
and threat models, so they are separated. The cost for a solo dev is duplication — mitigated by the
monorepo: **all shared UI, auth handling, API access, and types live in `packages/*`. Nothing that
both apps use is copied.** See [ADR-0005](DECISIONS.md).

## 3. `site` — public content app

* **Purpose:** everything that must be indexed and cached — all article pages, category/tag pages,
  homepage, topic landing pages, author pages, search results, marketing.
* **Rendering:** **SSG/ISR** (static generation + incremental revalidation). Pages are static-fast and
  crawlable; revalidation is triggered by content publish events (webhook/ISR).
* **Data:** reads the public read API (cached in Redis + CDN). No secrets.
* **Auth-aware, not auth-only:** `site` renders logged-out and logged-in states. A **premium** article
  renders its full SEO metadata and a preview/teaser publicly (for indexing + conversion) and gates the
  full body behind auth/entitlement. `site` is not "logged-out only."

## 4. `app` — authenticated learner app

* **Purpose:** dynamic, user-specific surfaces — dashboard, progress, bookmarks, account/billing
  (P3), the CMS authoring UI (internal roles), and the Playground (P3).
* **Rendering:** SSR/SPA as appropriate; **not** SEO-optimized (behind auth).
* **Data:** authenticated API with the user's JWT; nothing user-specific is shared-cached.

> CMS authoring lives in `app` (behind Author/Editor/Admin roles). The **content-block renderer is
> shared** via `packages/ui`, so the CMS preview and the public `site` render identically — no drift.

## 5. Shared packages

* **`ui`** — component library + Tailwind preset (single source of design tokens) + the content-block
  renderer registry (one renderer used by both apps). WCAG 2.1 AA target.
* **`api-client`** — typed wrapper over `/api/v1`, handling the response envelope, auth token refresh,
  and error normalization.
* **`types`** — DTOs and the content-block schema types, kept in lockstep with backend contracts.

## 6. State & data

* **Pinia** for client state.
* Server data via Nuxt's data-fetching (`useAsyncData`/`useFetch`) hitting the read API; caching keys
  aligned with backend cache/ISR invalidation.

## 7. Internationalization

* UI chrome via an i18n layer; `en` and `id` dictionaries stay structurally identical (same keys both
  sides). No hardcoded user-facing strings.
* Article **bodies** are per-locale Content Units (see [CONTENT_MODEL.md](CONTENT_MODEL.md)), separate
  from chrome i18n.

## 8. SEO responsibilities (site)

* Per-page canonical, meta, OpenGraph/Twitter tags, and JSON-LD (`Article`).
* Consumes `sitemap.xml`, `robots.txt`, RSS from the backend/platform.
* Honors 301 redirects from the `redirects` table on changed slugs.
* See [SEO.md](SEO.md).

## 9. Build & deploy

* One CI pipeline builds affected workspaces (pnpm filtering).
* `site` and `app` deploy independently (separate Nginx vhosts / DO targets) but from one repo.
* Shared packages are versioned internally (workspace protocol), not published externally.

## 10. Conventions

* TypeScript strict mode across all workspaces.
* Components are presentational where possible; data-fetching lives in composables/pages.
* No business logic in the frontend that belongs in the backend domain (e.g. entitlement decisions are
  server-authoritative; the client only reflects them).
