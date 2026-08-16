# DataBro — Business Rules

Business rules must not live only in source code. They are documented here and implemented in the
Domain layer. Rules are grouped by area and will grow per phase.

## Identity

* ID-1: An email is unique across all users (case-insensitive).
* ID-2: Privileged actions (authoring, publishing) require a verified email.
* ID-3: A user may link multiple external logins (Google/GitHub) to one account, matched by verified
  email.
* ID-4: Refresh tokens are single-use and rotated; detected reuse revokes the entire token chain.
* ID-5: A user account is soft-deleted, never physically removed; email may be released per privacy
  policy on deletion.

## Content — authoring & publishing

* CT-1: An article requires a non-empty `title` and at least one content block to be published.
* CT-2: A `slug` is unique across articles and is **immutable once the article has been published**.
* CT-3: Changing a published article's slug is only possible by creating a new slug **and** a 301
  redirect from the old path; the old URL must never 404.
* CT-4: Only `Content.Publish` holders (Editor/Admin) may publish, schedule, or unpublish. Authors may
  create and edit drafts only.
* CT-5: Publishing snapshots `draft_blocks` into `published_blocks`, writes an immutable version row,
  and increments `current_version` — atomically.
* CT-6: Public consumers only ever receive the published snapshot — `published_blocks`,
  `published_title` and `published_summary`; drafts are never publicly visible. This covers every
  public surface, not just the article page: listings, search (including the fuzzy fallback), the
  sitemap and RSS all read the published values.
* CT-7: A scheduled article publishes automatically at `scheduled_for`; if publish validation fails at
  that time, it remains scheduled and an alert is raised (it does not silently drop). A pending
  schedule can be cancelled, which returns the article to draft and leaves the draft untouched —
  cancelling is a decision about *when*, not about *what*.
* CT-8: `article_versions` is append-only; a published version is immutable. Restoring a version copies
  it into the draft; it never mutates history.
* CT-9: Deleting an article is a soft delete; it is removed from public listings/search but history is
  retained.
* CT-10: `visibility = premium` never hides an article from indexing — SEO metadata and a preview
  remain public; only the gated body is restricted (gating enforced from P3).
* CT-11: An article belongs to at most one category and any number of tags.
* CT-12: `reading_time_minutes` is derived from content on save, not user-entered.

## Content — localization

* CT-13: Locale variants of the same article share a `translation_group_id`; each variant has its own
  slug and is independently publishable.

## Media

* MD-1: Media assets are referenced by content and users by `mediaId`; the URL is owned by Media and
  never embedded/duplicated in content blocks.
* MD-2: Uploaded files are validated for type and size; disallowed types are rejected.
* MD-3: Images without `alt_text` produce an accessibility warning at publish (not a hard block in P1).

## Search

* SR-1: Only `published` + publicly visible (or premium-preview) content appears in public search
  results.
* SR-2: The search index is updated reactively from content events; it is eventually consistent and
  must be rebuildable from source content.

## Taxonomy

* TX-1: Category and tag slugs are unique within their type.
* TX-2: A category referenced by any article cannot be hard-deleted; deactivate/reassign instead.
* TX-3: Categories may nest (parent/child); cycles are disallowed.

## Cross-cutting

* XC-1: No business data is physically deleted — soft delete + history everywhere.
* XC-2: Every state-changing action is attributed to an actor and timestamped (audit).
* XC-3: Modules never read/write another module's tables; cross-module needs go through contracts/events.

---

## Future phases (placeholders)

* **Learning (P2):** lesson prerequisites gate progression; a course is "completed" only when all
  required lessons are complete; a certificate issues only on course completion (P3).
* **Billing (P3):** premium content access requires an active entitlement; entitlement checks are
  server-authoritative.
* **Enterprise (P4):** org seats are finite; a member consuming a seat cannot exceed the org's plan.

Rules are added here **before or alongside** the code that enforces them.
