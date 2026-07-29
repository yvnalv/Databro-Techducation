# ADR-0005 — Two-app frontend in a pnpm monorepo

Status: Accepted
Date: 2026-07-29
Deciders: Project owner

## Context

DataBro has two very different frontend surfaces: a public, SEO-critical, cache-heavy content
experience, and an authenticated, dynamic learner/authoring app. They have different rendering
strategies, caching profiles, and threat models. A single blended app risks compromising SEO/perf for
the public surface or over-exposing the app surface.

## Decision

Ship **two Nuxt 4 applications** — `site` (public content, SSG/ISR) and `app` (authenticated) — inside
a **single pnpm workspace monorepo**. All shared code (design system + Tailwind preset + content-block
renderers in `packages/ui`, typed API access in `packages/api-client`, shared DTOs in `packages/types`)
is factored into workspace packages consumed by both apps.

## Alternatives considered

* **One app, hybrid rendering** — simplest for a solo dev, but couples two divergent rendering/caching/
  security profiles and complicates keeping the public surface lean. Considered; not chosen (owner
  preferred clean separation).
* **Two separate repos** — cleanest isolation, but duplicated design system/auth/API logic and two
  release processes — a real maintenance tax for a solo builder. Rejected.

## Consequences

* Positive: clean separation of concerns; `site` stays lean and crawlable; `app` can be dynamic; the
  content-block renderer is shared so CMS preview and public rendering never drift.
* Trade-offs: two build/deploy targets. Mitigated by one monorepo, pnpm-filtered CI, and shared
  packages — **nothing both apps use is copied**.
* Obligates: `site` is auth-aware, not logged-out-only (premium articles expose SEO + preview
  publicly). See [FRONTEND_ARCHITECTURE.md](../FRONTEND_ARCHITECTURE.md).

## References

[FRONTEND_ARCHITECTURE.md](../FRONTEND_ARCHITECTURE.md); [SEO.md](../SEO.md).
