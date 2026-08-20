# DataBro — Open Items

Everything outstanding, grouped by **who has to act**. [STATUS.md](STATUS.md) says where the project
is and what is next; this says what is owed and by whom.

An item leaves this file only when it is done or explicitly dropped. Dropped items keep a line saying
so — a register that quietly loses entries is worse than no register.

Last reviewed: 2026-08-19.

---

## 1. Needs a decision from the product owner

These are blocked on judgement, not effort. Each is recorded as **unmade** rather than defaulted,
because defaulting them silently is how a product acquires rules nobody chose.

| # | Item | Why it is open |
|---|---|---|
| ~~D-1~~ | ~~Does passing a quiz gate lesson completion?~~ (AS-9) | **Decided 2026-08-19: yes, and shipped (CHG-0052, S-6).** A lesson with a published quiz requires a passing attempt; a lesson with no quiz completes as before. Built as a synchronous `IQuizGate` query rather than by consuming `QuizAttemptSubmitted` — a decision-time check cannot be eventually consistent without refusing a just-passed learner. |
| D-2 | **Deliverability provider** — Resend / Postmark / SES | Deferred by [ADR-0016](adr/0016-transactional-email-transport.md). SMTP is the seam every one of them speaks, so waiting costs nothing until there is a domain, SPF/DKIM and a bounce rate. |
| D-3 | **Staging deploy on DigitalOcean** | Deferred deliberately; nothing depends on it while the stack runs locally. Becomes urgent the moment someone else needs to see this. |
| D-4 | **Partial credit on multiple-choice** | [ADR-0018](adr/0018-assessment-scoring-and-the-answer-key.md) chose all-or-nothing and explained why. Listed here because it is the decision most likely to be revisited once real learners hit a five-option question. |

---

## 2. Needs manual work outside the codebase

| # | Item | What to do |
|---|---|---|
| M-1 | **Dev learner accounts cannot sign in** | CHG-0048 enforced ID-2; 21 accounts are unconfirmed. Per account: sign in → **"Send it again"** → confirm via <http://localhost:8025>. `admin@databro.local` was confirmed at seed time and is unaffected. |
| ~~M-2~~ | ~~Click through the quiz UI~~ | **Done (2026-08-19)** — driven by hand in a headless browser against the running stack, on a real published lesson page. Confirmed: the answer control is a radio on single-choice and enforces single selection; publish blockers fire inline; submitting from the lesson page scores and reveals the key only afterwards. One adjacent, non-quiz observation: `POST …/enrollments/{course}/lessons/{id}/visit` returns **422** for a signed-in but unenrolled reader (the progress bar degrades correctly to "Join course"), see O-7. |

| ~~M-3~~ | ~~**Register the Google and GitHub OAuth apps**~~ | **Done (2026-08-20).** The owner registered both apps and filled the four `.env` values, so social login (CHG-0061) can authenticate against the running stack. A production deploy still needs a **second GitHub app** (one callback URL per app) — recorded in ADR-0019 and LOCAL_DEVELOPMENT, owed with the first deploy. |

---

## 3. Built but unreachable

The recurring failure on this project: a module ships without a surface. Worth watching as a pattern,
not just as three tickets. All three are now closed (U-1 in CHG-0051) — the pattern is what stays on
the record, so the next module is built with its surface rather than owing one.

| # | Item | State |
|---|---|---|
| U-1 | ~~Quiz attempt review in the CMS~~ | **Done — CHG-0051.** `GET /api/v1/authoring/quizzes/{id}/attempts` and `/studio/quizzes/{id}/attempts`: a roll-up of who submitted, their score and pass/fail, learner names resolved through `IUserDirectory`. No per-question selections and no answer key. Driven end to end in a browser. |
| U-2 | ~~Learning-path curator~~ | **Done** — CHG-0044, after shipping in CHG-0043 with no surface. |
| U-3 | ~~Quiz authoring and learner UI~~ | **Verified** — shipped in CHG-0050, driven end to end in a browser on 2026-08-19 (M-2). |

---

## 4. Scope still owed

| # | Item | Phase | Note |
|---|---|---|---|
| ~~S-1~~ | ~~**Social login (Google/GitHub)**~~ | 1 | **Done — CHG-0061 (ADR-0019).** Manual OAuth behind `IExternalIdentityProvider`, link-by-verified-email (ID-3), signed state, one-time code handoff. Owner registers the OAuth apps (M-3) before a live sign-in works; the code is verified by 8 unit + 6 integration tests. |
| S-2 | **`PATCH /me`** — profile editing | 1 | Same. Returns 405 today. |
| ~~S-3~~ | ~~**Bookmarks**~~ | 2 | **Done (CHG-0059).** Courses and lessons; articles deliberately deferred. |
| S-4 | **Streaks** | 2 | Untouched. |
| ~~S-5~~ | ~~**`/studio` Indonesian strings**~~ | — | **Done (CHG-0054 … CHG-0058).** All 16 studio files, 347 keys, both locales at parity. |
| ~~S-6~~ | ~~Gate lesson completion on a passing quiz~~ | 2 | **Done — CHG-0052.** A lesson with a published quiz cannot be completed until passed, via a synchronous `IQuizGate` query (not the submit event, which would refuse a just-passed learner). Draft quizzes do not gate; a quiz added after completion does not revoke it. Learner sees a message pointing at the quiz, both locales. 5 Learning tests. |

---

## 5. Operational debt

| # | Item | Risk if ignored |
|---|---|---|
| O-1 | **Outbox retention** | Processed rows are kept as an audit and accumulate without bound. Negligible now; a sweep is owed before it is not ([ADR-0017](adr/0017-transactional-outbox.md)). |
| O-2 | **Dead-lettered messages have no operational surface** | A parked message is only visible via SQL. |
| O-3 | **Cross-subdomain session cookie** | Works locally only because cookies ignore port. Production needs an explicit parent `domain`, and it cannot be verified from here. |
| ~~O-4~~ | ~~**Redis is provisioned and unused**~~ | **Addressed — CHG-0061.** Redis now backs the single-use OAuth handoff code (ADR-0019) via `IDistributedCache`. It is a real dependency of a live social sign-in; a Redis-less run falls back to an in-memory cache (tests do this). General response/read caching is still unbuilt, but the dependency is no longer inert. |
| O-5 | **No analyzer ruleset for C#** | ESLint covers the frontend; the backend has no equivalent gate. |
| O-6 | **Premium gating is reserved, not enforced** | Badge, preview and JSON-LD paywall declaration exist; the full body still renders. Correct until Billing (Phase 3), but it is a gate that looks real and is not. |
| O-7 | **Lesson-visit call 422s for an unenrolled reader** | `LessonProgressBar` fires `POST …/enrollments/{course}/lessons/{id}/visit` on every lesson view; for a signed-in but unenrolled learner it returns 422. The UI degrades correctly to "Join course", so it is console-only noise — but decide whether the component should withhold the call until enrolled, or whether 422 is the right status for "not enrolled". Found while verifying M-2; not a quiz-surface defect. |

---

## 6. Verification status

Everything committed to date is built, linted, typechecked and tested — 346 backend, 90 frontend.

CHG-0050 (the quiz surfaces) was written while the build tooling was unavailable and **held
uncommitted until it returned**, then verified before committing. That caught one real defect (an
unused import failing lint), which is the argument for not committing on trust.

The interactive client-side behaviour automated checks could not reach — radio versus checkbox on
the answer key, the inline publish blockers, and submitting a quiz from the lesson page — was driven
by hand in a headless browser on 2026-08-19 and holds (M-2, now done). The browser step is not yet a
committed test; a Playwright harness against the running stack is the way to keep it from regressing.

Social login (CHG-0061) was driven **live** on 2026-08-20: a real Google sign-in through the consent
screen returned to the app authenticated. It surfaced one non-code snag — a containerised API keeps
the environment it was created with, so the OAuth secrets have to be applied by recreating the
container, not by the hot-reload (now documented in LOCAL_DEVELOPMENT §"Applying the values"). The
faked-provider path is covered by 6 committed integration tests; the live consent screen, like the
quiz UI, is not yet a committed test and is the same argument for a Playwright harness.

---

## Two habits this register exists to enforce

1. **Build the surface in the same slice as the module.** Learning paths shipped without one
   (CHG-0043 → CHG-0044), quizzes did the same, and attempt review still has none. A module with no
   way to reach it is not done, however green its tests are.

2. **Check the doc against the running API before trusting it.** `API_SPEC.md` described five auth
   endpoints that returned 404, and `STATUS.md` claimed learning paths had an API that had never been
   written. Both were found by probing the live stack, not by reading.
