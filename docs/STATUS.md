# DataBro — Status

Snapshot of where the project is, what's next, and what's open. Update this with every meaningful
milestone.

Last updated: 2026-07-30.

## Current phase

**Phase 1 — Foundation & Content.** Sub-stage: **content + taxonomy render publicly → search, media,
and scheduled publishing next**.

## Done

* Locked the load-bearing architecture decisions (ADR-0001 … ADR-0008).
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

1. Slug-change 301 redirects (`redirects` table + lookup + site honoring), covering articles and
   taxonomy together. Blocks renaming a mis-slugged term, so it is the natural follow-on.
2. Scheduled publishing (`scheduled_for` exists; needs Hangfire and rule CT-7).
3. Wire the transactional outbox + `ArticlePublished` handling (Search reindex / cache invalidation).
4. PostgreSQL FTS search; platform `sitemap.xml` / `robots.txt` / RSS; Media upload to MinIO/Spaces.
5. CI pipeline (build/test + architecture-fitness gate).
6. Design system pass over the site (deferred deliberately — see below).

## Known gaps / deferred

* Email transport not wired — email verification not yet enforced on login
  (`RequireConfirmedEmail=false`); the no-op sender logs the confirmation token.
* Social login (Google/GitHub) not yet implemented.
* **Design pass is half done.** The token architecture (semantic colours, dark mode, type scale,
  spacing) and the *article reading experience* are in. Still outstanding: the palette and typeface
  values, and the marketing/listing layout — both need the LearnUp reference, which is a
  client-rendered SPA that cannot be read programmatically, so it needs screenshots.
* **Premium bodies are not actually gated yet.** The badge, preview notice, marked region and JSON-LD
  paywall declaration are in place, but the full body still renders: there is no entitlement check to
  gate on until Billing (Phase 3). Reserved, not enforced.
* **Syntax highlighting is not wired.** Code blocks emit the standard `language-*` markup so a
  highlighter drops in later without touching page code.
* **No authoring UI.** Articles and taxonomy are API-only; `apps/app` is still a stub. This is the
  binding constraint on content production and the reason the CMS editor is next.
* Inline rich text is renderable but not yet *authorable* — the editor lands with the CMS slice.
* Redis and Hangfire are provisioned but nothing in the backend uses them yet.
* Media module is still a scaffold, so image blocks render an accessible placeholder.
* **Category and tag slugs are immutable**, like article slugs — renaming a term's display name works,
  but changing its URL waits for the redirects slice (CT-3).
* No bulk "reassign all articles from category A to B" operation; deleting a category in use is
  refused and the editor reassigns manually.
* Taxonomy has no authoring UI — terms are managed via the API until the CMS surface exists.

## Testing status

* `dotnet test` — 69 passing: architecture-fitness (4) + Content & Identity unit/integration (65).
* `pnpm --filter @databro/ui test` — 45 passing: block renderer, embed allowlist, inline rich text
  (marks, unsafe hrefs, XSS), math, code output, and nested-block depth capping (Vitest).
* `pnpm typecheck` — clean across all five workspaces.
* Integration tests require Docker (Testcontainers spins up PostgreSQL).
* `scripts/dev-smoke.ps1` — 10-step end-to-end check against a running stack; passes in both run
  modes (host-run API and fully containerised).
* `scripts/dev-seed-article.ps1` — publishes a demo article exercising every block type (plus an
  unknown one) so the renderer can be checked against real data.

## Open questions / to be ADR'd later

* Exact content-block type catalog (finalize before CMS build — see [CONTENT_MODEL.md](CONTENT_MODEL.md)).
* Newsletter provider (Resend vs. ConvertKit) — decide end of Phase 1.
* Playground execution strategy (client WASM vs. server sandbox) — Phase 3 ADR.
* LLM provider(s) and embedding model — Phase 3 ADR.
* Billing provider specifics — Phase 3 ADR.
* Search upgrade trigger (when to move FTS → OpenSearch).

## Risks being watched

* **Solo + in-house CMS scope creep.** Mitigation: Phase 1 CMS is articles-only; no course/quiz
  authoring until Phase 2.
* **Two-app maintenance tax.** Mitigation: shared `packages/*` in a monorepo; no duplicated
  design/auth/API logic.
