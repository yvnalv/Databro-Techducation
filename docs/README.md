# DataBro Documentation

This is the documentation index. The master project instructions live in the root
[CLAUDE.md](../CLAUDE.md) and take precedence over everything here unless superseded by an ADR.

## Where to start

* New to the project? Read [../CLAUDE.md](../CLAUDE.md), then [PRD.md](PRD.md), then
  [ARCHITECTURE.md](ARCHITECTURE.md).
* Want current state / what's next? See [STATUS.md](STATUS.md).
* Want the list of everything still owed, and by whom? See [OPEN_ITEMS.md](OPEN_ITEMS.md).
* Setting up your machine? See [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md).
* Looking for the design preview or a past briefing? See [ARTIFACTS.md](ARTIFACTS.md).
* Spotted something wrong on screen? Log it in [UI_DEFECTS.md](UI_DEFECTS.md).
* Picking up the UI work? [UI_REWORK.md](UI_REWORK.md) says which stage is next and what is in it.

## Index

### Product & Planning
* [PRD.md](PRD.md) — product requirements, personas, scope.
* [ROADMAP.md](ROADMAP.md) — phased delivery plan.
* [STATUS.md](STATUS.md) — where we are, what's next.
* [OPEN_ITEMS.md](OPEN_ITEMS.md) — everything outstanding, grouped by who has to act: decisions
  awaiting the product owner, manual work, surfaces that were never built, and operational debt.
* [ARTIFACTS.md](ARTIFACTS.md) — index of published, hosted pages: the interactive design preview
  and the build ledger. Stable URLs; each entry says what it is for and whether it ages.

### Architecture & Technical Spine
* [ARCHITECTURE.md](ARCHITECTURE.md) — modular monolith, clean architecture, communication.
* [MODULES.md](MODULES.md) — module catalog and responsibilities.
* [DATABASE.md](DATABASE.md) — schema conventions and Phase 1 tables.
* [CONTENT_MODEL.md](CONTENT_MODEL.md) — the content/block/versioning engine (core domain).
* [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) — two-app monorepo, rendering strategy.
* [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md) — colour, typography, spacing, components. Rendered as an
  interactive preview — see [ARTIFACTS.md](ARTIFACTS.md).
* [UI_PATTERNS.md](UI_PATTERNS.md) — page-level composition and what we deliberately diverge on.
* [UI_DEFECTS.md](UI_DEFECTS.md) — visual defects found by looking at the running app. Nothing in CI
  can see this category, so the register is the memory.
* [UI_REWORK.md](UI_REWORK.md) — the staged plan for the visual rework: what stage A delivered, and
  what B (icons, logo, favicon), C (primitives) and D (page layout) contain.
* [SEO.md](SEO.md) — SEO as a cross-cutting concern.
* [API_SPEC.md](API_SPEC.md) — REST conventions and Phase 1 endpoints.
* [ERROR_HANDLING.md](ERROR_HANDLING.md) — error envelope and codes.

### Rules & Standards
* [SECURITY.md](SECURITY.md) — auth, authorization, privacy, abuse.
* [BUSINESS_RULES.md](BUSINESS_RULES.md) — documented business rules.
* [CODING_STANDARDS.md](CODING_STANDARDS.md) — C#/TS conventions.
* [TESTING.md](TESTING.md) — testing strategy.
* [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md) — running the stack locally and verifying a change.
* [DEPLOYMENT.md](DEPLOYMENT.md) — environments and delivery.
* [GLOSSARY.md](GLOSSARY.md) — ubiquitous language.

### Decisions
* [DECISIONS.md](DECISIONS.md) — Architectural Decision Records (ADR) index.
* [adr/](adr/) — individual ADR files.

## Documentation discipline

* Docs are part of the product; they must stay synchronized with implementation.
* Every major decision is recorded as an ADR before or alongside the change.
* Update the root [../CHANGELOG.md](../CHANGELOG.md) with every meaningful change.
