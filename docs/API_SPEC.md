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

* Cursor-based for large/public listings: `?limit=20&cursor=…`. Offset (`?page=&pageSize=`) allowed for
  admin lists.
* Filtering via explicit query params (`?category=python&tag=async`).
* Sorting via `?sort=-published_at` (leading `-` = descending).
* List responses put paging info in `meta`: `{ "nextCursor": "…", "total": 123 }`.

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
GET    /api/v1/search?q=…                    keyword search over published content
GET    /api/v1/feed.rss                      RSS
GET    /sitemap.xml, /robots.txt             (platform, outside /api)
```

### Articles — authoring (app; Author/Editor/Admin)
```
POST   /api/v1/authoring/articles                       create draft
GET    /api/v1/authoring/articles                       list (all statuses, own/all per role)
GET    /api/v1/authoring/articles/{id}                  full article incl. draft_blocks + versions
PATCH  /api/v1/authoring/articles/{id}                  update draft (blocks, meta, seo, taxonomy)
POST   /api/v1/authoring/articles/{id}/publish          publish (Editor/Admin)
POST   /api/v1/authoring/articles/{id}/schedule         { scheduledFor }
POST   /api/v1/authoring/articles/{id}/unpublish
DELETE /api/v1/authoring/articles/{id}                  soft delete
GET    /api/v1/authoring/articles/{id}/versions         version history
POST   /api/v1/authoring/articles/{id}/restore/{version}
```

### Taxonomy (Editor/Admin)
```
POST/PATCH/DELETE /api/v1/authoring/categories
POST/PATCH/DELETE /api/v1/authoring/tags
```

### Media (Author/Editor/Admin)
```
POST   /api/v1/media                        upload (multipart) -> asset + variants
GET    /api/v1/media/{id}
DELETE /api/v1/media/{id}
```

## 6. Contracts & types

* Request/response DTOs are the source of truth for `packages/types` and `packages/api-client`.
* OpenAPI/Swagger is generated from the API and published for internal use.

## 7. Rate limiting & abuse

* Auth endpoints and search are rate-limited (Redis). See [SECURITY.md](SECURITY.md).

## 8. Errors

All error responses follow [ERROR_HANDLING.md](ERROR_HANDLING.md).
