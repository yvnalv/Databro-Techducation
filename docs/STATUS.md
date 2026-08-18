# DataBro — Status

Snapshot of where the project is, what's next, and what's open. Update this with every meaningful
milestone.

Last updated: 2026-08-18.

## Current phase

**Phase 1 — Foundation & Content.** Sub-stage: **the exit criterion is met; CI and a staging deploy
remain.**

The [ROADMAP](ROADMAP.md) exit criterion is one compound sentence:

> "an editor can author, version, schedule, and publish an SEO-complete article that is indexed,
> searchable, and served fast from the public site."

| Verb | State |
|---|---|
| author | ✅ block editor |
| version | ✅ history panel with restore |
| schedule | ✅ schedule and cancel, in the CMS |
| publish | ✅ |
| SEO-complete | ✅ including `og:image`, Twitter card and JSON-LD `image` |
| indexed | ✅ sitemap, robots, feed, JSON-LD |
| searchable | ✅ PostgreSQL FTS |
| served fast | ✅ SSG/ISR |

An earlier revision of this file claimed this was met while scheduling and version history had no
CMS controls at all. It is met now — but the intermediate correction stays on the record rather than
being tidied away.

## Done

* Locked the load-bearing architecture decisions (ADR-0001 … ADR-0009).
* Authored the foundational documentation set (CLAUDE.md + docs/).
* Scaffolded the **backend** Modular Monolith and **frontend** pnpm monorepo.
* **Local dev environment:** `docker-compose.yml` — infra by default (PostgreSQL on host port
  **5439**, Redis, MinIO) plus an opt-in `apps` profile that containerises the API and both Nuxt apps
  with hot reload. Helper scripts in `scripts/` (`dev-up`, `dev-grant-role`, `dev-smoke`). See
  [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md).
* **EF Core foundation:** EF 9.0.18 + Npgsql 9.0.4 pinned; `Platform.Persistence` (audit interceptor,
  soft-delete filter, client-generated keys, clock) keeps EF out of the domain-facing `Platform`.
* **Content vertical slice (working):** `Article` aggregate with JSONB blocks + versioning,
  `ContentDbContext` (snake_case, `content` schema), repository, initial migration applied, and public
  read + authoring endpoints. Verified create → publish → public read against Dockerized Postgres.
* Build 0/0; 4 architecture-fitness tests pass.

## In progress

* Phase 1's exit criterion is met and **CI is running**.
* **The staging deploy is deferred, deliberately.** DigitalOcean is not provisioned yet and nothing
  else depends on it — the whole stack runs locally through Docker Compose. Development continues
  against the local stack; the deploy is picked up when there is infrastructure to deploy to. This
  is a scheduling decision, not a gap in the phase.

## Recently done

* **Polish pass:** server-side syntax highlighting (Shiki — no highlighter in the client bundle,
  verified against the built image), a table grid editor that keeps the grid rectangular, a seeded
  `admin@databro.local` for local CMS access, and **ESLint** across the workspace, wired into CI
  with `vue/no-v-html` escalated to an error so a third unjustified `v-html` fails the build.
* **CI** ([`.github/workflows/ci.yml`](../.github/workflows/ci.yml)): backend build + 180 tests
  (including the architecture-fitness gate) and frontend typecheck + 81 tests in parallel, then
  image builds. Every command was run locally before committing rather than trusted to be right —
  which caught an invalid `pnpm --if-present exec` flag and three test projects overwriting each
  other's TRX file.
* **Found and fixed a deployment landmine while wiring CI.** The production site image baked a
  broken homepage: `prerender: true` renders `/` at image-build time with no API reachable, so the
  shipped HTML was the error fallback with zero article links — served forever, since a prerendered
  page is never re-rendered. Now `isr: 600`, which also fixes a second bug hiding behind the first:
  a prerendered homepage would never show a newly published article until the next deploy. CI now
  fails if any prerendered HTML appears in the image.

* **Scheduling and version history in the CMS**, plus the endpoints they needed. This was scoped as
  "UI-only work" and was not: there was no endpoint to read or restore a version, and no way to
  cancel a schedule once set (`/unpublish` only accepts a published article, so scheduling was a
  one-way door).
* **Fixed a CT-6 leak found by one of those tests.** `title` and `summary` were single columns shared
  by the draft and the published copy — as if `published_blocks` had never been split from
  `draft_blocks`. Editing a published article's draft title changed the live page, the listings, the
  sitemap, the RSS feed and the search index the moment it was saved, including the fuzzy search
  fallback, which matched on the draft column. Now `published_title` / `published_summary`, with a
  migration that backfills existing rows. Three regression tests pin it.

* **Media module** ([ADR-0011](adr/0011-media-storage-and-image-processing.md)). Upload to
  S3-compatible storage (MinIO dev / Spaces prod), images **re-encoded before storing** so the bytes
  are always ours, EXIF stripped, format from magic bytes, generated storage keys, decompression-bomb
  check on header dimensions. Responsive variants in a Hangfire job. Content resolves media ids
  through `IMediaDirectory` in Platform and ships a resolved map with the article, so the renderer
  does a lookup rather than a request per figure. CMS gets an upload-or-choose picker; the site gets
  real `srcset`/`sizes` and a working `og:image`. 29 Media tests plus renderer coverage; the full
  path verified against the running stack including rejection of an executable renamed `.jpg`.

* **PostgreSQL full-text search** ([ADR-0010](adr/0010-fts-lives-in-content.md)). Implemented inside
  **Content**, not the Search module: a `tsvector GENERATED ALWAYS … STORED` column on `articles`,
  weighted title/summary/body, stemmed per locale, with a `word_similarity` typo fallback. The
  ADR-0006 design (a Search-owned table fed by integration events) depends on a transactional outbox
  that does not exist, so it would have shipped with a known consistency hole. `GET /api/v1/search`
  is the seam that survives the eventual engine swap. Site UI at `/search`, `noindex, follow` and
  robots-disallowed. 10 integration tests against a real PostgreSQL container.

* **Discovery artifacts:** `robots.txt`, `sitemap.xml` and `feed.xml` (RSS 2.0), served as Nitro
  routes from the `site` app. This **corrects [SEO.md](SEO.md) §1 and [API_SPEC.md](API_SPEC.md)**,
  which had assigned them to Platform — a crawler fetches them from the site's origin, so they
  cannot live on the API. The sitemap emits every URL per locale with `xhtml:link` alternates and
  `x-default`; the feed is English-only with summaries, never rendered bodies. Verified against the
  containerised stack: 74 sitemap URLs, 25 feed items, both well-formed XML.

* **Scheduled publishing (CT-7):** `POST /api/v1/authoring/articles/{id}/schedule` (behind
  `Content.Publish`) sets a future publish time; a **Hangfire** recurring sweep (every minute, backed
  by PostgreSQL storage) publishes articles as they come due. Honours CT-7's failure contract — an
  article that can no longer satisfy the publish preconditions when its time arrives stays scheduled
  and logs an alert rather than being dropped. Hangfire is host-owned; the Content module registers
  its own recurring job. Dev-only dashboard at `/hangfire`.
* **Slug-change 301 redirects (CT-2/CT-3, docs/SEO.md §4):** a `redirects` table in the `content`
  schema, dedicated `PUT .../{id}/slug` endpoints for articles (behind `Content.Publish`) and
  categories/tags (behind `Taxonomy.Manage`), and a public `GET /api/v1/redirects?from=` lookup the
  `site` app hits on a 404 to serve a 301 instead of a dead page. An article's move records a redirect
  only once it has been published; a term's always does. Redirect chains are collapsed on write (one
  hop), and the `from_path` unique index is filtered on `is_deleted` so a freed path can move again.
* **Content model v2 (ADR-0009):** inline rich text as a ProseMirror/Tiptap-compatible node tree, so
  articles can finally contain links, inline code and emphasis. Added `math` (KaTeX, block + inline),
  `code.output`, and blocks nested inside list items. Renderers accept the pre-ADR-0009 plain-string
  shape as a compatibility shim.
* **Taxonomy:** `Category` (hierarchical) and `Tag` aggregates with TX-1/2/3 + CT-11 enforced,
  authoring CRUD behind `Taxonomy.Manage`, assignment on articles, and `/categories/{slug}` +
  `/tags/{slug}` pages with `BreadcrumbList` structured data. Public listings are now offset-paginated
  with crawlable page links.
* **Public site render:** block renderer registry in `@databro/ui` (all ten Phase 1 block types +
  unknown-type fallback), article and list pages on `site`, full SEO head (canonical, hreflang,
  OG/Twitter, JSON-LD `Article`), real 404s, premium preview, and `en`/`id` i18n. Verified end to end
  against live data.
* **First cross-module contract (ADR-0008):** `IUserDirectory` in `Platform`, implemented by Identity,
  consumed by Content to resolve author bylines without a module-to-module reference.
* **Identity module:** ASP.NET Core Identity + JWT (access + rotating refresh), RBAC with permission
  claims and `perm:` authorization policies, secured Content authoring (401/403), real
  `HttpCurrentUser` for audit + author-of-record. Roles seeded; dev auto-migration initializers.
* **Shared `Platform.Web`** kernel (envelope + validation filter); Content refactored onto it.

## Next up (proposed order)

**Phase 2 — Learning** is under way. Steps 1 and 2 of
[ADR-0012](adr/0012-lesson-bodies-live-in-content.md)'s implementation order are done: the content
engine is extracted, and lesson bodies exist as their own aggregate.

Steps 1–5 of [ADR-0012](adr/0012-lesson-bodies-live-in-content.md)'s order are done, bar the CMS.
A course can be built, published and read through the API, with its lessons joined to bodies Content
owns.

The backend loop is closed: a lesson body can be written and published, attached to a course, and
read on the public course page — all through the API, verified live.

A curriculum can now be built end to end in the CMS: write a lesson body, publish it, create a
course, attach the lesson, reorder, publish. Nothing left needs a script or a hand-inserted row.

Public course pages ship too: a learner can browse the catalogue and read a curriculum.

Courses are searchable ([ADR-0014](adr/0014-search-across-modules.md)): results come back segmented
per module rather than merged into a ranking that would be fabricated.

Enrollment and progress are built (LN-6 … LN-11): a learner can join a course, move through it, and
finish it, with completion recorded as a moment that a growing curriculum cannot revoke.

The learner app exists ([ADR-0015](adr/0015-authenticated-app-hosts-both-audiences.md)):
`apps/app` serves learners at `/` and the CMS at `/studio`, with role-aware landing.

**The learner loop is closed.** Browse a path or a course, open a lesson at
`/courses/{course}/{lesson}`, read it, mark it complete, and watch the dashboard move — whose Resume
button goes back to that lesson. In English or Indonesian, with every page in the sitemap and
carrying structured data.

Learning paths ship too, at `/learning-paths` and `/learning-paths/{slug}`, with the curated sequence
numbered. **An earlier revision of this file said their "domain and API exist" — the API did not.**
There was no service and no endpoints, only an orphan DTO. Both exist now, and the correction stays
on the record rather than being tidied away.

1. **Assessment** (quizzes, attempts, scoring) — the largest remaining piece of Phase 2 and its own
   module. It is what makes "completed" something a learner had to earn, and the prerequisite for
   certificates in Phase 3.
2. **Finish the CMS's Indonesian strings.** ADR-0015 wired up i18n and covered the chrome, login and
   every learner string; `/studio`'s own labels are still hardcoded English against rule 19.
3. Then close Phase 2: **bookmarks**, **streaks**, and **social login** (quizzes, attempts, scoring) as its own module.

Two items that stood here for several revisions are now **done** and are recorded as such rather than
quietly deleted: the search decision ([ADR-0014](adr/0014-search-across-modules.md), segmented
per module) and the app-boundary question ([ADR-0015](adr/0015-authenticated-app-hosts-both-audiences.md)).

Independent of Phase 2:

* **Staging deploy on DigitalOcean** — deferred until there is infrastructure to deploy to.
  Auto-migration is `IsDevelopment()`-gated, so the deploy needs an explicit migration step or the
  schema will never move.
* **ESLint has no backend counterpart.** No analyzer ruleset is configured for the C# side.

## Known gaps / deferred

* **Outbox retention.** Processed rows are kept as an audit of what the system decided to do, which is
  worth having while volume is negligible. A sweep is owed before it is not
  ([ADR-0017](adr/0017-transactional-outbox.md)).
* **Dead-lettered outbox messages are only visible in the database.** A parked row has no operational
  surface.
* **Social login (Google/GitHub) and `PATCH /me` are still unbuilt** — Phase 1 scope. API_SPEC now
  lists them under an explicit "Not built" heading rather than describing them as though they exist;
  the other three phantom endpoints were built in CHG-0047.

* **The CMS is still English-only.** [ADR-0015](adr/0015-authenticated-app-hosts-both-audiences.md)
  registered `@nuxtjs/i18n` in `apps/app` — it had been a dependency that was never wired — and
  covered the shared chrome, login and every learner string in both locales. The CMS's own body
  strings (editor labels, table headers, buttons inside `/studio`) are still hardcoded English
  against rule 19. Now mechanical to finish, since the module is in place and the locale files exist.
* **The session cookie is shared between apps by host, not by configuration.** Cookies ignore port,
  so `localhost:3000` and `localhost:3001` genuinely share one in development. In production the two
  apps are separate subdomains and the cookie needs an explicit parent `domain` — owed with the first
  real deploy, and not verifiable from here.
* **Existing dev accounts are unconfirmed** and cannot sign in since CHG-0048 enforced ID-2. Use the
  "send it again" link on the sign-in form and confirm through Mailpit; the seeded
  `admin@databro.local` was confirmed at seed time and is unaffected.
* Social login (Google/GitHub) not yet implemented.
* **Design pass complete for what exists**, matching the reference: sampled blue palette,
  pink→violet page-header gradient, navy footer. **Light mode only** — the earlier
  `prefers-color-scheme` switch made dark-OS visitors see a dark site and has been removed. The
  reference's course grid, instructor carousel and pricing table wait for Phase 2 data.
* **Premium bodies are not actually gated yet.** The badge, preview notice, marked region and JSON-LD
  paywall declaration are in place, but the full body still renders: there is no entitlement check to
  gate on until Billing (Phase 3). Reserved, not enforced.
* **Syntax highlighting runs server-side only.** The CMS live preview therefore shows code as plain
  text — deliberate: preview is for structure, and shipping a highlighter to the browser to colour a
  draft would cost every reader the same bundle.
* **Authoring UI is complete for every block type.** Sign-in, route guard, dashboard shell, article
  list, and a block editor with Tiptap rich text and live preview. Taxonomy, media, scheduling,
  version history and tables all have forms. An article can be written, saved, published and read
  on the public site without touching a script.
* **ImageSharp's licence must be re-checked before commercial launch.** The Six Labors Split License
  is free for open source and organisations under $1M revenue, which is DataBro today. It is confined
  behind `IImageProcessor` precisely because it is the most likely dependency to need swapping.
* **No orphan sweep for media.** Deleting an asset soft-deletes the row and deliberately leaves the
  stored objects, since a published article may still reference them. Reclaiming bytes needs
  something that tracks which content references which asset — it does not exist yet.
* **Media has no integration event.** `MediaUploaded` is unbuilt because nothing consumes one and the
  outbox does not exist.
* **CMS tokens are not `httpOnly`.** The app sets them from JS, so it cannot be; they are
  `sameSite=strict` and `secure` outside development. The hardening is a backend-for-frontend that
  proxies login and sets cookies the browser never reads — a deliberate follow-up, not an oversight.
* **`Search/` is four empty marker projects.** Real search lives in Content (ADR-0010), so the module
  list overstates what is built. It becomes real when the outbox lands or Learning adds a second
  searchable aggregate.
* **The fuzzy fallback does a sequential scan.** `word_similarity(query, title) > 0.3` cannot use a
  `gin_trgm_ops` index, which answers the `<%` operator at a session-level 0.6 threshold — too strict
  for the typos the fallback exists for. Acceptable while it only runs on queries that matched
  nothing; it is one of the triggers for the OpenSearch upgrade.
* **Article listings load full bodies.** `ListPublishedAsync` materializes whole `Article` entities —
  `draft_blocks`, `published_blocks` and now `search_vector` — to build summaries that use none of
  them. The search vector made an existing inefficiency ~30% worse rather than creating a new one;
  the fix is a projection query on the summary paths, not table splitting for the vector alone.
* **The sitemap pages the public listing** 100 articles at a time (cap 50 pages). Correct and cheap
  at the current size; at ten thousand articles it needs a bulk `lastmod`-only endpoint and a sitemap
  index. Noted rather than pre-built.
* **One RSS feed, English only.** A channel declares a single `language`; `/id` gets its own feed
  when it has content to justify one.
* **Hangfire** now runs the scheduled-publish sweep (PostgreSQL storage). Redis is still provisioned
  but unused. The scheduled-publish failure "alert" is a logged error for now — it becomes a real
  notification when the Notification module lands. The sweep assumes a single job server; a
  multi-server deploy needs `DisableConcurrentExecution` before it is safe.
* Media module is still a scaffold, so image blocks render an accessible placeholder.
* **Slug changes go through a dedicated endpoint**, not the general update — a term's rename and its
  URL move are separate operations, and the URL move always records a 301 (CT-3).
* No bulk "reassign all articles from category A to B" operation; deleting a category in use is
  refused and the editor reassigns manually.

## Testing status

* `dotnet test` — **298 passing**: Content & Identity (188), Learning (77), Media (29),
  architecture-fitness (4). Covers slug-change/redirect, scheduled publishing, the CT-6 draft-leak
  regressions, curriculum invariants, segmented search, and the LN-6 completion rule from both
  directions.
* `pnpm test` — **90 passing** across the frontend workspaces: block renderer, embed allowlist,
  inline rich text (marks, unsafe hrefs, XSS), math, code output, nested-block depth capping, the
  primitives' accessibility contracts, and the API client (Vitest).
* `pnpm typecheck` — clean across all five workspaces.
* Integration tests require Docker (Testcontainers spins up PostgreSQL).
* `scripts/dev-smoke.ps1` — 10-step end-to-end check against a running stack; passes in both run
  modes (host-run API and fully containerised).
* `scripts/dev-seed-article.ps1` — publishes a demo article exercising every block type (plus an
  unknown one) so the renderer can be checked against real data.

## Open questions / to be ADR'd later

* Newsletter provider (Resend vs. ConvertKit) — decide end of Phase 1. The home CTA band is a plain
  link until then: a subscribe form that silently discards addresses costs more trust than it earns.
* Playground execution strategy (client WASM vs. server sandbox) — Phase 3 ADR.
* LLM provider(s) and embedding model — Phase 3 ADR.
* Billing provider specifics — Phase 3 ADR.
* Search upgrade trigger (when to move FTS → OpenSearch).

## Risks being watched

* **Solo + in-house CMS scope creep.** Mitigation: Phase 1 CMS is articles-only; no course/quiz
  authoring until Phase 2.
* **Two-app maintenance tax.** Mitigation: shared `packages/*` in a monorepo; no duplicated
  design/auth/API logic.
