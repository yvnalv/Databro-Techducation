# DataBro Changelog

## [2026-07-31 15:05:00 UTC]

CHG-0010 — Content model v2: inline rich text, math, code output, nested blocks

- **ADR-0009 — inline rich text as a ProseMirror-compatible node tree.** Every text field in the block
  catalog was a plain string, which meant a published article could not contain a single hyperlink:
  no citations, no linking to external docs, no internal linking beyond taxonomy. For a platform whose
  acquisition strategy is long-form technical content, that was the most limiting property of the
  model. Inline content is now an array of nodes shaped like ProseMirror's document model — the shape
  Tiptap uses natively, so the CMS editor will need no translation layer between what it edits and
  what is stored.
- Marks are `bold`, `italic`, `code`, `strike` and `link`. Inline content applies to `paragraph`,
  `callout`, `quote`, list items and table cells. `heading` deliberately stays a plain string, since
  emphasis or links inside a heading hurt both the document outline and anchor generation.
- **Marks map to elements, never to HTML strings** — the no-`v-html` rule already governing block text
  extends to inline content, which is equally author-supplied. A `link` href is scheme-checked exactly
  like an embed URL: `javascript:`, `data:` and protocol-relative URLs drop the anchor while keeping
  the prose. Site-relative hrefs are allowed so articles can link to one another.
- **`math` moved into Phase 1** (block + `mathInline`), from "reserved for later phases". Explaining
  attention, gradients and loss functions is core Phase 1 subject matter here, not a Phase 2 nicety.
  KaTeX is the single deliberate `v-html` exception: its input is LaTeX rather than HTML, it runs with
  `trust: false` so markup-emitting commands are disabled, and `throwOnError: false` renders a
  malformed formula as visible error text instead of failing the whole server render. The reasoning
  lives at the call site.
- **`code.output`** pairs a sample with its result — the "run this, get that" pattern this genre leans
  on — rendered as `<samp>` so it is never mistaken for source or syntax-highlighted.
- **List items may contain blocks**, so a tutorial step can carry its own code sample. Rendering is
  therefore recursive and depth-capped at one level of nesting: past that, nested blocks are dropped,
  so a malformed document cannot exhaust the stack during SSR.
- Renderers accept the pre-ADR-0009 plain `text: string` wherever `content` is expected. There is no
  production content, so no data migration was written; the shim keeps existing local documents
  rendering and is explicitly not a supported authoring shape.
- Verified: 45 renderer tests (up from 22) covering marks, mark nesting, hostile hrefs, XSS, malformed
  LaTeX, KaTeX injection attempts, code output, nested steps and the depth cap; 69 backend tests;
  clean typecheck across five workspaces. Confirmed live in the containerised stack against a seeded
  article exercising every new capability, including that a `javascript:` link renders as text with no
  anchor.

---

## [2026-07-30 16:10:00 UTC]

CHG-0009 — Fix SSR API resolution in the containerised stack

- The Nuxt apps reached the API at a single configured URL. That is wrong in a containerised run:
  `NUXT_PUBLIC_API_BASE_URL` is browser-facing (`http://localhost:5158`), but inside the site
  container `localhost` is the site container itself, so every server-rendered page would fail with a
  connection refused. Split it: a server-only `apiInternalBaseUrl` (`http://api:8080` in Docker) used
  during SSR/prerender, falling back to the public URL when unset — which is the correct behaviour on
  the host, where one address serves both.
- The bug was latent until CHG-0007: before the site fetched anything, `/` was a static placeholder
  that returned 200 either way. It cannot reproduce on the host at all, since there `localhost` is
  right for both callers.
- Compose: `site` and `app` now wait on the API's healthcheck rather than merely its start, and `site`
  gets `NUXT_PUBLIC_SITE_URL` so canonical URLs are correct in the containerised run.
- Documented in LOCAL_DEVELOPMENT.md (both addresses, plus how to rebuild after a dependency change so
  containers do not keep the `node_modules` baked into the previous image) and
  FRONTEND_ARCHITECTURE.md.
- Verified: the full `apps` profile serves the homepage, article, category, tag and Indonesian pages
  with data fetched server-side, correct 404s, and no internal container URL leaking into the HTML.

---

## [2026-07-30 15:30:00 UTC]

CHG-0008 — Taxonomy: categories, tags, and crawlable pagination

- **Domain:** `Category` (hierarchical) and `Tag` as aggregates separate from `Article`, referenced by
  id only so the Article boundary holds. Enforced TX-1 (slug unique *per type*, so
  `/categories/python` and `/tags/python` legitimately coexist), TX-2 (a category still classifying
  articles, or with children, cannot be deleted — refused with the referencing count), TX-3 (no
  cycles: the domain rejects a move using an ancestor chain the application supplies, since the domain
  cannot query), and CT-11 (one category, many tags). `SetTags` is idempotent so EF does not churn
  join rows on every save.
- **Category and tag slugs are immutable**, matching articles (CT-2/CT-3). Only display names are
  editable. Renaming a term's URL needs a 301 record, so it waits for the redirects slice — which
  means this slice ships with no URL-breaking hole rather than a half-built one.
- **Permission split that falls out of the existing grants:** creating a term needs `Taxonomy.Manage`
  (Editor/Admin), but assigning an existing term is part of `Content.Edit`. An Author can label an
  article and cannot mint new vocabulary, which is what prevents tag sprawl.
- **Persistence:** `categories`, `tags`, `article_tags` plus the real FK on `articles.category_id`
  (`AddTaxonomy` migration). Tag links are an aggregate-owned child collection rather than a
  many-to-many navigation, which would have coupled the two aggregates. Article tag lists are read
  through a join against `tags` so the global soft-delete filter applies — a deleted tag cannot leak
  onto a public page.
- **Offset pagination on public listings**, replacing the unbounded `limit`. Resolves a genuine
  conflict between two docs: API_SPEC §3 preferred cursors, but SEO.md requires crawlable paginated
  URLs, and a cursor has no stable URL a crawler can enumerate. Cursors are now scoped to non-indexed
  feeds; `pageSize` is clamped (default 20, max 100) so it cannot be used to pull the whole table.
  Paging lives in `meta`.
- **Filtering:** `?category=` / `?tag=` by slug. An unmatched slug returns an empty page rather than
  the unfiltered catalogue — silently dropping a filter would serve the whole archive on a page that
  should be empty.
- **Site:** `/categories/{slug}` and `/tags/{slug}` with a shared `ArticleList`, crawlable
  `PaginationNav`, and taxonomy links on article pages and cards — the internal linking structure that
  makes a topic cluster legible. Category pages emit `BreadcrumbList` structured data mirroring the
  visible breadcrumb; tag pages deliberately emit none, because tags are flat and claiming a hierarchy
  would misrepresent the site.
- **Listing SEO:** each page is self-canonical (page 2 canonicalises to page 2, not page 1 — otherwise
  the articles only listed there lose their discovery path), page 2+ titles are disambiguated, and a
  `?page=` past the end returns **404** instead of an empty 200, which would have let a crawler
  enumerate unbounded thin pages. `rel=prev/next` is emitted only as a courtesy; Google dropped it as
  an indexing signal in 2019, so the crawlable anchors are the load-bearing part. SEO.md corrected
  accordingly.
- Contracts: `TaxonomyTerm`, `Category`, `CategoryWithAncestors`, `Paged<T>` and `PageMeta` in
  `@databro/types`; category/tag/paging support in `api-client`. `en`/`id` dictionaries extended with
  pluralized article counts.
- `scripts/dev-seed-article.ps1` now seeds a category tree and tags, and takes `-Count` for
  paginating volume. Fixed a collision where consecutive runs reused the same registration email.
- Verified end to end: 69 backend tests (up from 39), 22 renderer tests, clean typecheck across five
  workspaces, and live checks of the category tree, breadcrumb JSON-LD, filtering, multi-page
  pagination, out-of-range 404s, and the Indonesian locale.

---

## [2026-07-30 14:35:00 UTC]

CHG-0007 — Public site render: block renderer, SEO surface, and the first cross-module contract

- **ADR-0008 — cross-module read contracts live in `Platform`.** Rendering a byline needed Identity's
  display name from inside Content, which the `Application_should_not_depend_on_other_modules` fitness
  test forbids. Added `IUserDirectory` (+ `UserSummary`) to `Platform.Abstractions`, implemented by
  Identity's Infrastructure and consumed by `ArticleService`. Batch-shaped to prevent N+1 on list
  endpoints; partial results are legal so a deleted author cannot break an article page.
- **Reconciled the API contract with `@databro/types`**, which had drifted from the backend on six
  fields. `author` is now a resolved `{ id, displayName, avatarUrl }` object instead of a raw
  `authorId`; `status`/`visibility` cross the wire lowercase to match the TypeScript unions;
  `tags`/`categorySlug` removed until taxonomy exists. `api-client` dropped `search()` and the
  category/tag filters — endpoints that do not exist yet.
- **Block renderer in `@databro/ui`**: `ContentRenderer` + a typed `Record<BlockType, Component>`
  registry covering all ten Phase 1 block types, so adding a `BlockType` member fails the build until
  a renderer exists. Lives in the shared package because `site` and the future CMS preview must never
  drift. Unknown types degrade (hidden for readers, placeholder in preview) because content outlives
  renderers. `SUPPORTED_BLOCK_TYPES` is now derived from the registry rather than hand-maintained.
- **Renderer security:** block text is interpolated, never `v-html` — block data is author-supplied
  and arrives straight from JSONB. Embeds are host-allowlisted (YouTube/Vimeo/CodePen), normalized to
  the provider's documented embed URL, https-only, sandboxed, and degraded to a `nofollow noopener`
  link when unrecognised; `paragraph.marks` stays unimplemented pending a structured mark renderer.
- **Site pages:** article and list pages, layout chrome, and an error page. A missing or unpublished
  slug now returns a real `404` — `useAsyncData` re-wraps handler throws, so the API status was being
  lost and surfaced as `503`, which would have told crawlers to retry and kept dead URLs indexed.
- **SEO (`useArticleSeo`)**: canonical (author-set wins), hreflang alternates derived from
  `localePath` so the URL strategy is not duplicated, OpenGraph/Twitter, and JSON-LD `Article`.
  Premium articles stay fully indexable with `isAccessibleForFree: false` + `hasPart.cssSelector`
  declaring the gated region.
- **i18n**: `@nuxtjs/i18n` on both apps with structurally identical `en`/`id` dictionaries;
  `prefix_except_default` strategy, and browser-language detection never redirects so a crawler always
  gets the same HTML for a URL.
- Fixed along the way: both Nuxt apps lacked a `tsconfig.json`, so `nuxt typecheck` had been silently
  checking nothing; `apps/app` used a `robots: false` route rule that was never a real Nuxt option
  (replaced with `X-Robots-Tag`); Tailwind now scans `packages/ui` or the renderer's classes are purged
  from production builds; pinned Tailwind v3 (v4 is incompatible with `@nuxtjs/tailwindcss@6`) and
  upgraded Pinia to v3 / `@pinia/nuxt@0.11` (0.9 crashed Nuxt 4.5's payload serializer while rendering
  the error page); pnpm 11 renamed `onlyBuiltDependencies` to `allowBuilds`.
- Added `scripts/dev-seed-article.ps1`, which publishes a demo article using every block type plus a
  deliberately unknown one.
- Verified end to end against live data: 39 backend tests, 22 renderer tests, clean typecheck across
  all five workspaces, all ten block types rendering in SSR HTML, the full SEO surface asserted,
  `404`/`200` status codes correct in both locales, and the production build prerendering with
  renderer classes surviving the Tailwind purge.

---

## [2026-07-30 13:55:00 UTC]

CHG-0006 — Containerised local development environment

- Extended `docker-compose.yml` with an opt-in `apps` profile that runs the API and both Nuxt apps
  alongside the existing infra, all hot-reloading against bind-mounted source. The default
  `docker compose up -d` still starts infrastructure only, so the fast host-based inner loop is
  unchanged.
- Added `backend/Dockerfile` (`dev` target running `dotnet watch`; `build`/`runtime` targets
  publishing a non-root ASP.NET image) and `frontend/Dockerfile` (pnpm-workspace aware, `APP` build
  arg selecting `site` or `app`; `dev` target running `nuxt dev`, `runtime` target serving the Nitro
  node-server output), plus `.dockerignore` for both.
- The API dev container redirects MSBuild output to an `/artifacts` volume via `UseArtifactsOutput`,
  so a Linux container and the Windows host never share `bin`/`obj`. Node modules are likewise
  masked by anonymous volumes.
- Added `scripts/dev-up.ps1` (start + wait for health, `-Apps`, `-Reset`), `scripts/dev-grant-role.ps1`
  (dev-only RBAC grant — self-registration assigns Reader), and `scripts/dev-smoke.ps1`, a 10-step
  end-to-end check of the running stack: register → grant Editor → login → 401 unauthenticated →
  create draft → 404 unpublished → publish → public read → unpublish → 404. All scripts target
  Windows PowerShell 5.1.
- Relaxed `backend/global.json` from the exact SDK `9.0.309` to the `9.0.3xx` feature band
  (`rollForward: latestFeature`). The exact pin failed on any machine with a lower patch in the same
  band, including the .NET SDK container image.
- Added `docs/LOCAL_DEVELOPMENT.md` (prerequisites, both run modes and their trade-offs, verification
  layers, data/migration recipes, troubleshooting) and indexed it in `docs/README.md`.
- Verified: 36 tests pass; both run modes serve `/health`, and `dev-smoke.ps1` passes 10/10 against
  each; API hot reload and Nuxt HMR both confirmed through the Windows bind mount.

---

## [2026-07-30 09:30:00 UTC]

CHG-0005 — Identity module: authentication, RBAC, and secured authoring

- Built the Identity module on ASP.NET Core Identity (EF Core, `identity` schema): registration with
  email-confirmation token, password login, JWT access tokens + hashed rotating refresh tokens, and a
  `/api/v1/me` profile endpoint. Email transport and social login are stubbed for a later slice
  (`RequireConfirmedEmail=false`, no-op email sender logs the token).
- RBAC: roles (Reader/Author/Editor/Admin) with a role→permission grant map; permissions issued as JWT
  claims. Permission-based authorization via on-demand `perm:{Permission}` policies (custom policy
  provider + handler). Roles seeded on startup.
- Moved the permission-name vocabulary to `Platform.Authorization.Permissions` (shared) so modules
  require permissions without depending on Identity; the grant map stays in Identity.
- Extracted a shared `Platform.Web` kernel (response envelope + validation filter) and refactored the
  Content module onto it (removing the duplicated helpers).
- Secured the Content authoring endpoints with permissions (create/edit → Author, publish/unpublish →
  Editor); anonymous → 401, insufficient permission → 403. The author-of-record now comes from the
  JWT via a real `HttpCurrentUser` (replaces `NullCurrentUser`), which also populates audit fields.
- Dev convenience: per-module startup initializers apply pending migrations in Development only, so a
  fresh clone self-provisions after `docker compose up`.
- Tests: added Identity auth integration tests (register/login/refresh rotation/me) and updated the
  Content tests to authenticate; added authz-boundary cases (401/403) and author-of-record. Whole
  suite green: build 0/0; 36 tests pass (4 architecture + 32 Content/Identity). Verified end-to-end
  against the local Dockerized Postgres.

---

## [2026-07-30 06:00:00 UTC]

CHG-0004 — Harden the Content module: validation and tests

- Added FluentValidation request validation (create/update article, content document, blocks) enforced
  via a reusable minimal-API `ValidationFilter<T>` that returns the standard `validation_failed`
  envelope with per-field details (docs/ERROR_HANDLING.md).
- Exposed `POST /api/v1/authoring/articles/{id}/unpublish`; added `ArticleService.UnpublishAsync`.
- New test project `DataBro.Modules.Content.Tests`:
  - Domain unit tests for `Slug` (validation/normalization) and `Article`
    (publish/versioning/unpublish business rules CT-1/CT-5/CT-8).
  - Full-stack API integration tests against a throwaway PostgreSQL container (Testcontainers) via
    `WebApplicationFactory<Program>`: happy-path create → publish → public read, publish gating,
    duplicate-slug conflict, invalid-slug/empty-title validation, blockless-publish `422`, and
    unpublish hiding from public read.
- Whole suite green: build 0/0; 29 tests pass (4 architecture + 25 Content).

---

## [2026-07-30 02:30:00 UTC]

CHG-0003 — Local dev infrastructure and the first Content vertical slice

- Added `docker-compose.yml` (PostgreSQL 16, Redis 7, MinIO) with healthchecks, `.env.example`, and a
  gitignored local `.env`. Postgres is mapped to host port 5439 (5432–5434 were occupied locally).
- Wired EF Core 9.0.18 + Npgsql 9.0.4 (pinned; SDK stays on 9.0.309). Introduced a `Platform.Persistence`
  shared infra project so EF never leaks into domain-facing `Platform`: audit `SaveChanges` interceptor,
  soft-delete global query filter, client-generated-key convention (`ValueGeneratedNever`), `SystemClock`,
  and `NullCurrentUser` (until Identity supplies the user).
- Content domain: `Article` aggregate (typed JSONB `draft_blocks`/`published_blocks`, `Slug` value
  object, `SeoMetadata`, `Visibility`), append-only `ArticleVersion` history, `Publish`/`Unpublish`
  with domain events and business-rule enforcement (CT-1, CT-5, CT-6, CT-8).
- Content persistence: `ContentDbContext` (owns the `content` schema, snake_case naming), EF configs
  with JSONB value converters, `ArticleRepository`, DI wiring, design-time factory, and the initial
  migration applied to Postgres.
- Content API: public read (`GET /api/v1/articles`, `/{slug}`) and authoring
  (`POST /api/v1/authoring/articles`, `PATCH`, `/{id}/publish`) behind the standard response envelope.
- Verified end-to-end against Dockerized Postgres: create draft → 404 while unpublished → publish
  (snapshots blocks, writes an immutable version, sets `published_at`) → served publicly by slug;
  empty article rejected with `business_rule_violation` (422). Build 0/0; 4 architecture tests pass.

---

## [2026-07-29 10:30:00 UTC]

CHG-0002 — Scaffold backend modular monolith and frontend monorepo

- Backend (.NET 9, SDK pinned to 9.0.309 via `global.json`): created `DataBro.sln` with the
  `Platform` shared kernel (Entity, AggregateRoot, Result/Error, integration-event building blocks),
  the `Identity`/`Content`/`Media`/`Search` modules each across Domain/Application/Infrastructure/Api
  layers, an API host that composes every module (health endpoint + per-module endpoints), and a
  NetArchTest architecture-fitness test project.
- Enforced boundaries: a scoped `src/Modules/Directory.Build.props` grants the ASP.NET Core framework
  reference only to Infrastructure/Api, keeping Domain/Application free of web/framework dependencies.
  Four architecture tests pass (Domain purity, no cross-module dependencies).
- Verified: full solution builds with 0 warnings/0 errors; architecture tests green; the running host
  serves `/health` and each module's `/_ping` endpoint.
- Frontend (pnpm workspace monorepo): `apps/site` (public, SSG/ISR) and `apps/app` (authenticated
  Nuxt 4 apps), plus shared packages `@databro/ui` (Tailwind preset/tokens), `@databro/api-client`
  (typed envelope client), and `@databro/types` (API + content-block schema). Shared packages
  typecheck clean and the `site` app builds end-to-end.
- Extended `.gitignore` for .NET (`bin`/`obj`) and Node/Nuxt (`node_modules`/`.nuxt`/`.output`)
  artifacts.

---

## [2026-07-29 00:00:00 UTC]

CHG-0001 — Initial project documentation

- Established the foundational documentation set: `CLAUDE.md` master instructions, `README.md`, and
  the `docs/` tree (PRD, ROADMAP, STATUS, ARCHITECTURE, MODULES, DATABASE, CONTENT_MODEL,
  FRONTEND_ARCHITECTURE, SEO, API_SPEC, SECURITY, BUSINESS_RULES, ERROR_HANDLING, DECISIONS,
  CODING_STANDARDS, TESTING, DEPLOYMENT, GLOSSARY).
- Recorded the initial architecture decisions as ADR-0001 through ADR-0007.
- Locked the load-bearing decisions: Modular Monolith + Clean Architecture; B2C-first tenancy;
  articles-first wedge; in-house CMS scoped to articles for Phase 1; unified Article/Lesson content
  engine; typed JSONB content blocks; two-app frontend monorepo; PostgreSQL FTS for initial search.
- No application code yet — design phase.
