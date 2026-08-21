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
POST   /api/v1/auth/logout                   { refreshToken } - revokes it
POST   /api/v1/auth/forgot-password          { email }
POST   /api/v1/auth/reset-password           { userId, token, password }
POST   /api/v1/auth/resend-confirmation      { email }
POST   /api/v1/auth/confirm-email            { userId, token }
GET    /api/v1/me                            current user profile
```

#### Social login (OAuth) — Google and GitHub (ADR-0019)

```
GET    /api/v1/auth/oauth/{provider}         start: 302 to the provider (google|github)
       ?returnTo=<path|site-url>             optional, validated post-login destination
GET    /api/v1/auth/oauth/{provider}/callback  provider returns here; 302 to the app
POST   /api/v1/auth/oauth/exchange           { code } -> { accessToken, refreshToken, expiresInSeconds }
```

The first two are **browser navigations, not API calls**: they answer with a `302`, never the
envelope. `GET /oauth/{provider}` redirects to the provider carrying a signed `state`. The callback
verifies state, links or creates the account by **verified email** (ID-3), and redirects the app to
`/auth/callback` with a **single-use handoff code** — never tokens in the URL. Only
`POST /oauth/exchange` speaks the envelope: the app trades the code for the token pair, exactly as
`login` returns them. A callback that cannot be completed (denied consent, bad state, unverified
GitHub email) redirects to the app's sign-in page with `?error=oauth`.

**Not built:**

```
PATCH  /api/v1/me                            profile editing - unbuilt
```

#### Sign-in and confirmation

`login` returns **403 `email_not_confirmed`** when the password is correct but the address has never
been confirmed. It returns the generic **401** for a wrong password or an unknown address, and those
two are byte-identical.

The ordering is the point: the confirmation check runs *after* the password check, so the specific
message only ever reaches someone who has already proved the account is theirs. Running it first
would make the endpoint an enumeration oracle.

`Identity:Emails:RequireConfirmedEmail` controls it and defaults to **true**, including in
development — a rule enforced only in production is a rule nobody has tested.

> **`options.SignIn.RequireConfirmedEmail` is not the control**, despite reading like it. Sign-in goes
> through `UserManager.CheckPasswordAsync` rather than `SignInManager`, so that setting is never
> consulted; it sat at `false` for months while appearing to be the switch. The real check is
> explicit in `AuthService.LoginAsync`.

#### Account recovery

`forgot-password` and `resend-confirmation` **always return 200**, whether or not the address belongs
to an account. An endpoint that answered differently would be a membership oracle: an address list
could be tested against it to learn who has an account here. The cost is that a typo produces
silence, which is why the UI says *"if that address has an account"* and never *"sent"*.

`reset-password` distinguishes exactly one failure — a password that breaks the policy, which is
actionable and tells an attacker nothing. Expired, already used, wrong user and tampered-with all
return one message, because telling someone holding a stolen link which kind they have helps only
them.

**A successful reset revokes every refresh token the account holds.** Resetting is what someone does
when they believe the account is compromised, and leaving an attacker's session alive through it
would make the reset theatre.

`logout` is unauthenticated on purpose: the refresh token in the body is the thing being revoked, and
requiring a valid *access* token would make signing out impossible once one had expired — exactly
when someone most wants to. It succeeds for an unknown or already-revoked token, because signing out
must never fail.

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
POST   /api/v1/authoring/articles/{id}/publish          publish (Content.Publish)
POST   /api/v1/authoring/articles/{id}/schedule         { scheduledFor } (Content.Publish)
POST   /api/v1/authoring/articles/{id}/unschedule       cancel a pending schedule (Content.Publish)
POST   /api/v1/authoring/articles/{id}/unpublish        (Content.Publish)
PUT    /api/v1/authoring/articles/{id}/slug             change slug { slug }; 301 if published (CT-3)
DELETE /api/v1/authoring/articles/{id}                  soft delete
GET    /api/v1/authoring/articles/{id}/versions         history, newest first (Content.Edit)
GET    /api/v1/authoring/articles/{id}/versions/{n}     one snapshot, with its content (Content.Edit)
POST   /api/v1/authoring/articles/{id}/versions/{n}/restore   copy into the draft (Content.Edit)
```

**Version history and restore sit behind `Content.Edit`, not `Content.Publish`** — restoring copies a
snapshot into the *draft* and changes nothing a reader sees until someone publishes afterwards, which
writes a new version of its own (CT-8). An Author undoing their own work in progress does not need
the publishing permission. Scheduling and *un*scheduling are the reverse: both are publishing acts
(CT-4).

`/unschedule` exists because scheduling was otherwise a one-way door: `/unpublish` only accepts a
published article, so an editor who scheduled something and changed their mind had no way back to
draft.

The version list carries metadata only (`version`, `title`, `summary`, `readingTimeMinutes`,
`createdAt`, `isCurrent`). Fetch a single version for its `content` — shipping every snapshot's body
in the list would make it the heaviest response in the CMS.

**Draft and published title/summary are separate** (CT-6). Public responses — the article detail,
listings, search and RSS — carry the *published* values; authoring responses carry the draft. Before
this separation existed, typing a new headline changed the live page, the sitemap and the search
index the moment it was saved.

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

### Courses — public read (site)
```
GET /api/v1/courses                          published courses, newest first (paginated)
GET /api/v1/courses/{slug}                   one course with its full curriculum
GET /api/v1/courses/{slug}/lessons/{lessonSlug}   one lesson, with its neighbours
```

The course page returns modules, each with its lessons, each lesson carrying its **published** body
resolved from Content. A lesson whose body is unpublished is omitted entirely, and a module left with
nothing in it is omitted with it (LN-2). The join is one batch call however many lessons there are —
a per-lesson lookup would be an N+1 on a learner's hottest page.

**The single-lesson response carries a resolved `media` map** keyed by media id, exactly like an
article — covering the image blocks in that one lesson's body, so the renderer resolves an image with
a lookup rather than a request per figure. Only the lesson on the page is resolved, not its prev/next
neighbours: those are links, and a link renders no images.

The lesson read is **nested under its course** because the course is what gives a lesson prev/next,
a breadcrumb and a progress context: the same body reached through two courses is two positions in
two sequences, and the URL should say which. It is a separate read rather than a client-side pick out
of the course response, which carries every body it has — right for rendering a whole curriculum,
wrong as the cost of reading lesson three of fifty.

Neighbours cross module boundaries: the last lesson of a module links to the first of the next. A
learner moves through one sequence, and stopping them at a section break would be the data model
showing through the page. Both reads compose from the same published-only view, so they cannot
disagree about what a learner may see.

### Courses — authoring (app; Author/Editor/Admin)
```
POST   /api/v1/authoring/courses                                       create draft
GET    /api/v1/authoring/courses                                       all courses, drafts included
GET    /api/v1/authoring/courses/{id}                                  full curriculum, gaps visible
PATCH  /api/v1/authoring/courses/{id}                                  { title, summary, difficulty }
POST   /api/v1/authoring/courses/{id}/modules                          { title }
PATCH  /api/v1/authoring/courses/{id}/modules/{moduleId}
DELETE /api/v1/authoring/courses/{id}/modules/{moduleId}
PUT    /api/v1/authoring/courses/{id}/modules/order                    { orderedIds }
POST   /api/v1/authoring/courses/{id}/modules/{moduleId}/lessons       { contentUnitId }
PATCH  /api/v1/authoring/courses/{id}/modules/{moduleId}/lessons/{lessonId}
DELETE /api/v1/authoring/courses/{id}/modules/{moduleId}/lessons/{lessonId}
PUT    /api/v1/authoring/courses/{id}/modules/{moduleId}/lessons/order { orderedIds }
POST   /api/v1/authoring/courses/{id}/publish
POST   /api/v1/authoring/courses/{id}/unpublish
```

Structure requires **`Content.Edit`** and publishing requires **`Content.Publish`** — the same split
articles use (CT-4), reusing Content's permissions rather than minting Learning ones so there is one
editorial role rather than two overlapping sets.

Reordering is **one call for a whole rearrangement**, not a move per row: it is one transaction
against one aggregate, and a per-row API would let a drag half-apply. Every authoring call returns
the whole course, so a builder UI never reconciles a patch against local state.

### Learning paths — public read (site)
```
GET /api/v1/learning-paths                   published paths, each with resolved course cards
GET /api/v1/learning-paths/{slug}
```

A path holds an **ordered list of course ids** and the read resolves them into cards, in the curated
order — the sequence is the point of a path, and a database's natural ordering is not it. Courses
that are not published are **dropped** from the public read, the same rule a course applies to an
unpublished lesson (LN-1/LN-2): a path may be curated ahead of the courses in it. The authoring view
keeps them so a curator sees the gap.

### Learning paths — authoring (app; Author/Editor/Admin)
```
POST   /api/v1/authoring/learning-paths                            create draft
GET    /api/v1/authoring/learning-paths                            all paths, drafts included
GET    /api/v1/authoring/learning-paths/{id}                       full path, gaps visible
PATCH  /api/v1/authoring/learning-paths/{id}                       { title, summary, difficulty }
POST   /api/v1/authoring/learning-paths/{id}/courses/{courseId}    append (idempotent)
DELETE /api/v1/authoring/learning-paths/{id}/courses/{courseId}
PUT    /api/v1/authoring/learning-paths/{id}/courses/order         { orderedIds }
POST   /api/v1/authoring/learning-paths/{id}/publish
POST   /api/v1/authoring/learning-paths/{id}/unpublish
```

Same permission split as courses: curating is `Content.Edit`, publishing is `Content.Publish`.
Appending a course already in the path is a **no-op, not an error** — a builder UI dropping the same
card twice is a slip, not a decision worth refusing. Publishing requires at least one course but not
that those courses are published.

**Every mutation returns the whole path**, including `PATCH`. The builder replaces its state from the
response rather than patching locally, so a response carrying only the changed fields would blank the
sequence on screen. Reordering is one call for the whole order, never a move per row: it is one
transaction against one aggregate, and a per-row API would let a drag half-apply.

### Quizzes (Assessment)

```
GET    /api/v1/lessons/{lessonId}/quiz                   the quiz, without any answer key
POST   /api/v1/lessons/{lessonId}/quiz/attempts          start, or resume an open attempt
GET    /api/v1/lessons/{lessonId}/quiz/attempts          this learner's history for it
GET    /api/v1/me/attempts/{attemptId}
POST   /api/v1/me/attempts/{attemptId}/submit            { answers: { questionId: [choiceId] } }
```

Authenticated, no permission required — taking a quiz is a learner acting on their own data. The
quiz read is authenticated rather than public: a question bank is worth something and there is no SEO
case for exposing one.

**No response on this path carries the answer key** (AS-1). It is enforced by type: the learner DTO
has no correctness field to populate, rather than one that is conditionally omitted. Correct choices
and explanations appear only on an attempt that has been **submitted** (AS-2) — at which point it can
no longer change, so the same data is feedback.

The submission carries **selections only**. Scoring happens in the domain from the stored key (AS-3),
so there is no score field to fabricate. Questions are scored all-or-nothing (AS-4).

```
POST   /api/v1/authoring/quizzes                                              create draft
GET    /api/v1/authoring/quizzes                                              all quizzes
GET    /api/v1/authoring/quizzes/{id}                                         includes the answer key
GET    /api/v1/authoring/quizzes/by-lesson/{lessonId}                         "does this lesson have one?"
GET    /api/v1/authoring/quizzes/{id}/attempts                                submitted attempts, no answer key
PATCH  /api/v1/authoring/quizzes/{id}                                         { title, passingScore }
POST   /api/v1/authoring/quizzes/{id}/questions                               { prompt, type, points }
PATCH  /api/v1/authoring/quizzes/{id}/questions/{questionId}
DELETE /api/v1/authoring/quizzes/{id}/questions/{questionId}
PUT    /api/v1/authoring/quizzes/{id}/questions/order                         { orderedIds }
POST   /api/v1/authoring/quizzes/{id}/questions/{questionId}/choices          { text }
DELETE /api/v1/authoring/quizzes/{id}/questions/{questionId}/choices/{choiceId}
PUT    /api/v1/authoring/quizzes/{id}/questions/{questionId}/answer           { correctChoiceIds }
POST   /api/v1/authoring/quizzes/{id}/publish
POST   /api/v1/authoring/quizzes/{id}/unpublish
```

Structure requires `Content.Edit`, publishing `Content.Publish` — the same split articles and courses
use. The answer key is set **as a whole** rather than toggled per choice, so "two correct answers on a
single-choice question" is not a state an author can pass through. Publishing requires every question
to have at least two choices and one correct answer (AS-5): an unanswerable question is a trap, not an
incomplete offering.

The attempts read (`Content.Edit`) is a roll-up for the author's review screen: one row per submitted
attempt — learner (resolved through `IUserDirectory`), score and pass/fail — and deliberately **no
per-question selections and no answer key**. In-progress attempts are omitted; they have no score to
review. It is the authoring counterpart to the learner's `/api/v1/lessons/{lessonId}/quiz/attempts`,
which only ever returns the caller's own attempts.

### Progress (learner; any authenticated user)
```
GET    /api/v1/me/enrollments                                          dashboard (paginated)
GET    /api/v1/me/enrollments/{courseSlug}                             progress in one course
POST   /api/v1/me/enrollments/{courseSlug}                             enrol
POST   /api/v1/me/enrollments/{courseSlug}/lessons/{lessonId}/visit    move the resume point
POST   /api/v1/me/enrollments/{courseSlug}/lessons/{lessonId}/complete
DELETE /api/v1/me/enrollments/{courseSlug}/lessons/{lessonId}/complete un-mark
```

Authenticated but with **no permission requirement**, deliberately. Every other write on the platform
is an editorial act gated by RBAC; this is a learner acting on their own data, and a `Learning.Enrol`
permission would mean every new signup needs a grant before the platform does the thing it exists to
do. Being signed in is the entitlement.

The authorization that matters here is the id, not the role: the learner comes from the token and
never from the route or body, so no request shape reads or writes someone else's progress. That is
why these live under `/me` rather than `/users/{id}` (LN-8).

`visit` and `complete` are separate because opening a lesson and finishing it are different claims;
one endpoint for both would complete a course for someone who merely scrolled through it. Both are
idempotent (LN-9, LN-10). A lesson that is not in the course, or whose body is unpublished, is a
**404** — the recordable set is the readable set (LN-7).

```json
{ "success": true, "data": {
  "id": "…", "courseId": "…", "courseSlug": "rag-course",
  "courseTitle": "Retrieval-Augmented Generation",
  "enrolledAt": "…", "completedAt": null,
  "lastLessonId": "…", "lastLessonSlug": "chunking-strategies", "lastAccessedAt": "…",
  "totalLessons": 9, "completedLessons": 4, "percentComplete": 44,
  "completedLessonIds": ["…"] } }
```

`completedAt` is set once and never cleared, so `completedLessons` can legitimately sit below
`totalLessons` on a completed course — the course grew after the learner finished it (LN-6).
`percentComplete` is derived per request and capped at 100. `lastLessonSlug` is the resume point as
a URL segment, and is **null** when that lesson has since been unpublished or dropped from the
curriculum — a client should fall back to the course page rather than link somewhere that 404s.

### Streak (learner; any authenticated user)
```
GET    /api/v1/me/streak                                               study streak
```

```json
{ "success": true, "data": {
  "current": 4, "longest": 11, "lastActiveOn": "2026-08-20", "activeToday": true } }
```

**Read-only, and deliberately so.** A streak is earned by completing lessons — it advances as a
consequence of `POST .../complete` and there is no endpoint that lets a client assert one (LN-16).

`current` is the streak **as of today**, which is not the number stored: a streak decays with the
passage of time rather than with writes, so a learner who last studied three days ago reads 0 here
while nothing has changed on the server since (LN-17). Clients must render `current` as given.
Recomputing it from `lastActiveOn` in a browser would use the *visitor's* timezone rather than the
platform's and would disagree with the server for anyone travelling.

`lastActiveOn` is a **local date** (`YYYY-MM-DD`) in the platform timezone, not a timestamp — which
day an instant belongs to is settled server-side (LN-15). `activeToday` says whether today already
counts, so a dashboard can acknowledge someone who has already put the work in instead of nagging
them.

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
