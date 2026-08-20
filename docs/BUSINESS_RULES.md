# DataBro — Business Rules

Business rules must not live only in source code. They are documented here and implemented in the
Domain layer. Rules are grouped by area and will grow per phase.

## Identity

* ID-1: An email is unique across all users (case-insensitive).
* ID-2: **An unconfirmed address cannot sign in** (`Identity:Emails:RequireConfirmedEmail`, default
  true). Checked **after** the password, deliberately: before it, "confirm your email" would tell
  anyone which addresses have accounts; after it, the caller has already proved the account is theirs,
  so the message can be actionable instead of a dead end.
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

## Learning — curriculum

* LN-1: A course publishes independently of its lessons. A published course requires a title and at
  least one lesson, but **not** that every lesson body is published (ADR-0013).
* LN-2: A lesson whose body is unpublished is absent from the learner's view of a course, and an
  empty module is absent with it. The authoring view shows both, so an author sees the gap.
* LN-3: Order is a contiguous integer from zero within its parent, normalised by the aggregate after
  every structural change. Callers request a sequence; they never assign positions.
* LN-4: A lesson holds no blocks. Its body is a Content unit referenced by id and resolved through
  `ILessonContentReader` (ADR-0007, ADR-0012).
* LN-5: A course slug is immutable once published and a change creates a 301, exactly as CT-2/CT-3
  govern articles.

## Learning — enrollment & progress

* LN-6: **Course completion is a moment, not a computed state.** It is recorded when the learner has
  completed every lesson published *at that time*, and it is never revoked — not by the curriculum
  growing, and not by the learner un-ticking a lesson afterwards.

  Derived completion is retroactive: publishing one new lesson would silently un-finish everyone who
  had ever completed the course and invalidate their certificates, for a lesson that did not exist
  while they were studying. Courses grow after launch by design (LN-1), so this would be the ordinary
  consequence of authoring rather than an edge case.

  The visible consequence is intended: a learner can show as complete at 8 of 9 lessons. Both facts
  are true and the platform reports both.
* LN-7: Progress may only be recorded against a lesson the learner can actually reach — one in that
  course, with a published body. The recordable set and the readable set are the same set.
* LN-8: A learner's progress is addressed only as their own (`/me/...`), never by user id in a route
  or body. The identity comes from the token.
* LN-9: Enrolling is idempotent. A second enrolment returns the existing one rather than a conflict,
  including when it loses a race to a concurrent request.
* LN-10: Completing a lesson is idempotent and keeps the original timestamp; the resume point is the
  lesson last *opened*, which is not the lesson last completed.
* LN-11: Percent complete is derived at read time from published lessons, never stored, and capped at
  100 — a learner who completed a course before it grew is not shown above 100%.

## Learning — saved items

* LN-12: A bookmark stores a user, a kind and a target id, and **nothing copied from the target**.
  Title and path are resolved at read time, so a saved list can never disagree with the thing it
  points at.
* LN-13: A target that is no longer reachable **keeps its row**, with a null path. Dropping it would
  make a saved list shrink without explanation the moment an author unpublished something.
* LN-14: Saving is idempotent and removing succeeds even when nothing was saved — the same reasoning
  as LN-9 and logout: a control that can fail leaves the UI lying about its own state.

## Learning — streaks

* LN-15: **A streak counts local days, and the platform picks the timezone.** `Learning:Streaks:TimeZone`
  (default `Asia/Jakarta`) decides which day an instant belongs to.

  UTC days are the obvious choice and are wrong here: a WIB learner studying at 23:00 Monday and
  01:00 Tuesday has studied on two local days, but both instants land on the same UTC day — so the
  streak would count one. The error only ever undercounts, and it undercounts hardest for the
  learners furthest from UTC, who on this platform are most of them.

  A single configured zone is still a simplification, and a learner outside it sees their day roll
  over at an odd hour. It is a simplification that is right for the audience the product is built
  for, rather than one that is right for nobody. Per-learner timezones are the upgrade path; the
  domain already takes a day rather than an instant, so nothing below the service changes.
* LN-16: **A streak advances on completing a lesson, never on opening one.** A streak that rewards
  visiting rewards the wrong thing. A second lesson finished the same day is more work, not another
  day, and re-marking a lesson already complete is not work at all.
* LN-17: **A streak decays with the passage of time, not with writes.** The stored counter is only
  ever advanced; the *read* applies the days since. Someone who last studied three days ago has a
  stored count of 5 and a reported streak of 0, and nothing has written to their row — because they
  have not been back. Yesterday still counts: only a gap of two days or more breaks a run.
* LN-18: **The longest run is never reduced.** Breaking a streak is a fact about now; it is not a
  reason to erase what someone already did.
* LN-19: A completion dated before the last recorded one — a replay, a backfill, a clock that stepped
  backwards — is ignored rather than treated as a gap. Rewinding a streak would punish a learner for
  something the system did.

## Assessment

* AS-1: **A learner-facing response never carries the answer key.** Enforced by having separate DTO
  types rather than one with a nullable field — the learner shape has no correctness field to
  populate ([ADR-0018](adr/0018-assessment-scoring-and-the-answer-key.md)).
* AS-2: Correct choices and explanations are released only once an attempt is **submitted**. At that
  point the attempt cannot change, so the same data is feedback rather than the answers.
* AS-3: Scoring happens server-side from the stored key. A submission carries selections only; there
  is no score field on the request.
* AS-4: A question is scored **all or nothing** — the selection must be exactly the correct set.
  Partial credit rewards selecting broadly and has no defensible formula; a question that needs it
  should be split.
* AS-5: A quiz publishes only if every question has at least two choices and at least one correct
  answer. Stricter than a course's publish rule (LN-1): an unanswerable question is a trap, not an
  incomplete offering.
* AS-6: One quiz per lesson.
* AS-7: An attempt is submitted once and kept. A retake is a new attempt, so what someone actually
  answered survives. Starting a quiz with an open attempt **resumes** it — a reload is not a decision
  to discard answers.
* AS-8: `passed` is decided at submit time against the threshold in force *then*, and stored. Raising
  the passing score later must not retroactively fail anyone (the same reasoning as LN-6).
* AS-9: A lesson with a **published** quiz cannot be completed until the learner has a **passing**
  attempt (decided in D-1, 2026-08-19). A lesson with no quiz, or one whose quiz is still a draft,
  completes exactly as before. The check is a synchronous query Learning makes of Assessment at
  completion time (`IQuizGate`), never a subscription to `QuizAttemptSubmitted`: the answer has to be
  right the instant a learner clicks complete, and an eventually-consistent copy would refuse someone
  who had just passed. The gate stands only in front of a completion still to be made — a quiz added
  after a lesson was completed does not revoke that completion (the one-way stance of LN-6).

## Cross-cutting

* XC-1: No business data is physically deleted — soft delete + history everywhere.
* XC-2: Every state-changing action is attributed to an actor and timestamped (audit).
* XC-3: Modules never read/write another module's tables; cross-module needs go through contracts/events.

---

## Future phases (placeholders)

* **Learning (P2):** ~~a course is "completed" only when all required lessons are complete~~ — built,
  and **refined** into LN-6: completion is recorded once against the lessons published at that
  moment, not recomputed. The placeholder's wording would have made completion retroactive.
  Prerequisites are recorded but do not yet gate progression (ADR-0013); a certificate issues on
  course completion (P3) and will hang off `CourseCompletedDomainEvent`.
* **Billing (P3):** premium content access requires an active entitlement; entitlement checks are
  server-authoritative.
* **Enterprise (P4):** org seats are finite; a member consuming a seat cannot exceed the org's plan.

Rules are added here **before or alongside** the code that enforces them.
