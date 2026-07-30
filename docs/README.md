# DataBro Documentation

This is the documentation index. The master project instructions live in the root
[CLAUDE.md](../CLAUDE.md) and take precedence over everything here unless superseded by an ADR.

## Where to start

* New to the project? Read [../CLAUDE.md](../CLAUDE.md), then [PRD.md](PRD.md), then
  [ARCHITECTURE.md](ARCHITECTURE.md).
* Want current state / what's next? See [STATUS.md](STATUS.md).
* Setting up your machine? See [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md).

## Index

### Product & Planning
* [PRD.md](PRD.md) — product requirements, personas, scope.
* [ROADMAP.md](ROADMAP.md) — phased delivery plan.
* [STATUS.md](STATUS.md) — where we are, what's next.

### Architecture & Technical Spine
* [ARCHITECTURE.md](ARCHITECTURE.md) — modular monolith, clean architecture, communication.
* [MODULES.md](MODULES.md) — module catalog and responsibilities.
* [DATABASE.md](DATABASE.md) — schema conventions and Phase 1 tables.
* [CONTENT_MODEL.md](CONTENT_MODEL.md) — the content/block/versioning engine (core domain).
* [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) — two-app monorepo, rendering strategy.
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
