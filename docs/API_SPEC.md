# DataBro — API Specification

REST API conventions and the Phase 1 endpoint surface. The API is consumed by both frontend apps and
(later) external clients.

## 1. Conventions

* Base path: `/api/v1`. Versioned in the path; breaking changes bump the version.
* JSON only. UTF-8. `application/json`.
* Resource names are plural, kebab-case (`/learning-paths`, `/content-blocks`).
* Use HTTP verbs correctly: GET (read, safe), POST (create/action), PUT/PATCH (update), DELETE (soft
  delete).
* Idempotency: publish/unpublish and other state actions are idempotent where possible.
* Auth: `Authorization: Bearer <jwt>`. Public read endpoints allow anonymous.

## 2. Response envelope

Success:

```json
{ "success": true, "data": { }, "meta": { } }
```

`meta` is optional (pagination, etc.). Failure — see [ERROR_HANDLING.md](ERROR_HANDLING.md):

```json
{ "success": false, "error": { "code": "validation_failed", "message": "…", "details": [] } }
```

## 3. Pagination, filtering, sorting

* **Offset (`?page=&pageSize=`) for indexable listings** — articles, categories, tags. These pages
  exist to be crawled, and a cursor has no stable URL a crawler can enumerate, so page numbers are
  required to satisfy the pagination requirement in [SEO.md](SEO.md). `pageSize` is clamped
  server-side (default 20, max 100) so it cannot be used to pull the whole table.
* **Cursor-based (`?limit=20&cursor=…`) for non-indexed feeds** — infinite-scroll surfaces in the
  authenticated app, activity streams, and anything else no crawler enumerates. *(Not yet implemented;
  no such endpoint exists.)*
* Filtering via explicit query params (`?category=python&tag=async`). A filter slug that matches
  nothing yields an **empty page**, never the unfiltered collection — silently dropping the filter
  would serve the whole catalogue on a page that should be empty.
* Sorting via `?sort=-published_at` (leading `-` = descending). *(Not yet implemented.)*
* List responses put paging info in `meta`:
  `{ "page": 1, "pageSize": 20, "total": 123, "totalPages": 7 }`.

## 4. Read vs. write separation

* **Public read endpoints** serve only published, public (or premium-preview) content and are
  cache-friendly (ETag/Cache-Control; backed by Redis/CDN).
* **Authoring endpoints** require Author/Editor/Admin and operate on drafts.

---

## 5. Phase 1 endpoints

### Auth (identity)
```
POST   /api/v1/auth/register                 { email, password, displayName }
POST   /api/v1/auth/verify-email             { token }
POST   /api/v1/auth/login                    { email, password } -> { accessToken, refreshToken }
POST   /api/v1/auth/refresh                  { refreshToken }
POST   /api/v1/auth/logout                   (revoke refresh)
POST   /api/v1/auth/forgot-password          { email }
POST   /api/v1/auth/reset-password           { token, password }
GET    /api/v1/auth/oauth/{provider}         start (google|github)
GET    /api/v1/auth/oauth/{provider}/callback
GET    /api/v1/me                            current user profile
PATCH  /api/v1/me                            update profile
```

### Articles — public read (site)
```
GET    /api/v1/articles                      published list (filter: category, tag, q; paginated)
GET    /api/v1/articles/{slug}               published article by slug (blocks + seo + author)
GET    /api/v1/categories                    category tree
GET    /api/v1/categories/{slug}/articles    articles in category
GET    /api/v1/tags                          tag list
GET    /api/v1/tags/{slug}/articles          articles by tag
GET    /api/v1/authors/{id}                  author profile + articles
GET    /api/v1/redirects?from={path}         resolve a moved path -> { toPath, statusCode } or 404
GET    /api/v1/search?q=…&locale=…           full-text search over published content (paginated)
```

#### `GET /api/v1/search`

Served by **Content**, not the Search module ([ADR-0010](adr/0010-fts-lives-in-content.md)). This
route is the seam that survives the eventual move to OpenSearch.

| Parameter | Default | Notes |
|---|---|---|
| `q` | — | Fewer than 2 characters returns an empty page rather than everything. |
| `locale` | `en` | Search is locale-scoped because the index stems per locale. An unrecognised value falls back to the default; it is not a 400. |
| `page`, `pageSize` | 1, 20 | Same offset paging as every other listing. |

`data` is an array of article summaries, identical in shape to `/api/v1/articles`, so clients reuse
one parser. `meta` carries the usual paging plus:

* **`matchMode`** — `"exact"` when full-text matched the query as typed, `"fuzzy"` when full-text
  found nothing and the API fell back to trigram similarity over titles. A client showing fuzzy
  results **must say so**; presenting approximations as exact is how a search box loses trust.
  `"exact"` is also reported when the fallback found nothing either — there is no approximation to
  apologise for, just no results.

The query goes through `websearch_to_tsquery`, so unbalanced quotes and stray boolean operators are
tolerated rather than raising a syntax error. A public search box must never 500 on typed input.

**`sitemap.xml`, `robots.txt` and `feed.xml` are not API endpoints.** An earlier revision of this
document listed them here (and an RSS endpoint at `/api/v1/feed.rss`). They are served by the `site`
app as Nitro routes at `{siteUrl}/sitemap.xml`, `/robots.txt` and `/feed.xml`, because a crawler
requests them from the site's origin, not the API's. The API's contribution is the data they are
built from — the public article, category and tag listings above. See [SEO.md](SEO.md) §6.

The `site` app calls `/api/v1/redirects` on a 404 to honor a moved slug with a 301 rather than
serving a dead page (docs/SEO.md §4). `from` is the normalized path (leading slash, lowercased, no
trailing slash); a 404 is the normal "no redirect" answer.

### Articles — authoring (app; Author/Editor/Admin)
```
POST   /api/v1/authoring/articles                       create draft
GET    /api/v1/authoring/articles                       list (all statuses, own/all per role)
GET    /api/v1/authoring/articles/{id}                  full article incl. draft_blocks + versions
PATCH  /api/v1/authoring/articles/{id}                  update draft (blocks, meta, seo, taxonomy)
POST   /api/v1/authoring/articles/{id}/publish          publish (Editor/Admin)
POST   /api/v1/authoring/articles/{id}/schedule         { scheduledFor }
POST   /api/v1/authoring/articles/{id}/unpublish
PUT    /api/v1/authoring/articles/{id}/slug             change slug { slug }; 301 if published (CT-3)
DELETE /api/v1/authoring/articles/{id}                  soft delete
GET    /api/v1/authoring/articles/{id}/versions         version history
POST   /api/v1/authoring/articles/{id}/restore/{version}
```

### Taxonomy (Editor/Admin)
```
POST/PATCH/DELETE /api/v1/authoring/categories
POST/PATCH/DELETE /api/v1/authoring/tags
PUT /api/v1/authoring/categories/{id}/slug   change slug { slug }; always records a 301 (CT-3)
PUT /api/v1/authoring/tags/{id}/slug         change slug { slug }; always records a 301 (CT-3)
```

A term slug is always a live public URL, so a taxonomy slug change is unconditionally paired with a
301. An article's is paired with one only once it has been published — a never-published draft has no
indexed URL to protect. Redirect chains are collapsed on write, so a stored redirect always points at
a live page (one hop).

### Media (Author/Editor/Admin)
```
POST   /api/v1/media                        upload (multipart, field: file; optional altText)
GET    /api/v1/media                        library listing, newest first (paginated)
GET    /api/v1/media/{id}
PATCH  /api/v1/media/{id}                   { altText }
DELETE /api/v1/media/{id}                   soft delete (requires Content.Delete)
```

All of these require **`Media.Upload`** except `DELETE`, which requires `Content.Delete`. The listing
is protected too: the stored *files* are public, but the index of them is not a public gallery.

Upload accepts **JPEG, PNG, WebP and GIF**, identified by magic bytes — the declared `Content-Type`
and the filename extension are ignored (ADR-0011). SVG is refused and must stay refused: it is XML,
it can execute script, and unlike a raster format it cannot be neutralised by re-encoding. Limits:
10 MB, 12,000px per side, 50 megapixels.

The response is the asset, with `processingStatus` `pending` until the variant job finishes:

```json
{ "success": true, "data": {
  "id": "…", "url": "https://…/original.jpg", "fileName": "chart.jpg",
  "mimeType": "image/jpeg", "byteSize": 32657, "width": 1600, "height": 900,
  "altText": "…", "processingStatus": "pending", "processingError": null,
  "createdAt": "…", "variants": [] } }
```

A client needing a `srcset` re-reads the asset once processing completes; `variants` is empty until
then, and consumers render the original at full size rather than a half-built `srcset`.

**Article responses carry a resolved `media` map** keyed by media id, covering every image block plus
`og:image`. That is what lets the renderer resolve an image with a lookup instead of a request per
figure on the cached read path.

## 6. Contracts & types

* Request/response DTOs are the source of truth for `packages/types` and `packages/api-client`.
* OpenAPI/Swagger is generated from the API and published for internal use.

### Wire conventions for the Article contract

These are contract-level guarantees, not incidental serializer behaviour:

* **Enums cross the wire lowercase.** `status` is `draft|scheduled|published|unpublished|archived`
  and `visibility` is `public|premium`, matching the TypeScript unions in `@databro/types`. Parsing
  inbound values stays case-insensitive.
* **`author` is a resolved object, not an id.** `{ id, displayName, avatarUrl }`. Content stores only
  an author id and resolves the name through the shared `IUserDirectory` contract (ADR-0008).
* **Detail responses carry `author.bio`; list responses do not.** A page of twenty summaries has no
  use for twenty bios, and this is the cached, read-heavy public path. The two shapes are distinct
  types (`AuthorDto` vs `AuthorProfileDto`) rather than one type populated inconsistently.
* **`author` is nullable.** It is null when the author can no longer be resolved (e.g. a deleted
  account). Clients render their own localized fallback — the API does not emit user-facing English.
  A missing author must never break an article page.
* `avatarUrl` is always null until the Media module lands; resolving a `MediaId` to a URL is Media's
  responsibility.

`packages/api-client` deliberately exposes **only endpoints that exist**. Category/tag filtering and
`search` are listed above as the Phase 1 target surface but are not implemented yet, so they are
absent from the client rather than shipped as methods that 404 at runtime.

## 7. Rate limiting & abuse

* Auth endpoints and search are rate-limited (Redis). See [SECURITY.md](SECURITY.md).

## 8. Errors

All error responses follow [ERROR_HANDLING.md](ERROR_HANDLING.md).
