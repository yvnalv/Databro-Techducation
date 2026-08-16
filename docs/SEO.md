# DataBro — SEO

SEO is a **cross-cutting concern, not a module.** For an articles-first platform, organic search is
the primary acquisition channel, so SEO is treated as load-bearing architecture, not decoration.

## 1. Ownership

* **Content module** owns per-unit SEO *metadata* (slug, meta, canonical, OG, robots, structured data
  inputs) and the `redirects` table.
* **`site` frontend** owns *rendering* SEO into the page (head tags, JSON-LD), honoring redirects,
  and serving the site-wide artifacts (`sitemap.xml`, `robots.txt`, `feed.xml`).

> **Correction (2026-08-16).** This document previously assigned the site-wide artifacts to
> **Platform**. That is wrong in a two-origin deployment: a crawler fetches
> `https://databro.id/robots.txt`, so the file must come from whichever host answers for that origin
> — the `site` app, not the API. The API still owns the *data*; the site app reads it through the
> public endpoints and renders the XML. See §6.

## 2. Per-article SEO metadata

Stored in the article's `seo` JSONB plus first-class columns:

* `slug` — unique, lowercase, hyphenated, ASCII-folded. **Immutable once published.**
* `meta_title` — defaults to `title`; overridable; length-guided.
* `meta_description` — defaults to `summary`; overridable.
* `canonical_url` — self-canonical by default; overridable (e.g. syndicated content).
* `robots` — `index,follow` by default; `noindex` for drafts/unpublished/premium-preview as configured.
* `og_image` — references a Media asset; falls back to cover image, then a site default.

## 3. Structured data (JSON-LD)

* Articles emit `Article` / `TechArticle` schema (headline, author, datePublished, dateModified,
  image, publisher). Premium articles add `isAccessibleForFree: false` + `hasPart.cssSelector`
  naming the gated region, which is how a paywall stays indexable instead of reading as cloaking.
* **Category pages emit `BreadcrumbList`**, mirroring the visible breadcrumb. This is what makes a
  category tree legible as a topic cluster rather than a pile of unrelated pages.
* **Tag pages emit no breadcrumb.** Tags are flat, so claiming a hierarchy would be structured data
  that misrepresents the site.
* Author pages emit `Person`; the site emits `Organization` + `WebSite` (with SearchAction).
  *(Not yet implemented.)*
* Phase 2 adds `Course` structured data for courses/lessons.

## 4. URLs & redirects

* Clean, stable, human-readable paths (e.g. `/python/virtual-environments`).
* **Slugs are immutable after publish.** If a slug must change, the old path is written to `redirects`
  as a **301** and the `site` app serves the redirect. Never break an indexed URL silently.
* **Paginated listings** (homepage, category, tag) use offset page numbers — `?page=2` — not cursors,
  because a crawler must be able to enumerate the set. Each page is **self-canonical**: page 2
  canonicalises to page 2, never to page 1. Canonicalising every page to the first tells a crawler
  the deeper pages are duplicates, and the articles only listed there lose their discovery path.
* Pagination links must be real `<a href>` elements. `rel=prev/next` is still emitted, but it is a
  courtesy for other engines only — Google confirmed in 2019 that it had not used those hints for
  indexing for years, so the crawlable anchors are the load-bearing part.
* A `?page=` beyond the last page returns **404**, not an empty 200. Otherwise a crawler can
  enumerate an unbounded supply of thin, near-duplicate indexable pages.
* Page 2+ titles are disambiguated (`Category — 2`) to avoid duplicate-title reports.
* Trailing-slash and case normalization handled at the edge (Nginx/CDN) consistently.

## 5. Rendering & performance

* Public pages render via **SSG/ISR** so crawlers get fully-formed HTML fast.
* Core Web Vitals are a target: lazy-load below-the-fold media, responsive image variants (Media
  module), preconnect to the CDN, minimal blocking JS on `site`.
* Target: Lighthouse SEO ≥ 95 and Performance ≥ 90 on content pages.

## 6. Sitemaps, robots, feeds

All three are **Nitro server routes in the `site` app** (`frontend/apps/site/server/routes/`), built
on the public API through `server/utils/catalogue.ts`. They must live on the site's own origin — see
the correction in §1.

### `robots.txt`

* Allows everything by default: this is a content site whose entire strategy is being indexed.
* Disallows `/*?page=` — paginated listings past page 1 are thin and near-duplicate, and every
  article they contain is reachable from the sitemap anyway.
* Points at `Sitemap: {siteUrl}/sitemap.xml`.
* The authoring app is a **separate origin** and carries its own `X-Robots-Tag: noindex`, so it needs
  no entry here.

### `sitemap.xml`

* Home, every published article (`lastmod` = `publishedAt`), every **populated** category, and every
  tag. Empty categories are omitted for the same reason the home tiles omit them: a sitemap entry
  for an empty listing is an invitation to a dead end.
* Each URL is emitted **once per locale**, and every entry carries `xhtml:link` alternates for the
  full locale set plus `x-default`. Listing only `en` would leave `/id/*` undiscovered; listing both
  without alternates would read as duplicate content rather than translations.
* A failing section is caught and skipped rather than 500ing the document — a partial sitemap still
  gets most of the catalogue indexed; a 500 indexes nothing.

### `feed.xml` (RSS 2.0)

* Latest 25 published English articles. **English only, deliberately**: an RSS channel declares one
  `language`, so mixing locales gives every subscriber half their items in a language they did not
  ask for. `/id` gets its own feed when it has the content to justify one.
* **Summaries only, never rendered bodies.** Bodies are typed blocks; rendering them to feed HTML
  would mean a second renderer to keep in step with the real one.
* `guid` is the permalink, which is safe precisely because slugs are immutable once published (§4) —
  a reader will never be shown an old item as new.
* Discoverable via `<link rel="alternate" type="application/rss+xml">` in the site head plus a footer
  link.

**Scale note.** `allPublishedArticles` pages the public listing 100 at a time (hard cap 50 pages).
That is fine at the current catalogue size and wrong at ten thousand articles — the upgrade path is
a bulk/`lastmod`-only endpoint plus a sitemap index, tracked in [STATUS.md](STATUS.md).

## 7. Premium content & SEO

* Premium articles are **indexable**: they expose full metadata + a substantive preview so they rank
  and convert, with the gated body behind auth. This is why `site` is not logged-out-only.
* Use appropriate structured data (`isAccessibleForFree: false` + `hasPart` gated section) so search
  engines understand the paywall and don't treat it as cloaking.

## 8. Internationalization & SEO

* Locale variants (linked by `translation_group_id`) emit `hreflang` alternates pointing at each other
  and a canonical per locale.

## 9. Guardrails

* No indexable duplicate content: canonical everything; drafts and previews are `noindex`.
* No cloaking: crawlers and users receive the same content (paywall handled via standard structured
  data, not user-agent sniffing).
* Every new public page type must define its canonical, meta, and structured-data story before ship.
