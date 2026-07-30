# DataBro — SEO

SEO is a **cross-cutting concern, not a module.** For an articles-first platform, organic search is
the primary acquisition channel, so SEO is treated as load-bearing architecture, not decoration.

## 1. Ownership

* **Content module** owns per-unit SEO *metadata* (slug, meta, canonical, OG, robots, structured data
  inputs) and the `redirects` table.
* **Platform** owns site-wide artifacts (`sitemap.xml`, `robots.txt`, RSS) generated from content.
* **`site` frontend** owns *rendering* SEO into the page (head tags, JSON-LD) and honoring redirects.

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

* `sitemap.xml` (indexed, split by type/section when large) regenerated on publish via Hangfire.
* `robots.txt` allows content, disallows the `app`/authoring surfaces and API.
* RSS/Atom feed for articles (also aids discovery + newsletter).

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
