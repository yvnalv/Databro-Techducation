# DataBro — Frontend Architecture

Two Nuxt 4 applications in a single **pnpm workspace monorepo**, sharing a design system, API client,
and types. See [ADR-0005](DECISIONS.md).

## 1. Layout

```
frontend/
├── pnpm-workspace.yaml
├── apps/
│   ├── site/        Public content — SEO + cache critical
│   └── app/         Authenticated app — learner surfaces at /, the CMS at /studio
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

**The boundary is indexability**, not audience and not feature ([ADR-0015](adr/0015-authenticated-app-hosts-both-audiences.md)):

| Surface | Indexable | App |
|---|---|---|
| Articles, course catalogue, course pages, lesson pages | yes | `site` |
| Learner dashboard, progress, playground | no | `app` |
| CMS — article/lesson editors, course builder, taxonomy | no | `app` |

A learner dashboard and the CMS have identical rendering needs — authenticated, dynamic, `noindex`,
client-heavy — so they share an app and are separated by route and role instead. Lesson *reading* is
content and belongs to `site`, gated body and all.

## 3. `site` — public content app

* **Purpose:** everything that must be indexed and cached — all article pages, category/tag pages,
  homepage, topic landing pages, author pages, search results, marketing.
* **Rendering:** **SSG/ISR** (static generation + incremental revalidation). Pages are static-fast and
  crawlable; revalidation is triggered by content publish events (webhook/ISR).
* **Data:** reads the public read API (cached in Redis + CDN). No secrets.
* **Auth-aware, not auth-only:** `site` renders logged-out and logged-in states. A **premium** article
  renders its full SEO metadata and a preview/teaser publicly (for indexing + conversion) and gates the
  full body behind auth/entitlement. `site` is not "logged-out only."

## 4. `app` — the authenticated app

Two audiences, one shell, one session ([ADR-0015](adr/0015-authenticated-app-hosts-both-audiences.md)).
Every editor is also a learner, and making them log in twice to be both would be an artefact of our
file layout rather than anything they asked for.

```
/           learner — dashboard and progress; the Playground (P3) joins it here
/studio     CMS — article and lesson editors, course builder, taxonomy
/login      shared
```

* **Rendering:** SSR/SPA as appropriate; **not** SEO-optimized. `X-Robots-Tag: noindex, nofollow` on
  every route.
* **Data:** authenticated API with the user's JWT; nothing user-specific is shared-cached.
* **Layouts:** `default.vue` is the learner top bar; `studio.vue` is the CMS sidebar. Studio pages opt
  in with `definePageMeta({ layout: "studio" })` — Nuxt's default is `default`, and a studio page
  quietly rendering learner chrome should fail loudly rather than look nearly right.
* **Landing is role-aware:** `useRoles().homePath` sends an Author/Editor/Admin to `/studio` and
  everyone else to `/`. Learners outnumber editors by orders of magnitude, which is why they hold the
  root.
* **Role checks are affordances, never a boundary.** The API authorises every request independently
  (SECURITY.md §2). A learner who types `/studio` is redirected to their dashboard because a shell
  whose every request 403s is a bad page, not because the redirect protects anything.
* **i18n:** `no_prefix` strategy, unlike the site's `prefix_except_default` — nothing here is indexed,
  so there is no `/id/*` namespace to earn and a locale prefix would be pure URL noise. The
  `databro_locale` cookie is shared with `site`, so a language choice survives crossing between them.

> The **content-block renderer is shared** via `packages/ui`, so the CMS preview and the public `site`
> render identically — no drift.

## 5. Shared packages

* **`ui`** — component library + Tailwind preset (single source of design tokens) + the content-block
  renderer registry (one renderer used by both apps). WCAG 2.1 AA target.

### Design tokens

Two layers, and the distinction is load-bearing:

1. **Raw values** — the brand ramp, typefaces, the type scale — in `tailwind-preset.ts`. Swapping the
   palette or typeface is an edit to that object and nothing else.
2. **Semantic names** — `surface`, `ink`, `line`, `accent`, `note-*` — resolved through CSS custom
   properties in `ui/src/styles/tokens.css`, which both apps load.

Components reference *meaning* (`text-ink-muted`), never a raw colour (`text-slate-500`). That is what
makes light and dark themes come from one set of class names, and what keeps a palette change from
touching a single component. Values use space-separated RGB channels so Tailwind's opacity modifiers
(`text-ink/70`) still work through a variable.

Dark mode responds to **both** `prefers-color-scheme` and an explicit `[data-theme]`, with the
explicit choice winning in either direction.

Reading-specific tokens worth knowing: `max-w-prose` is ~68ch — the single most load-bearing number
for long-form readability — and `max-w-shell` is the wider container for listings and chrome, which
are scanned rather than read. Article vertical rhythm lives on the `.databro-content` container, not
on individual blocks, so every block type gets consistent spacing and a block renders correctly
wherever it appears, including nested in a list item.
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
