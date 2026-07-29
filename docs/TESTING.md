# DataBro — Testing Strategy

Tests exist to protect business correctness and enable fearless refactoring, not to chase a coverage
number. Prioritize the areas where bugs hurt most.

## 1. Test pyramid

* **Unit tests (most):** Domain rules and Application use cases in isolation. Fast, no I/O.
* **Integration tests (fewer):** module slices against a real PostgreSQL (Testcontainers) — repositories,
  publishing flow, search indexing, auth.
* **End-to-end (few, later):** critical user journeys across `site`/`app` (Playwright) once the frontend
  exists.
* **Architecture-fitness tests:** enforce module boundaries and dependency direction (NetArchTest) — run
  in CI on every build.

## 2. High-priority coverage (Phase 1)

* **Content publishing & versioning:** draft→publish snapshotting, version immutability, scheduled
  publish, unpublish, restore-to-draft. (Rules CT-1…CT-9.)
* **Slug & redirects:** uniqueness, immutability-after-publish, 301 creation on change. (CT-2, CT-3.)
* **Authorization:** authoring vs. publishing separation; drafts/premium not visible to the public.
  (SECURITY §2–3; CT-4, CT-6, CT-10.)
* **Search:** only published/public content indexed; index rebuildable from source. (SR-1, SR-2.)
* **Identity:** registration, email verification gating, refresh-token rotation/reuse detection.
* **SEO metadata:** correct canonical/robots/structured-data outputs; hreflang for locale variants.

## 3. Conventions

* Test naming: `MethodOrBehavior_Scenario_ExpectedResult`.
* Arrange-Act-Assert; one logical assertion focus per test.
* No shared mutable fixtures that create test coupling; build data via builders/factories.
* Integration tests spin up ephemeral PostgreSQL (and Redis where needed) via Testcontainers; no shared
  dev DB.
* Deterministic: control time via `IClock`; no reliance on wall-clock or ordering.

## 4. Frontend testing

* Component/unit tests (Vitest + Vue Test Utils) for `packages/ui` (esp. content-block renderers — they
  must render every block type safely, including unknown-type fallback).
* Type safety via `vue-tsc` in CI.
* E2E (Playwright) for read journeys (article page renders, search) and, later, auth flows.

## 5. Non-functional checks

* SEO/perf: Lighthouse CI budget on key content pages (SEO ≥ 95, Perf ≥ 90).
* Accessibility: automated a11y checks (axe) on content pages.
* Security: dependency vulnerability scans (`dotnet list package --vulnerable`, `pnpm audit`) in CI.

## 6. CI gates

A pull request must pass: build, unit + integration tests, architecture-fitness tests, lint/typecheck,
and vulnerability scan before merge. Coverage is reported but the gate is on the high-priority suites,
not a global percentage.

## 7. What we deliberately don't over-test

* Framework glue and trivial mappers.
* Third-party libraries (trust their tests; test our usage at integration level).
