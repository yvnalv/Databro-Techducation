# DataBro — Open Items

Everything outstanding, grouped by **who has to act**. [STATUS.md](STATUS.md) says where the project
is and what is next; this says what is owed and by whom.

An item leaves this file only when it is done or explicitly dropped. Dropped items keep a line saying
so — a register that quietly loses entries is worse than no register.

Last reviewed: 2026-08-18.

---

## 1. Needs a decision from the product owner

These are blocked on judgement, not effort. Each is recorded as **unmade** rather than defaulted,
because defaulting them silently is how a product acquires rules nobody chose.

| # | Item | Why it is open |
|---|---|---|
| D-1 | **Does passing a quiz gate lesson completion?** (AS-9) | `QuizAttemptSubmitted` is raised but deliberately kept internal. Wiring it to Learning is one interface and one registry line, and doing it without deciding would make the rule by accident. Easiest to answer after taking a quiz on screen. |
| D-2 | **Deliverability provider** — Resend / Postmark / SES | Deferred by [ADR-0016](adr/0016-transactional-email-transport.md). SMTP is the seam every one of them speaks, so waiting costs nothing until there is a domain, SPF/DKIM and a bounce rate. |
| D-3 | **Staging deploy on DigitalOcean** | Deferred deliberately; nothing depends on it while the stack runs locally. Becomes urgent the moment someone else needs to see this. |
| D-4 | **Partial credit on multiple-choice** | [ADR-0018](adr/0018-assessment-scoring-and-the-answer-key.md) chose all-or-nothing and explained why. Listed here because it is the decision most likely to be revisited once real learners hit a five-option question. |

---

## 2. Needs manual work outside the codebase

| # | Item | What to do |
|---|---|---|
| M-1 | **Dev learner accounts cannot sign in** | CHG-0048 enforced ID-2; 21 accounts are unconfirmed. Per account: sign in → **"Send it again"** → confirm via <http://localhost:8025>. `admin@databro.local` was confirmed at seed time and is unaffected. |
| M-2 | **Click through the quiz UI** | Built, tested and smoke-checked over the API, but the interactive parts — radio vs checkbox behaviour, the inline publish blockers, submitting from the lesson page — are client-side and were never driven by hand. See the test guide in the CHG-0050 discussion. |

---

## 3. Built but unreachable

The recurring failure on this project: a module ships without a surface. Worth watching as a pattern,
not just as three tickets.

| # | Item | State |
|---|---|---|
| U-1 | **Quiz attempt review in the CMS** | An author can write a quiz and a learner can take it; nothing shows who attempted what. No screen, no endpoint. |
| U-2 | ~~Learning-path curator~~ | **Done** — CHG-0044, after shipping in CHG-0043 with no surface. |
| U-3 | ~~Quiz authoring and learner UI~~ | Written in the same session as the module; unverified (M-2). |

---

## 4. Scope still owed

| # | Item | Phase | Note |
|---|---|---|---|
| S-1 | **Social login (Google/GitHub)** | 1 | Never built. `API_SPEC.md` now lists it under an explicit "Not built" heading rather than describing it as though it exists. |
| S-2 | **`PATCH /me`** — profile editing | 1 | Same. Returns 405 today. |
| S-3 | **Bookmarks** | 2 | Untouched. |
| S-4 | **Streaks** | 2 | Untouched. |
| S-5 | **`/studio` Indonesian strings** | — | i18n is wired ([ADR-0015](adr/0015-authenticated-app-hosts-both-audiences.md)); the CMS's own labels are still hardcoded English, against rule 19. Mechanical now. |

---

## 5. Operational debt

| # | Item | Risk if ignored |
|---|---|---|
| O-1 | **Outbox retention** | Processed rows are kept as an audit and accumulate without bound. Negligible now; a sweep is owed before it is not ([ADR-0017](adr/0017-transactional-outbox.md)). |
| O-2 | **Dead-lettered messages have no operational surface** | A parked message is only visible via SQL. |
| O-3 | **Cross-subdomain session cookie** | Works locally only because cookies ignore port. Production needs an explicit parent `domain`, and it cannot be verified from here. |
| O-4 | **Redis is provisioned and unused** | Nothing caches. Either wire it or drop it from compose — an unused dependency reads as if something depends on it. |
| O-5 | **No analyzer ruleset for C#** | ESLint covers the frontend; the backend has no equivalent gate. |
| O-6 | **Premium gating is reserved, not enforced** | Badge, preview and JSON-LD paywall declaration exist; the full body still renders. Correct until Billing (Phase 3), but it is a gate that looks real and is not. |

---

## 6. Verification status

Everything committed to date is built, linted, typechecked and tested — 312 backend, 90 frontend.

CHG-0050 (the quiz surfaces) was written while the build tooling was unavailable and **held
uncommitted until it returned**, then verified before committing. That caught one real defect (an
unused import failing lint), which is the argument for not committing on trust.

What automated checks still cannot reach: the interactive client-side behaviour — radio versus
checkbox on the answer key, the inline publish blockers, and submitting a quiz from the lesson page.
Those are M-2.

---

## Two habits this register exists to enforce

1. **Build the surface in the same slice as the module.** Learning paths shipped without one
   (CHG-0043 → CHG-0044), quizzes did the same, and attempt review still has none. A module with no
   way to reach it is not done, however green its tests are.

2. **Check the doc against the running API before trusting it.** `API_SPEC.md` described five auth
   endpoints that returned 404, and `STATUS.md` claimed learning paths had an API that had never been
   written. Both were found by probing the live stack, not by reading.
