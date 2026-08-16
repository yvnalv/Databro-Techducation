# DataBro — Status

Snapshot of where the project is, what's next, and what's open. Update this with every meaningful
milestone.

Last updated: 2026-08-01.

## Current phase

**Phase 1 — Foundation & Content.** Sub-stage: **authoring loop closed → search, media and the
discovery layer next**.

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

* The full read path is proven: author in the API → published → rendered on `site` with complete SEO
  output. Picking up taxonomy, search, and media next.

## Recently done

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

1. Wire the transactional outbox + `ArticlePublished` handling (Search reindex / cache invalidation).
2. PostgreSQL FTS search; platform `sitemap.xml` / `robots.txt` / RSS; Media upload to MinIO/Spaces.
3. CI pipeline (build/test + architecture-fitness gate).

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
  and read on the public site without touching a script. Remaining gaps: no table/grid editor (table
  blocks render but have no form), no media upload, no version history or restore, and taxonomy has
  no management screen.
* **CMS tokens are not `httpOnly`.** The app sets them from JS, so it cannot be; they are
  `sameSite=strict` and `secure` outside development. The hardening is a backend-for-frontend that
  proxies login and sets cookies the browser never reads — a deliberate follow-up, not an oversight.
* Inline rich text is renderable but not yet *authorable* — the editor lands with the CMS slice.
* **Hangfire** now runs the scheduled-publish sweep (PostgreSQL storage). Redis is still provisioned
  but unused. The scheduled-publish failure "alert" is a logged error for now — it becomes a real
  notification when the Notification module lands. The sweep assumes a single job server; a
  multi-server deploy needs `DisableConcurrentExecution` before it is safe.
* Media module is still a scaffold, so image blocks render an accessible placeholder.
* **Slug changes go through a dedicated endpoint**, not the general update — a term's rename and its
  URL move are separate operations, and the URL move always records a 301 (CT-3).
* No bulk "reassign all articles from category A to B" operation; deleting a category in use is
  refused and the editor reassigns manually.
* Taxonomy has no authoring UI — terms are managed via the API until the CMS surface exists.

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
