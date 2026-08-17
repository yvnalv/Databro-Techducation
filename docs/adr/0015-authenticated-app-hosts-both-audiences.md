# ADR-0015 — The authenticated app hosts both audiences; the boundary is indexability

Status: Accepted
Date: 2026-08-17
Deciders: DataBro core

## Context

[ADR-0005](0005-two-app-frontend-monorepo.md) split the frontend into two apps and drew the line by
**rendering need, not by feature**: anything that must be indexed and cached lives in `site`.

It then labelled the other app "the authenticated **learner** app — dashboard, progress, playground",
and [FRONTEND_ARCHITECTURE.md](../FRONTEND_ARCHITECTURE.md) repeated that label in its heading and
layout diagram.

The docs are not wrong so much as **internally inconsistent**: the same file's body already lists "the
CMS authoring UI (internal roles)" among `app`'s purposes, and adds that CMS authoring lives there
behind Author/Editor/Admin. So the intent to host both was recorded; only the name never caught up,
and the name is what everyone reads.

What was actually built in `apps/app` is the CMS alone: article and lesson editors, the course
builder, taxonomy. No learner surface exists anywhere. This ADR settles the label in the direction
the body already pointed, and states the criterion behind it so the label cannot drift again.

The drift was invisible while everything a learner could do was read-only — browsing articles and
courses is `site`'s job and `site` does it. Enrollment and progress (CHG-0040) is where it bites: a
complete progress API now exists with nowhere to render it, and lesson pages cannot be built until
it is settled, because a lesson page is the first thing that has to *live* somewhere.

The forces:

* A learner dashboard and the CMS are both authenticated, dynamic, and must never be indexed.
* A lesson body is content. Rule 9 requires premium content to expose SEO metadata and a preview
  publicly, which means lesson pages have to be crawlable at the metadata level even when the body
  is gated.
* A third app is a third Dockerfile, CI job, deploy target and session surface.
* Learners will outnumber editors by orders of magnitude, so whoever gets the root path should be
  the common case, not the incumbent.

## Decision

**Keep two apps. `apps/app` is the *authenticated* app, not the *learner* app: it hosts both the
learner dashboard and the CMS, separated by route and role.**

The boundary between the two apps is restated as the criterion ADR-0005 actually derived it from:

| Surface | Indexable | App |
|---|---|---|
| Articles, course catalogue, course pages, **lesson pages** | yes | `site` |
| Learner dashboard, progress, playground | no | `app` |
| CMS: article/lesson editors, course builder, taxonomy | no | `app` |

The CMS and the learner dashboard have **identical rendering needs** — authenticated, dynamic,
`noindex`, client-heavy. Under the rule ADR-0005 set, that puts them in the same app. The two-app
split was right; only the label on the second app was wrong.

Concretely:

* The CMS moves to the **`/studio`** namespace, under its own layout.
* Learner surfaces take the **root**, under the default layout.
* Landing is role-aware: a user who can author goes to `/studio`, everyone else to their dashboard.
* **Lesson reading lives on `site`**, not here. A lesson is a renderable content unit with SEO value
  (ADR-0007), and `site` is already auth-aware by design — a premium article renders its preview
  publicly and gates the body. A lesson is the same shape, and progress controls hydrate into the
  page for a signed-in learner.

## Alternatives considered

* **A third app (`cms`, `learn`, `site`)** — rejected. It buys a clean name and costs a third build,
  deploy, session surface and shared-package consumer, to separate two surfaces whose *technical*
  requirements are the same. The split would be organisational, and ADR-0005 explicitly declined to
  split by organisation.
* **Move the CMS to `site` under auth, leaving `app` to learners as documented** — rejected, and it
  is the option that most directly "obeys" the existing docs. But it puts a block editor, a media
  picker and a course builder inside the app whose entire purpose is being static, cached and
  crawlable. `site` is auth-*aware*; it is not an application shell.
* **Put learner surfaces on `site` too, and keep `app` purely the CMS** — rejected as a whole, but
  **partially adopted**: lesson *reading* does go to `site`, because it is indexable content. A
  progress dashboard is not — it is per-user, uncacheable, and pointless to a crawler. Sending it to
  `site` would make the one app whose value is being cacheable serve a page that can never be cached.
* **Split `app` by role at the layout level with no route namespace** — rejected. Route structure is
  the honest place for a boundary this load-bearing; a layout-only split leaves `/courses` meaning
  two different things depending on who is logged in.

## Consequences

* Positive: two apps, as ADR-0005 intended, with the boundary now stated as the criterion rather
  than as a label that had quietly stopped matching. Learners get the root path. The CMS keeps every
  page it had, one segment deeper.
* Positive: the learner and the editor share one session, one API client, and one design system — a
  user who is both (which every one of our editors is) does not log in twice.
* Negative: `/studio` is a breaking change to every CMS bookmark. Accepted — the CMS has a handful of
  users, all of them us, and doing it later costs strictly more.
* Negative: one app now serves two audiences, so a role check is load-bearing for navigation. It is
  **not** a security boundary: the API authorises every request independently (SECURITY.md §2), and
  a learner who types `/studio` gets a UI they cannot use rather than data they should not see.
* Obligates: `apps/app` had `@nuxtjs/i18n` as a dependency but never registered it, so the CMS is
  English-only against rule 19. This ADR's implementation wires it up and covers the shared chrome
  and every learner string; **the CMS's own body strings remain untranslated** and are recorded in
  STATUS as owed.
* Obligates: lesson pages on `site`, with the body gated and progress controls hydrating in.

## References

* [ADR-0005](0005-two-app-frontend-monorepo.md) — the two-app split this restates rather than
  replaces.
* [ADR-0007](0007-unify-article-lesson.md) — a lesson is a content unit, which is why its page is
  `site`'s.
* [CHG-0040](../../CHANGELOG.md) — enrollment and progress, the slice that surfaced the drift.
* [FRONTEND_ARCHITECTURE.md](../FRONTEND_ARCHITECTURE.md)
