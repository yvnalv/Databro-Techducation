# DataBro — Status

Snapshot of where the project is, what's next, and what's open. Update this with every meaningful
milestone.

Last updated: 2026-08-16.

## Current phase

**Phase 1 — Foundation & Content.** Sub-stage: **content is indexable and searchable; media, CI and
two CMS surfaces remain.**

The [ROADMAP](ROADMAP.md) exit criterion is one compound sentence, and it is not met yet:

> "an editor can author, version, schedule, and publish an SEO-complete article that is indexed,
> searchable, and served fast from the public site."

| Verb | State |
|---|---|
| author | ✅ block editor |
| version | ⚠️ versions are written on every publish, but there is **no UI to view or restore** one |
| schedule | ⚠️ the API and Hangfire sweep work; **the CMS has no scheduling control**, so an editor cannot do this |
| publish | ✅ |
| SEO-complete | ✅ — except `og:image`, which needs Media |
| indexed | ✅ sitemap, robots, feed, JSON-LD |
| searchable | ✅ PostgreSQL FTS |
| served fast | ✅ SSG/ISR |

An earlier revision of this file claimed the exit criteria were met. That read "indexed and
searchable" as the whole criterion and skipped the four verbs in front of it — corrected here.

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

* Discovery and search are done. What stands between here and the Phase 1 exit criterion is **Media**
  (the image block and `og:image` are both unusable without it), the **scheduling and version-history
  controls** in the CMS, and **CI**.

## Recently done

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

1. **Media upload** to MinIO/Spaces with responsive variants — the last unbuilt Phase 1 module, and
   the reason the editor's image block and `og:image` are both dead controls today.
2. **CMS scheduling + version history/restore** — both APIs already exist; this is UI-only work and
   it is what literally closes the exit criterion.
3. **CI pipeline** (build/test + architecture-fitness gate). 129 backend tests and 11 client tests
   exist and nothing runs them on push.
4. Staging deploy on DigitalOcean.
5. Wire the transactional outbox + `ArticlePublished` handling (cache invalidation; unblocks the
   real Search module).

## Known gaps / deferred

* Email transport not wired — email verification not yet enforced on login
  (`RequireConfirmedEmail=false`); the no-op sender logs the confirmation token.
* Social login (Google/GitHub) not yet implemented.
* **Design pass complete for what exists**, matching the reference: sampled blue palette,
  pink→violet page-header gradient, navy footer. **Light mode only** — the earlier
  `prefers-color-scheme` switch made dark-OS visitors see a dark site and has been removed. The
  reference's course grid, instructor carousel and pricing table wait for Phase 2 data.
* **Premium bodies are not actually gated yet.** The badge, preview notice, marked region and JSON-LD
  paywall declaration are in place, but the full body still renders: there is no entitlement check to
  gate on until Billing (Phase 3). Reserved, not enforced.
* **Syntax highlighting is not wired.** Code blocks emit the standard `language-*` markup so a
  highlighter drops in later without touching page code.
* **Authoring UI works end to end.** Sign-in, route guard, dashboard shell, article list, and a
  block editor with Tiptap rich text and live preview. An article can be written, saved, published
  and read on the public site without touching a script. Taxonomy has a management screen too. Remaining gaps: no
  table/grid editor (table blocks render but have no form), no media upload, and no version history
  or restore.
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

* `dotnet test` — 106 passing: architecture-fitness (4) + Content & Identity unit/integration (102),
  including the slug-change/redirect and scheduled-publishing domain + API suites.
* `pnpm --filter @databro/ui test` — 59 passing: block renderer, embed allowlist, inline rich text
  (marks, unsafe hrefs, XSS), math, code output, nested-block depth capping, and the primitives'
  accessibility contracts (Vitest).
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
