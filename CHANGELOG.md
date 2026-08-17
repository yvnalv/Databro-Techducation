# DataBro Changelog

## [2026-08-17 07:16:46 UTC]

CHG-0032 — `LessonContent`: a lesson body beside articles (ADR-0012, step 2)

The first slice where the `ContentUnit` seam carries weight. A lesson body is now a real aggregate
sharing the engine with `Article` — same blocks, same versioning, same publish path — in its own
table, so no query over articles can return one.

- **`LessonContent` is deliberately almost empty.** It *is* the engine. No author byline, no category
  or tags, no SEO metadata, no locale: a lesson is discovered through its course, not as a standalone
  indexed page, and giving it those fields would invite exactly the confusion this design prevents.
  Learning's `Lesson` will hold the objectives, difficulty and ordering.
- **No search vector on the table either.** Indexing lesson bodies into the vector that feeds
  `/api/v1/search` would put them in public article results. Making lessons findable is its own
  decision, tied to the outbox (ADR-0010).
- **`ContentVersion` became abstract** with `ArticleVersion` and `LessonContentVersion` beneath it.
  A single shared version table is not possible: two owner tables cannot share one foreign-key
  column, so the relationship EF needs to load history through an aggregate could not be expressed.
- **`IContentSlugRegistry`** enforces slug uniqueness across both tables on the write path. A unique
  index cannot span tables — this is the one cost of separate tables, and it is paid in one guard
  rather than as a predicate repeated on every read path.
- **Four isolation tests assert a structural property, not a predicate.** A published lesson 404s at
  `/api/v1/articles/{slug}`, is absent from the listing that feeds the homepage, sitemap and RSS, and
  returns nothing from search — because it is not in that table. Under the rejected discriminator
  these would have been tests of something a developer must remember to write.

Three things the mapping fought back on, each now commented where it bites:

- **Keys and query filters belong on the hierarchy root.** EF rejects both on a derived type, so the
  engine's columns moved to a `ContentUnitConfiguration` — which is the better place anyway, since
  every content unit table carries exactly those columns and defining them twice would let the two
  drift. `ApplySoftDeleteQueryFilter` now skips derived types; before this hierarchy existed, every
  entity was its own root and the loop was free to filter each in turn.
- **`Include(a => a.Versions)` stopped working.** `Versions` is a computed projection over each
  type's own list, not a navigation, so the repository includes the backing field instead.
- **The migration needed hand-editing.** EF emitted `DropPrimaryKey` + `AddPrimaryKey` for what is
  only a change of constraint *name*, and PostgreSQL refuses to drop a primary key that foreign keys
  depend on — so it failed outright, taking 77 tests with it. Replaced with `RENAME CONSTRAINT`,
  same end state, dependents untouched. The names go PascalCase because the naming convention cannot
  derive a table name for a key on an abstract type; recorded in DATABASE.md as the cosmetic wart it
  is rather than fought.

Verified on the live database after migrating: both tables present, all 35 articles and 36 version
rows intact through the `article_id` → `content_unit_id` rename, and every public surface unchanged.
Backend 192 green including the architecture-fitness gate.

---

## [2026-08-17 06:57:55 UTC]

CHG-0031 — Extract the content engine into `ContentUnit` (ADR-0012, step 1)

First Phase 2 slice, and deliberately a **pure refactor**: no new behaviour, no data movement, the
existing 180 tests as the safety net. Nothing depends on it yet, which is the point — the seam is
proven before a lesson is built on it.

- **[ADR-0012](docs/adr/0012-lesson-bodies-live-in-content.md) accepted.** Lesson bodies get their
  own table in Content beside articles, sharing the engine through an abstract base class. The
  alternative — one table with a `kind` discriminator — was rejected because every public article
  read path would then need a `kind = Article` predicate, and a missing one puts a lesson in the RSS
  feed. That is the **same shape as the CT-6 leak fixed two days ago**; buying it out by construction
  is worth a second table. Renaming `articles` → `content_units` was also rejected: 359 references
  across 38 backend files plus a published `/api/v1/articles` contract, spent on naming purity
  before a single Lesson exists.
- **`ContentUnit`** now owns everything true of any versioned, publishable body: the block pair, the
  CT-6 published snapshot, version history, and the draft → scheduled → published state machine.
  **`Article`** keeps only what a standalone discoverable page has — byline, taxonomy, SEO, locale.
- **Domain events are a hook, not a base-class concern.** `Publish()` performs the transition and
  calls `OnPublished()`, which each type overrides to raise its own event. A base raising
  `ArticlePublished` for a lesson would be a lie, and once the outbox exists it would be a lie
  delivered to every subscriber.
- **`ArticleVersion` → `ContentVersion`** so history belongs to the engine. Internal to Content: the
  version endpoints return DTOs, so `ArticleVersionDto` and the public shape are untouched.
- **Table-per-concrete-type mapping.** Not TPH, which would put both types in one table and
  reintroduce exactly the leak this design prevents; not TPT, which would add a join to the hottest
  read path for nothing.
- Two things the refactor taught, both now commented at the point they bite: EF resolves an explicit
  `HasField` against the *derived* type, so naming `_versions` on the `Article` builder fails once
  the field moves to the base — convention finds it correctly. And the schema diff is a single
  **foreign-key rename**, no column or table movement, which is the evidence that this was a
  refactor rather than a redesign.
- Verified on the running stack after migrating: public read, version history, search, and a full
  unpublish → publish round trip appending version 2. Backend 180 green including the
  architecture-fitness gate.

---

## [2026-08-16 17:33:43 UTC]

CHG-0030 — Polish pass: syntax highlighting, table editor, dev login, ESLint

Four gaps that were each visible every time the app was opened locally.

### Syntax highlighting (Shiki, server-side)

- Code blocks emitted `language-*` markup and nothing highlighted it. On a site teaching Python and
  SQL that was the most visible quality gap in the product.
- **Highlighting runs on the server; the renderer does a lookup.** Shiki carries TextMate grammars
  for every language it supports, and shipping those so a reader can look at a page we already
  rendered is several hundred kilobytes for nothing. Verified against the built image: the client
  bundle contains no Shiki and no Oniguruma; the server bundle does.
- The article page now fetches through the site's own Nitro route (`/api/articles/[slug]`) rather
  than calling the backend directly. Highlighting inside the page's own `useAsyncData` would only
  have covered the first render — the handler re-runs in the browser on client-side navigation,
  where Shiki deliberately does not exist — so code would have been highlighted on reload and plain
  when followed from a link.
- Keyed by an FNV-1a hash of code + language, so the payload does not carry every sample twice.
- **`codeKey` lives in its own dependency-free module** exported as `@databro/ui/code-key`.
  Importing it from the package root pulled every `.vue` component into the Nitro server bundle,
  which Rollup cannot parse — the site failed to build at all until this was split out.
- Unknown languages and any sample Shiki throws on fall back to plain text rather than rendering
  empty.

### Table block editor

- The last block type that rendered but could not be authored. A grid of rich-text cells, with
  add/remove row and column.
- **Every mutation keeps the grid rectangular**, and reads repair a ragged one. The renderer maps
  headers to `<th scope="col">` and each row's cells positionally, so a row one cell short silently
  shifts every value after it into the wrong column — and the stored shape is free-form JSONB, so
  the editor is the only place that can guarantee it.

### Seeded development administrator

- `admin@databro.local` / `Databro-Dev-1!`, seeded on startup. The only way into the CMS was an
  account created by a script whose address was a timestamp, so signing in meant going to find the
  string first.
- Gated on `IsDevelopment()` **at the call site**, not inside the seeder, so the decision is visible
  where it is made — a seeded admin with a documented password is a back door anywhere else.
  Idempotent, and it does not reset an existing password.

### ESLint

- There was no linter config anywhere in the repo, so the root `lint` script was a no-op and CI had
  nothing to run. Flat config at the frontend root, wired into CI, and the workspace is clean.
- **`vue/no-v-html` escalated from the recommended `warn` to `error`.** This renderer's entire
  security posture is that authored content becomes elements, never markup — a warning that still
  lets CI pass is not a guard. Verified in both directions: clean as-is, and a new `v-html` in a
  third component fails the run.
- The sanctioned uses (KaTeX and Shiki) carry an inline disable with a reason at the line rather
  than being exempted by filename, so the justification sits where a reviewer will read it.
- Deliberately **not** type-aware linting: it needs a TS program per package, roughly triples the
  run time, and mostly duplicates what `pnpm typecheck` already enforces.

### Also

- **Fixed the frontend containers failing to start.** When workspace dependencies drift, pnpm wants
  to confirm before purging `node_modules`, finds no TTY, and aborts — which took the CMS container
  down on boot. `CI=true` in the dev image is pnpm's own answer for non-interactive environments.
- Backend 180 tests green, frontend 85, lint clean, typecheck clean across five workspaces.

---

## [2026-08-16 16:30:59 UTC]

CHG-0029 — CI, and a broken homepage caught on its way to production

- **`.github/workflows/ci.yml`** — the repo had 261 tests and nothing running them on push. Three
  jobs: `backend` (Release build + all 180 tests, including the architecture-fitness rules) and
  `frontend` (frozen-lockfile install, typecheck, 81 tests) in parallel, then `images`.
- The fitness rules are why this gate matters most: a module-boundary violation compiles perfectly
  and is invisible in review.
- **Every CI command was run locally before committing**, which caught two mistakes that would have
  failed the first run: `pnpm -r --if-present exec` is not valid (`--if-present` applies to `run`,
  not `exec`), and a fixed `LogFileName` made all three test projects overwrite each other's TRX,
  leaving one file that looks like a complete result and is not.
- CI verifies `.nuxt/tsconfig.json` exists before typechecking. Without it the app typechecks pass
  **vacuously** — worse than not running them, because they report success — and that is exactly how
  a route rule that was never valid shipped once before.

**The find:** the production site image shipped a permanently broken homepage.

- `routeRules: { "/": { prerender: true } }` means `nuxt build` renders the homepage at image-build
  time. No API is reachable then, so the HTML baked into the image was the "we could not load the
  articles right now" fallback containing **zero article links** — and a prerendered page is never
  re-rendered, so it would have served that until the next deploy.
- This had never been exercised: the containers run the `dev` target, so nothing had ever built the
  production image. It was found by building it deliberately while wiring the `images` job, then
  extracting `.output/public/index.html` and reading it.
- Fixed by moving `/` to `isr: 600`, joining the article, category and tag routes. That also fixes a
  second bug hiding behind the first: a prerendered homepage would never show a newly published
  article until someone redeployed.
- CI now fails if any prerendered HTML appears in the site image. Nothing here can be correctly
  prerendered at image-build time — every page's content comes from the API. The guard was tested
  in both directions: it passes on the fixed image and trips on a deliberately poisoned one.
- Also recorded in DEPLOYMENT: auto-migration is `IsDevelopment()`-gated, so a deployed environment
  will not migrate itself and the deploy's migration step is the only thing that will apply schema
  changes.
- Not in CI and now explicit in STATUS: **no linter exists in the repo**, so the root `lint` script
  is a no-op; and no vulnerability scan.

---

## [2026-08-16 16:19:52 UTC]

CHG-0028 — Scheduling and version history in the CMS, and a draft-content leak closed

Meets the Phase 1 exit criterion: an editor can now author, version, schedule and publish an
SEO-complete article that is indexed, searchable and served fast.

- **This was scoped as "UI-only work" and was not.** There was no endpoint to read or restore a
  version — history rows had been written since the first publish and were unreachable — and no way
  to cancel a schedule once set, because `/unpublish` only accepts a *published* article. Scheduling
  was a one-way door.
- **New endpoints:** `GET /versions`, `GET /versions/{n}`, `POST /versions/{n}/restore`, and
  `POST /unschedule`.
- **Restore sits behind `Content.Edit`, not `Content.Publish`.** It copies a snapshot into the draft
  and changes nothing a reader sees until someone publishes afterwards — which appends a *new*
  version rather than reverting the sequence (CT-8). An Author undoing their own work in progress
  should not need the publishing permission. Scheduling and unscheduling are the reverse: both are
  publishing acts (CT-4).
- **Fixed a CT-6 leak that a test caught, and that predates this work.** `title` and `summary` were
  single columns shared by the draft and the published copy — as if `published_blocks` had never been
  split from `draft_blocks`. Editing a published article's draft title changed the **live page, the
  listings, the sitemap, the RSS feed and the search index** the moment it was saved. A half-written
  headline went public as it was typed. Reproduced on the running stack before fixing, not just in a
  test.
- The fix is `published_title` / `published_summary`, snapshotted on publish exactly like the blocks.
  The generated search vector now indexes those columns, and **the fuzzy search fallback was leaking
  independently** — it matched `word_similarity` against the draft `title`, so a draft headline was
  findable through the typo path even once full-text correctly refused to index it. Caught by the
  third regression test rather than by reading the code.
- Migration `AddPublishedTitleAndSummary` backfills `published_title = title` for every row with a
  `published_at`. That copy is correct precisely because, until this migration, the draft title *was*
  the published title — there was nowhere else for it to live.
- CMS: a schedule control that saves the draft first (so what goes live next Tuesday is what you
  see), a cancel button, and a collapsible version-history panel loaded on demand. Restore asks for
  confirmation — it is the one action here that can discard unsaved work; publishing saves rather
  than discards, so it does not.
- Also corrects STATUS again: the previous entry had marked scheduling and version history as
  "UI-only work remaining", which understated them. Both the earlier overstatement and this
  correction stay on the record.
- Tests: 8 domain tests (restore semantics, reading-time recomputation on restore, cancel-schedule
  guards), 14 API tests including three regressions for the leak — public detail, public listing, and
  search — and the authorization split between Edit and Publish. Backend 180 green; frontend 81
  green; clean typecheck. Verified against the running stack: restored a version and confirmed the
  public page did not move.

---

## [2026-08-16 13:48:11 UTC]

CHG-0027 — Media module: upload, storage and responsive images (ADR-0011)

The last unbuilt Phase 1 module. It closes two features that shipped dead: the editor's image block
had no way to get an image, and `og:image` could never be set, so every share card fell back to
nothing.

- **ADR-0011** covers three decisions that constrain each other: where bytes live, how variants are
  produced, and what we accept from an authenticated uploader.
- **One S3 adapter for both environments.** MinIO in development, DigitalOcean Spaces in production —
  they differ by endpoint and credentials, not by API, so the upload path is exercised locally
  exactly as it runs live.
- **Images are re-encoded before anything is stored, and that is the security decision.** Validating
  an upload and then storing the original still stores the original: a polyglot file that is a valid
  JPEG *and* a valid HTML document passes every header check and is then served from our domain.
  Decoding to pixels and re-encoding cannot carry the non-image portion, because it was never pixels.
  A test asserts exactly this against a real PNG with a `<script>` tag appended.
- **EXIF, IPTC and XMP are stripped.** Privacy, not size: a phone photo routinely carries GPS
  coordinates, and an author dragging one into the editor is not consenting to publish their
  location.
- **Format comes from magic bytes.** The `Content-Type` header and the filename extension are both
  attacker-controlled. Verified against the live endpoint: an executable renamed `cat.jpg` is
  refused, and so is an SVG — SVG is XML, executes script, and cannot be neutralised by re-encoding.
- **Decompression bombs are caught on header dimensions, before any decode.** The byte limit does not
  catch them at all: a 100 MB flat-colour PNG is a few hundred KB compressed and roughly 14 GB
  decoded. Caps are 10 MB, 12,000px per side, and 50 megapixels — the last catches the shape the
  per-side cap misses (11,000 × 11,000 passes it and is still 121 megapixels).
- **Storage keys are generated**, never derived from the client's filename. A test uploads
  `../../../etc/passwd.php` and asserts the key contains none of it.
- **Variants run in a Hangfire job** so the upload request returns at once. An asset is usable at
  full size while `Pending` and gains a `srcset` when `Ready`; a failed resize leaves the original
  serving rather than costing an author their upload. Never upscales — a 640px original does not get
  sharper by being written out at 1920px.
- **Content resolves media ids through `IMediaDirectory` in Platform**, the same cross-module pattern
  as `IUserDirectory` (ADR-0008), and ships a resolved map with the article DTO. Resolution is a
  lookup rather than a request per figure on the cached read path.
- Site: real `srcset`/`sizes`, intrinsic `width`/`height` to prevent layout shift, and a working
  `og:image` with `twitter:card` following what is actually available plus JSON-LD `image`.
- CMS: an upload-or-choose picker, with a session cache so a just-uploaded image renders in the live
  preview instead of waiting for save-and-reload.
- **Two schema details worth recording.** The unique indexes on `media_variants(media_asset_id, name)`
  and `media_assets(storage_key)` are filtered on `is_deleted = false`, because deletes here are soft
  and a regenerated variant would otherwise collide with its own tombstone. And `SetVariants`
  reconciles in place rather than clear-and-re-add, matching `Article.SetTags` — the job retries, so
  it must converge rather than accumulate.
- Also corrects STATUS, which had claimed the Phase 1 exit criteria were met: that read the criterion
  as "indexed and searchable" and skipped the four verbs in front of it. Scheduling and version
  history have working APIs but no CMS controls, so an editor still cannot do either.
- Tests: 29 Media tests (sniffing, polyglot, EXIF, bomb, key generation, variant convergence, failure
  handling), 4 renderer tests for `srcset` behaviour. Backend 158 green including the
  architecture-fitness gate; frontend 81 green; clean typecheck across five workspaces. Verified
  end to end on the running stack: upload → re-encode → three variants → published article rendering
  a responsive image with a working share card.

---

## [2026-08-16 08:07:46 UTC]

CHG-0026 — PostgreSQL full-text search (ADR-0010)

Closes the second Phase 1 exit criterion. Content is now both indexable and searchable.

- **ADR-0010: search lives in Content, not the Search module** — a deliberate departure from
  ADR-0006, which specified a `Search`-owned `search_documents` table fed by integration events.
  That design needs a **transactional outbox that does not exist**; without one, "publish an article"
  and "update the search row" are two writes with no atomicity between them, and the first partial
  failure leaves an index that silently disagrees with the catalogue. A wrong search index is worse
  than a slow one. ADR-0006 is annotated, not rewritten; its core choice still stands.
- **The index is a generated column, and that is the whole point.**
  `search_vector tsvector GENERATED ALWAYS AS (…) STORED` weights title **A**, summary **B**, body
  **C**. PostgreSQL recomputes it on every write, so it *cannot* fall out of step with the row —
  no reindex job, no drift, nothing to operate.
- **Per-locale stemming** via `CASE WHEN locale = 'id' THEN 'indonesian' ELSE 'english' END`, so
  "belajar" and "pembelajaran" collapse to one stem. Both branches are literal `regconfig` casts
  because only `to_tsvector(regconfig, text)` is `IMMUTABLE` — the one-argument form reads a session
  setting, which a generated column may not depend on. Queries pick the same configuration, so the
  query is stemmed the way the index was.
- **`word_similarity`, not `similarity`, for the typo fallback.** Whole-string similarity divides by
  the title's length: "Retreival" against "Retrieval-Augmented Generation, End to End" scores 0.14
  and matches nothing, however obvious the typo. `word_similarity` scores the best matching run of
  words inside the title — 0.43 for the same pair. Caught by testing a real typo against real data,
  not by the first test, which accidentally used a query that was nearly the whole title.
- **No trigram index, deliberately.** A `gin_trgm_ops` index answers the `<%` operator, whose
  threshold comes from a session GUC (0.6) — too strict for the typos the fallback exists for. An
  index the only query touching it cannot use is pure write cost, so it was removed rather than left
  in to look thorough. The scan is documented in STATUS as an OpenSearch trigger.
- **`websearch_to_tsquery`, not `to_tsquery`** — a public search box must not 500 because someone
  typed a stray ampersand or an unclosed quote.
- **`matchMode` in the response meta**, surfaced in the UI as "No exact matches — showing articles
  with similar titles." Presenting approximations as exact is how a search box stops being trusted.
- **The body projection is written in C#** (`ContentText`), because the body is typed JSONB that SQL
  cannot meaningfully flatten. Writing it uncovered that **reading-time estimation had been reading
  `data.text` directly since before ADR-0009** — every rich-text paragraph counted as zero words, so
  long articles reported "1 min read". Both now share one extractor; a regression test pins it.
- **Backfill on startup** for articles published before the column existed. Idempotent and
  self-limiting; ids are collected first so an article whose body genuinely projects to nothing
  cannot be selected forever.
- Site UI at `/search`: a real `<form method="get">` that works before hydration and produces a
  shareable URL. `noindex, follow` as both a meta tag and an `X-Robots-Tag` header, plus a robots.txt
  disallow — belt and braces, because a crawler that obeys the disallow never sees the tag.
- Fixed `PaginationNav`, which hardcoded `?page=` and would have produced `/search?q=rag?page=2`.
- Tests: 10 search integration tests against a **real PostgreSQL container** (ranking, stemming,
  locale scoping, drafts excluded, malformed input, both fallback cases), 9 extractor unit tests,
  3 client tests. Full backend suite 129 green; api-client 11 green; clean typecheck.

---

## [2026-08-16 07:41:21 UTC]

CHG-0025 — Discovery layer: robots.txt, sitemap.xml, RSS

- **These moved from the API to the `site` app, and the docs were wrong.** `docs/SEO.md` §1 assigned
  the site-wide artifacts to **Platform** and `docs/API_SPEC.md` listed `/sitemap.xml`, `/robots.txt`
  and `/api/v1/feed.rss` as API endpoints. A crawler fetches `https://databro.id/robots.txt` — from
  the host that answers for that origin, which is the site, not the API. Both documents now record
  the correction rather than being quietly rewritten. The API still owns the data; the site reads it
  through the existing public listings.
- **`sitemap.xml`** — home, every published article (`lastmod` from `publishedAt`), every *populated*
  category, and every tag. Each URL is emitted once per locale with `xhtml:link` alternates for the
  full set plus `x-default`: listing only English would leave `/id/*` undiscovered, and listing both
  without alternates would read as duplicate content instead of translations. Empty categories are
  omitted for the same reason the home tiles omit them.
- **`robots.txt`** — permissive by design, with one disallow: `/*?page=`. Paginated listings past the
  first page are thin and near-duplicate, and every article on them is in the sitemap anyway. The
  authoring app is a separate origin carrying its own `X-Robots-Tag`, so it needs no entry.
- **`feed.xml`** — RSS 2.0, latest 25 articles, **English only and deliberately so**: a channel
  declares one `language`, so a mixed feed hands every subscriber half their items in a language
  they did not ask for. Summaries only, never rendered bodies — the body is typed blocks, and
  rendering it to feed HTML would mean a second renderer to keep in step with the real one. `guid`
  is the permalink, which is only safe because slugs are immutable once published (CT-2).
- **XML escaping is not decoration.** One article title containing `&` produces a malformed document
  that a crawler rejects wholesale — a single bad title would take the entire sitemap or feed
  offline. Each section is also independently fault-tolerant: a failing fetch drops that section
  rather than 500ing the document, because a partial sitemap still indexes most of the catalogue.
- Feed autodiscovery `<link>` in the site head plus a footer link, in both locales.
- Verified against the containerised stack: 74 sitemap URLs and 25 feed items, both parsing as
  well-formed XML, correct content types, and article `canonical`/`hreflang` tags unaffected by the
  new global head link.
- Known limit, recorded in STATUS: the sitemap pages the public listing 100 at a time (cap 50 pages).
  Fine now, wrong at ten thousand articles — that needs a bulk `lastmod` endpoint and a sitemap index.

---

## [2026-08-16 07:35:00 UTC]

CHG-0024 — Taxonomy management in the CMS

- Categories and tags are now managed from `/taxonomy` instead of only through the API. One screen
  for both: they are the same job — curating the vocabulary articles are filed under — and splitting
  them would have produced two half-empty pages.
- Categories show their **published article count** and their parent, so the hierarchy is visible
  where it is edited rather than only on the public site.
- **Slug changes are deliberately absent from the edit form.** A term's slug is a live public URL;
  moving it is a separate act the API pairs with a 301 (CT-3), and the client method exists
  (`changeCategorySlug` / `changeTagSlug`) but wiring it into the general edit form would make a
  rename silently move a URL. The create form says the slug is immutable afterwards.
- **TX-2 surfaces properly:** deleting a category that still classifies articles is refused by the
  API with the count, and the screen shows that message rather than a generic failure. Verified
  against the running stack — "This category still classifies 1 article(s)."
- Every mutation runs through one helper so success and failure are reported the same way, and the
  list refreshes from the server rather than being patched locally — the server is the authority on
  what the counts and hierarchy now are.
- Also fixes the dead `/taxonomy` link the sidebar has been pointing at since the shell was built,
  which was logging a router warning on every dashboard render.
- Verified: created, renamed and deleted a tag; confirmed a rename leaves the slug untouched;
  confirmed the category delete guard refuses with its count. Clean typecheck across five workspaces.

---

## [2026-08-16 07:30:00 UTC]

CHG-0023 — Block editor: the authoring loop closes

An article can now be written, saved, published and read on the public site **without touching a
script**. That was the binding constraint on the whole project since Phase 1 began.

- **Tiptap for inline rich text, and the conversion is three lines.** That is ADR-0009 paying for
  itself: DataBro stores inline content in ProseMirror's own shape, so "converting" is wrapping the
  node array in a document and unwrapping it — no mark translation, no offset remapping, no lossy
  round trip. Offset-range marks would have made this component the translation layer the ADR existed
  to avoid.
- Tiptap is configured with headings, lists, blockquotes and code blocks **off**. Block structure is
  DataBro's content model, not the editor's; letting Tiptap create a heading inside a paragraph block
  would produce a document the renderer cannot represent. Its link extension is restricted to
  http/https, mirroring the renderer's allowlist so an unsafe scheme cannot survive a round trip.
- **Block editor** for all eleven types: add, reorder, delete, and per-type forms. Reordering is
  buttons rather than drag-and-drop — buttons are keyboard-operable and announced for free, where
  drag needs a parallel keyboard affordance to be usable at all. Drag is an enhancement on top, never
  a replacement.
- **Live preview runs through the same `ContentRenderer` as the public site.** That shared registry
  was built so preview and production cannot drift (ADR-0007), and this is the first time the claim
  is actually exercised. Preview passes `showUnknownBlocks`: an author must see that a block exists
  even when this build cannot render it, a reader must not.
- The slug field **locks once the article has been published** (CT-2) rather than silently doing
  nothing, since moving a public URL is a separate deliberate act that pairs with a 301 (CT-3).
- Publish saves first: it snapshots the *saved* draft, so an unsaved edit would otherwise be silently
  left out of what goes live.
- Taxonomy is always sent as an explicit value, never omitted — the API reads an absent field as
  "leave unchanged", so clearing a category has to be a deliberate null.
- Verified the whole loop against the running stack: created an article with bold and link marks
  through the editor's exact payload, published it, and confirmed it renders on the public site with
  both marks intact and the link carrying `nofollow noopener noreferrer`. 110 backend tests, 76
  frontend, clean typecheck across five workspaces.

---

## [2026-08-16 07:15:00 UTC]

CHG-0022 — Fix two bugs that only a browser could hit: fetch binding and missing CORS

CMS sign-in failed with a generic error. Two independent faults, both invisible to every check made
so far, because **every previous call reached the API from the server during SSR** — this was the
first genuine browser-to-API request in the project.

- **`fetch` lost its receiver.** `ApiClient` stored `globalThis.fetch` and later invoked it as
  `this.fetchImpl(...)`, making `this` the client instance. A browser requires `fetch` to be called
  on the global and throws `TypeError: Illegal invocation`; **Node's `fetch` does not care**, so
  every server-rendered call worked. Fixed with `.bind(globalThis)`. Because the thrown value was a
  `TypeError` rather than an `ApiClientError`, it fell through to the generic catch — which is why
  the form said "sign-in failed" instead of anything about credentials.
- **The API had no CORS policy at all.** The preflight answered `405` with no
  `Access-Control-Allow-Origin`, so the browser blocked the request before it was sent. Origins now
  come from configuration and are never wildcarded: `AllowAnyOrigin` would let any site call the API
  with a user's bearer token from a script it controls. The base config ships **empty**, so a
  deployed environment must state its own rather than inherit localhost.
- `UseCors` sits **before** authentication so a 401 still carries CORS headers; without that the
  browser reports an opaque CORS error and the app can never see the real status. Credentials are
  deliberately not allowed — auth travels as a bearer header, not a cookie.
- **Tests now cover the gap that let this through.** Eight `@databro/api-client` tests, including one
  that installs a `fetch` which throws unless its receiver is the global, reproducing the browser's
  rule where it can actually be asserted — it fails against the old code. Four API tests cover the
  preflight, the allow header on real responses and on a 401, and refusal of an unlisted origin.
- Note for contributors: adding a dependency changes the lockfile and crash-loops the containers,
  which carry `node_modules` in the image. Rebuild, do not restart — as LOCAL_DEVELOPMENT.md says.

---

## [2026-08-16 05:55:00 UTC]

CHG-0021 — CMS foundation: authentication, dashboard shell, article list

First half of the authoring UI. `apps/app` was a stub that rendered "Learner app"; it now signs a
user in and lists articles. The block editor is the next slice.

- **Session** in a `useAuth` composable: cookie-backed (not `localStorage`, because SSR has to read
  the token — otherwise every authenticated page renders empty and re-fetches on the client), with
  refresh-on-401-and-retry. Only a 401 triggers a refresh: a 403 means the token is fine and the role
  is not, so refreshing would loop without ever fixing it.
- **Cookies are not `httpOnly`** — the app sets them from JS, so they cannot be. Mitigated with
  `sameSite=strict`, `secure` outside development, a short access-token lifetime, and the renderer's
  refusal to inject author content as HTML. The real hardening is a backend-for-frontend that proxies
  login and sets cookies the browser never reads; recorded in STATUS as a deliberate follow-up rather
  than left as a silent gap.
- **Global route guard**, not per-page: the CMS is authenticated by default, so forgetting a
  `definePageMeta` must not expose a page. It is a UX guard, not a security boundary — the API
  enforces permissions on every request. It also probes the session, so an expired cookie lands on
  the login screen instead of a page full of failed requests, and carries the intended destination
  through login.
- **Login** rejects with an identical message for a wrong password and an unknown address, so the
  form is not an account-enumeration oracle, and only accepts same-origin redirect targets.
- **Dashboard shell** — sidebar + main, per the reference, minus its gradient band and overlapping
  profile card: the CMS is a tool, and that flourish costs vertical space where density matters.
- **Article list** reads the *authoring* endpoint (every status), distinct from the public listing
  (published only, cached, indexable). Dark table header — the one sanctioned use of it
  (DESIGN_SYSTEM §5.7). Status chips carry the status word as well as the tint.
- Stat cards label their own scope (`this page` vs `all pages`) rather than deriving a total from one
  page and presenting it as a total, which would be wrong the moment there is a second page.
- The CMS reuses the site's tokens, fonts and `@databro/ui` primitives — it is a different surface,
  not a different product, and a second design language would be a second thing to maintain.
- Verified in the containerised stack: unauthenticated `/` 302s to `/login?redirect=/`, and an
  authenticated request server-renders the dashboard with real rows, the signed-in user, stat cards
  and pagination. 68 frontend tests, clean typecheck, both apps build.

---

## [2026-08-16 05:40:00 UTC]

CHG-0020 — Two-column reading layout with a table of contents

- **The article page now uses the width without stretching the text.** A centred 68ch column looked
  narrow on a large display, but widening it was the wrong fix: Bringhurst and webtypography.net put
  the comfortable measure at **45–75 characters**, Baymard finds comprehension falls off past ~80,
  and **WCAG 2.1 caps line length at 80**. Filling a 1870px viewport would have put the column near
  100 characters — outside both the research range and the accessibility guidance.
- The DataCamp reference given as the target is wide for a different reason: it runs **two columns**
  (a ~865px reading column plus a ~250px sidebar), not long lines. DataBro now does the same — on
  `xl` and above, the reading column keeps its measure and a **sticky table of contents** occupies
  the extra width. At a 1870px viewport the article starts at **423px** instead of 595px while every
  line stays the same length.
- The TOC lists `h2`/`h3` only, appears only with two or more headings, and highlights the section in
  view via `IntersectionObserver` (not a scroll handler, so it costs nothing per frame). Entries are
  real anchors, so they work without JS.
- **Anchor generation is now shared.** `headingAnchor` moved into `@databro/ui` and is used by both
  the heading renderer and the TOC builder. Two implementations would drift on the first odd heading
  and every contents link would scroll nowhere; a test asserts the id the TOC links to is the id the
  renderer stamps.
- A test caught a real bug while writing it: `buildToc` coerced an `h4` to level 2 rather than
  excluding it, which would have put sub-details in the contents claiming to be top-level sections.
- The reference's other sidebar content (search, categories, trending) is still **not** adopted —
  that is browsing, and it belongs below the article where a finished reader is. A contents list is
  different: it serves the article you are already in.
- Verified: 68 frontend tests (was 59), clean typecheck, production build, and live checks that the
  rendered heading ids match the TOC's links.

---

## [2026-08-16 05:15:00 UTC]

CHG-0019 — Widen the page shell; stop an unreachable API reading as an empty site

- **Widened the container.** The reference was measured rather than estimated: it runs **1220px of
  content in a 1753px viewport** (gutter 265px, ratio 0.70), consistently across the blog grid, home
  cards, element page and footer. DataBro now runs wider — fluid to a **1760px cap** with 16/24/40px
  responsive gutters — because 1200px left conspicuous dead margin on a large display. At a 1870px
  viewport content starts at 95px and spans 1680px.
- The **cap** is deliberate: fluid-to-infinity stretches card grids to absurd widths on an ultrawide.
  At 2560px the layout stops growing and re-centres.
- Container width is now a single `.db-shell` class in `tokens.css` rather than a
  `mx-auto max-w-shell px-4 sm:px-6` string repeated across eleven templates. It is the most-tuned
  value in a layout and should have one definition; all eleven usages migrated.
- **Column counts step up with the container** — listings and category tiles now go to 4 columns at
  `xl`. Without that, the extra width simply inflated three cards to ~550px banners; at 1870px the
  grid is now 402px cards, close to the reference's card size.
- The **article body is untouched** at the ~68ch measure. Widening the shell must never widen the
  reading column, which is why the two containers are separate.
- **Fixed a misleading empty state.** The homepage degraded a failed article fetch to an empty list,
  which rendered "No articles have been published yet" — indistinguishable from a genuinely empty
  site, and untrue. An unreachable API now says so. The degradation itself stays: a build-time API
  hiccup must not fail the prerender.
- Verified: geometry computed across seven viewport widths, `.db-shell` survives the Tailwind purge,
  59 frontend tests, clean typecheck, production build, and the live containerised stack.

---

## [2026-08-01 15:29:53 UTC]

CHG-0018 — Scheduled publishing via Hangfire (CT-7)

- **Articles can be scheduled to publish automatically.** `POST /api/v1/authoring/articles/{id}/schedule`
  with `{ scheduledFor }` (behind `Content.Publish`) sets a future publish time and moves the article
  to `Scheduled`. `Article.Schedule` enforces the publish preconditions up front (title + at least one
  block) and requires a future time, so a schedule can never be set on something that can't publish.
- **A Hangfire recurring sweep publishes due articles.** Runs every minute against PostgreSQL-backed
  Hangfire storage; each due article goes through the same `Publish` path as an interactive publish
  (snapshot, version row, event).
- **CT-7 failure contract honoured.** If an article can no longer publish when its time arrives (e.g.
  its draft was emptied), it stays `Scheduled` and logs an alert instead of being silently dropped —
  the next sweep retries once an editor fixes it. (The alert is a logged error until a Notification
  module exists.)
- **Hangfire is host-owned; the module owns its job.** The host stands up the server + storage; the
  Content module registers the `content:scheduled-publish` recurring job via a hosted initializer.
  Both are gated by `Hangfire:EnableServer`, which integration tests set to false and drive the job
  method directly. Hangfire manages its own `hangfire` schema (no EF migration).
- Dev-only dashboard at `/hangfire` (permissive auth in Development only; never mounted elsewhere).
- Docs updated: STATUS (slice done, next-up reordered, gaps), DATABASE (hangfire schema),
  LOCAL_DEVELOPMENT (dashboard). Verified: backend build clean, **106 tests passing** (was 94; +12
  domain, job-unit and endpoint tests for scheduling).

---

## [2026-08-01 15:02:04 UTC]

CHG-0017 — Slug-change 301 redirects for articles and taxonomy

- **A published content unit's URL can now move without breaking (CT-2/CT-3, docs/SEO.md §4).** A new
  `redirects` table in the `content` schema records a 301 from the old path whenever a slug changes,
  so an indexed URL never silently 404s.
- **Dedicated slug-change endpoints**, separate from the general update so moving a public URL is an
  explicit act: `PUT /api/v1/authoring/articles/{id}/slug` (behind `Content.Publish` — changing a URL
  is a publishing concern), and `PUT /api/v1/authoring/{categories,tags}/{id}/slug` (behind
  `Taxonomy.Manage`). Each carries `{ slug }`.
- **An article records a redirect only once it has been published**; a never-published draft has no
  indexed URL to protect and simply moves. A category or tag slug is always a live public URL, so its
  move is unconditionally paired with a 301.
- **Redirect chains are collapsed on write.** Renaming `a → b` then `b → c` repoints `a` straight to
  `c`, so a crawler never follows two hops and the public lookup needs only one.
- **Public lookup:** `GET /api/v1/redirects?from={path}` returns `{ toPath, statusCode }` or 404. The
  `site` app calls it on a content 404 and, on the server, issues a real 301 (`navigateTo` with
  `redirectCode`) — the SEO-critical path — before falling back to a genuine 404.
- The redirect is written in the **same unit of work** as the slug change, so the two commit together
  (CT-3 is atomic). The `from_path` unique index is **filtered on `is_deleted`** so a path redirected
  away, freed, then moved again does not collide with the tombstone row.
- Docs updated: API_SPEC (new endpoints), DATABASE (redirects table), STATUS (slice done, next-up
  reordered). Verified: backend build clean, **94 tests passing** (was 71; +23 domain + API redirect
  tests), frontend typecheck clean across all five workspaces.

---

## [2026-08-01 04:20:00 UTC]

CHG-0016 — Match the reference palette; remove automatic dark mode

- **Fixed: the site rendered dark for anyone whose OS was in dark mode.** The token layer keyed dark
  mode off `prefers-color-scheme`, so a dark-OS visitor saw a theme the design never intended.
  DataBro is a light-mode product; the automatic switch is gone. `[data-theme="dark"]` survives as an
  explicit opt-in for a future toggle, but nothing enables it.
- **Adopted the reference palette**, replacing the teal/violet set. Values were **sampled from the
  screenshot pixels** rather than estimated: primary blue `#0068d9`, page-header gradient
  `#e377b1 → #9274e4 → #7a73f4`, navy `#13293e` for headings and the footer, surface tint `#f6f8fd`,
  and the functional hues taken from the reference's own button set.
- **Restored the gradient page-header band.** An earlier revision replaced it with a flat band; the
  brief is to match the reference, so the gradient is back — scoped to page-header bands only, since
  it is a brand signature rather than a surface.
- Category chips are now **mint**, as in the reference, which keeps them reading as labels rather
  than competing with the blue title beneath.
- CTA band is the reference's solid brand blue with a navy pill action; the footer moved onto the
  same navy token, and the brand mark inverts to white there — blue-on-navy is the one place the mark
  loses contrast.
- Docs updated to match, including an explicit note recording *why* the teal palette was replaced, so
  the earlier reasoning is still available if DataBro ever wants visual separation from the category.
- Verified: no `prefers-color-scheme` remains in the built CSS, 59 frontend tests, clean typecheck,
  production build, and live checks of the gradient band, chips, CTA and footer.

---

## [2026-08-01 04:05:00 UTC]

CHG-0015 — Marketing home: hero, category tiles, CTA band

- **Hero** — type-led, without the reference's image collage or floating stat card. There is no
  photography, and a stat card would have to invent numbers; a confident text hero also keeps the LCP
  element text, which is the fastest thing a hero can be. Shown only on page 1 of the listing.
- **Category tiles**, which needed a new API capability: `CategoryDto.ArticleCount`, fed by a batched
  `CountPublishedArticlesAsync`. Deliberately a *different* count from the one guarding TX-2 deletion
  — that one must see drafts, a public tile must only ever count what a reader can open. One grouped
  query rather than one per tile.
- Tiles show **any category with its own published articles, at any depth**. Two alternatives were
  tried and rejected: top-level-only hid every tile once articles lived in child categories (the
  normal shape of a growing taxonomy), and rolling child counts up into the parent would have made a
  tile advertise 28 articles while the page it links to showed 0, because category pages filter
  strictly. **A count has to agree with the page it points at.** Rolling up would first require
  changing what a category page means.
- **CTA band** keeps the reference's rhythm and weight but carries a real link rather than its
  newsletter capture. No newsletter provider has been chosen (still open in STATUS.md), and a
  subscribe field that silently discards addresses costs more trust than it earns. Swap in the form
  once a provider exists.
- Home sections alternate `surface` / `surface-sunken` so the page separates into bands without
  rules, matching the reference's rhythm.
- The reference's logo/social-proof strip is omitted entirely rather than filled with fake logos;
  course grid, instructors and pricing wait for Phase 2 data.
- i18n extended for all of it in both locales — 55 keys, parity verified.
- Verified: 71 backend tests, 59 frontend tests, clean typecheck, production build, and live checks
  of both locales including that the tile count matches its category page exactly.

---

## [2026-08-01 03:55:00 UTC]

CHG-0014 — Article page: author card, meta row, related articles

- **Author bio flows through the cross-module contract.** `UserSummary` (ADR-0008) gains `Bio`, read
  from `ApplicationUser.Bio`, which existed and was unused since the Identity module landed.
- **Detail responses carry `author.bio`; list responses deliberately do not.** A page of twenty
  summaries has no use for twenty bios, and this is the cached read-heavy public path. Expressed as
  two distinct types — `AuthorDto` for bylines, `AuthorProfileDto` for the detail response — rather
  than one type populated inconsistently, which is the kind of subtlety that bites later. A test
  asserts both shapes.
- **Author card** below the article, horizontal rather than the reference's centred column: there
  are no social links to show, and horizontal costs less vertical space between the end of the
  article and the related links. Renders nothing when the author has no bio — a card with a name and
  empty space is worse than no card.
- **Related articles** replace the reference's sidebar, which was rejected because it competes with
  the ~68ch measure on the page where reading matters most. Same category, current article excluded,
  three across at `shell` width rather than the prose measure, because this is scanning not reading.
- **Meta row** rebuilt: category chip above the title, then author avatar + name, date and read time
  on a bounded row, echoing the card footer so the two surfaces read as one system. The premium badge
  moved into that row and the notice below it lost its duplicate label.
- `dev-seed-article.ps1` now sets an author bio directly in the Identity schema (dev convenience, as
  with the role grant) so the card has something to render.
- Verified: 70 backend tests, 59 renderer/primitive tests, clean typecheck, production build, and
  live checks confirming the bio appears on detail, is absent from summaries, and that an article
  whose author has no bio simply omits the card.

---

## [2026-08-01 03:45:00 UTC]

CHG-0013 — Site chrome and listings to the reference design

- **Header** split into a `SiteHeader` component with the reference's three-zone layout (brand,
  navigation, actions), a sticky translucent bar, and a placeholder `BrandMark` — a single-colour SVG
  that inherits `currentColor`, so the light header and dark footer need one asset, not two.
- **Footer** rebuilt dark, as in the reference, because it is what terminates the page. Four columns
  rather than five: DataBro has no apps, so the app-store column is dropped instead of filled with
  placeholders. **Topic links come from the API**, not a hardcoded list — the footer is a real crawl
  surface and a hardcoded list would rot silently. It degrades to an empty state rather than ever
  failing a page render.
- **`PageHeader` band** for index-style pages: flat `surface-sunken`, not the reference's
  pink→violet gradient. Slots for a breadcrumb above and a meta line below. Article pages
  deliberately have no band — it would push the body below the fold on the page where reading time
  matters most.
- **`ArticleCard`** rebuilt to the full reference anatomy: cover, category chip, title, excerpt, and
  a footer row with author avatar, name, date and read time. The cover is a **deterministic tinted
  panel keyed off the slug**, not a grey placeholder: Media does not exist yet, so the card has to
  look designed without an image, and the tint must not flicker between renders. Listings moved to a
  3-up grid on the wider `shell` container.
- **Pagination** restyled to the reference: numbered, active page a solid accent fill, still real
  crawlable anchors rather than buttons.
- **404** rebuilt to the reference composition — oversized ghosted status numeral with the heading
  overlaid. The reference keeps its newsletter band on error pages; DataBro drops it, since asking
  for an email on a broken page is the wrong moment.
- i18n extended for the new chrome in both locales, with a key-parity check confirming 39 identical
  keys on each side (CLAUDE.md rule 19).
- Verified: 59 tests, clean typecheck across five workspaces, production build prerendering, and live
  checks of chrome, cards, pagination, the 404 and the Indonesian locale in the containerised stack.

---

## [2026-07-31 16:05:00 UTC]

CHG-0012 — Design system documented, palette adopted, primitives built

- Studied the supplied 34-screenshot reference set and recorded it as two docs:
  [DESIGN_SYSTEM.md](docs/DESIGN_SYSTEM.md) (colour, typography, spacing, elevation, components) and
  [UI_PATTERNS.md](docs/UI_PATTERNS.md) (page-level composition, plus an explicit list of what is
  deliberately *not* adopted and why).
- **New palette: deep teal primary, violet secondary, amber reserved solely for premium.** The
  reference's bright blue is the default LMS choice and its pink→violet gradient reads
  consumer-lifestyle; DataBro teaches practitioners. Teal is uncommon in the category, carries
  data/terminal associations, and holds AA contrast on white. Teal `500` is marked decorative-only
  because it fails AA for text — `600` is the action/link step.
- Extended the token layer with secondary, functional (success/warning/danger/info) and premium
  roles, each with a `subtle` fill so a status can be tinted without inventing a colour. Dark-mode
  accent steps invert up the ramp rather than merely swapping the background.
- **Self-hosted fonts** — Plus Jakarta Sans (display), Inter (body), JetBrains Mono (code), as
  variable files via fontsource. Not Google's CDN: a third-party request on every page is both a
  privacy leak and a render-blocking dependency on the SEO-critical path.
- Added primitives to `@databro/ui` — `DbButton`, `DbCard`, `DbChip`, `DbInput`, `DbAccordion` —
  built before page work so the CMS editor inherits them rather than growing its own. They stay
  framework-agnostic: `DbButton` takes an `as` prop instead of importing NuxtLink.
- Accessibility contracts are enforced by tests rather than convention: `DbButton` uses
  `aria-disabled` when rendered as an anchor (where `disabled` is invalid), `DbInput` wires errors
  through `aria-invalid` + `aria-describedby` and suppresses the hint so only one message is
  announced, and `DbAccordion` uses real buttons with `aria-expanded`/`aria-controls`.
- Moved the reduced-motion guard into the token stylesheet so it applies globally instead of being
  re-declared per component.
- Verified: 59 renderer/primitive tests (up from 45), clean typecheck across five workspaces, live in
  the containerised stack, and a production build confirming the new token classes survive the
  Tailwind purge.

---

## [2026-07-31 16:20:00 UTC]

CHG-0011 — Design tokens and the article reading experience

- **Two-layer token system.** Raw values (brand ramp, typefaces, type scale) live in
  `tailwind-preset.ts`; semantic names (`surface`, `ink`, `line`, `accent`, `note-*`) resolve through
  CSS custom properties in `ui/src/styles/tokens.css`. Components now reference meaning
  (`text-ink-muted`) rather than a raw colour (`text-slate-500`), so light and dark themes come from
  one set of class names and a palette change touches no component. Channel values are
  space-separated RGB so Tailwind opacity modifiers still work through a variable.
- **Dark mode** responds to both `prefers-color-scheme` and an explicit `[data-theme]`, with the
  explicit choice winning in either direction.
- **Type scale** on a ~1.25 ratio with line heights tuned for reading rather than Tailwind's
  UI-oriented defaults, plus `max-w-prose` (~68ch) for article bodies and `max-w-shell` for listings
  and chrome, which are scanned rather than read.
- **Article rhythm lives on the `.databro-content` container**, not on individual blocks: eleven
  components no longer each assert their own margins, and a block renders correctly wherever it
  appears, including nested inside a list item.
- Restyled every block renderer against the tokens — code blocks with a filename header and visually
  separated output, callout variants coloured by semantic token (with role and data attribute still
  carrying the meaning, so it survives in monochrome), bordered tables, and inline marks. Links are
  underlined rather than colour-only, so they stay identifiable without colour perception.
- Migrated the site chrome, article page, listing cards and error page off hardcoded Tailwind palette
  classes onto the semantic tokens.
- Typed the preset as `Partial<Config>` at the source (adding `tailwindcss` as a dev dependency of
  `@databro/ui`) instead of casting at each point of use, so a malformed token fails in the package
  that owns it.
- **Scope note:** the palette *values* and the marketing/listing layout are deliberately not done.
  They need the supplied design reference, which is a client-rendered SPA that returns no markup to
  fetch, so it requires screenshots. What landed is the half driven by readability principle rather
  than by the reference, and is retunable from one file.
- Verified: 45 renderer tests, clean typecheck across five workspaces, live in the containerised
  stack, and a production build confirming the new token classes and CSS variables survive the
  Tailwind purge.

---

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
