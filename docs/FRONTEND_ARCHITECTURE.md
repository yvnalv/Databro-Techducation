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
* The API base URL is **two values, not one**: `public.apiBaseUrl` for the browser and a server-only
  `apiInternalBaseUrl` for SSR/prerender. They differ whenever the Nuxt server and the browser sit on
  different networks — a containerised run being the obvious case, where `localhost` inside the site
  container is the site container itself. `useApiClient` picks by `import.meta.server`, and the
  internal value falls back to the public one when unset.

## 7. Internationalization

* UI chrome via `@nuxtjs/i18n`; `en` and `id` dictionaries stay structurally identical (same keys both
  sides). No hardcoded user-facing strings.
* Strategy is `prefix_except_default`: English keeps clean canonical URLs, `/id/*` gets its own
  indexable namespace, and `hreflang` alternates link the two as translations rather than duplicates.
* Browser-language detection **never redirects** (`alwaysRedirect: false`, cookie only). A crawler must
  receive the same HTML for a URL on every request.
* Localized paths come from `localePath(path, locale)`, so the URL strategy is defined once in
  `nuxt.config` and never duplicated in SEO code.
* Article **bodies** are per-locale Content Units (see [CONTENT_MODEL.md](CONTENT_MODEL.md)), separate
  from chrome i18n.

## 8. SEO responsibilities (site)

* Per-page canonical, meta, OpenGraph/Twitter tags, and JSON-LD (`Article`) — implemented in the
  `useArticleSeo` composable so every content page emits the full surface by construction.
* An author-set `seo.canonicalUrl` wins; otherwise the site's own localized URL is canonical.
* Consumes `sitemap.xml`, `robots.txt`, RSS from the backend/platform.
* Honors 301 redirects from the `redirects` table on changed slugs.
* **Status codes are part of SEO.** A missing or unpublished slug must return a real `404`, never a
  soft 404 (200 with an error body) and never a `503` — a 503 tells crawlers "retry later" and keeps a
  dead URL indexed. `useAsyncData` re-wraps handler errors, so `toNuxtError` is applied *inside* the
  handler where the original status is still intact.
* **Premium articles stay fully indexable** (rule 9). Metadata, canonical and preview are public; the
  gated region is declared to search engines via JSON-LD `isAccessibleForFree: false` plus
  `hasPart.cssSelector`, which is the documented way to paywall without looking like cloaking.
* See [SEO.md](SEO.md).

## 9. Build & deploy

* One CI pipeline builds affected workspaces (pnpm filtering).
* `site` and `app` deploy independently (separate Nginx vhosts / DO targets) but from one repo.
* Shared packages are versioned internally (workspace protocol), not published externally.

## 10. Conventions

* TypeScript strict mode across all workspaces.
* Components are presentational where possible; data-fetching lives in composables/pages.
* Both apps use Nuxt 4 with `srcDir` at the app root (`apps/site/pages`, not `apps/site/app/pages`).
  This is Nuxt's supported fallback when no `app/` directory exists, and it avoids paths like
  `apps/app/app/pages`. Chosen deliberately — keep it consistent across both apps.
* Each app needs a `tsconfig.json` extending `./.nuxt/tsconfig.json`, or `nuxt typecheck` silently
  fails to find a config and never checks the app.
* An app that renders content blocks must add `../../packages/ui/src/**/*.{vue,ts}` to its Tailwind
  `content` globs, or the renderer's classes are purged from the production build.
* Pinned for Nuxt 4 compatibility: Tailwind **v3** (`@nuxtjs/tailwindcss@6` does not support v4's
  separate PostCSS plugin) and Pinia **v3** / `@pinia/nuxt@0.11` (0.9 + Pinia 2.3 crash Nuxt 4.5's
  payload serializer while rendering the error page).
* No business logic in the frontend that belongs in the backend domain (e.g. entitlement decisions are
  server-authoritative; the client only reflects them).
