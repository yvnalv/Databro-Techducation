# CLAUDE.md

# DataBro — Master Project Instructions

## Purpose

This file is the single source of truth for AI coding assistants, developers, architects, and
contributors working on DataBro.

All design, implementation, architectural, and business decisions must align with this document.

If any implementation conflicts with this document, this document takes precedence unless explicitly
superseded by an approved Architectural Decision Record (ADR) in [docs/DECISIONS.md](docs/DECISIONS.md).

---

# Project Overview

## Project Name

DataBro

## Project Type

Production-grade online learning platform (SaaS) specializing in AI, Data, and Software Engineering
education.

## Long-Term Vision

Build a scalable SaaS learning platform that can compete with Real Python, DataCamp, DeepLearning.AI,
freeCodeCamp, Coursera, and Educative — specializing in:

* Artificial Intelligence
* Data Science
* Machine Learning
* Deep Learning
* LLM Engineering
* RAG
* AI Agents
* Data Engineering
* Python
* SQL
* Software Engineering

## What DataBro Is Not

DataBro is **not** a blogging platform. Content is one part of a larger learning ecosystem:
structured paths, interactive courses, coding playgrounds, quizzes, projects, an AI tutor, community,
certifications, and enterprise learning.

---

# Core Principles

1. Learner outcomes over vanity content volume.
2. Maintainability over premature optimization.
3. Security and privacy by default.
4. Read-optimized: the platform is read-heavy; caching and SEO are load-bearing, not afterthoughts.
5. One content engine: articles and lessons share the same primitive.
6. Provider independence: never hard-couple business logic to a single LLM, payment, or email vendor.
7. Architecture should allow future growth (courses, billing, AI, enterprise) without a rewrite.
8. Documentation is part of the product.
9. Every major decision is recorded as an ADR.
10. Modular boundaries are enforced, not merely encouraged.

---

# Technology Stack

## Backend

* .NET 9
* ASP.NET Core Web API
* Entity Framework Core
* PostgreSQL (Npgsql)
* Redis (caching, sessions, rate limiting)
* Hangfire (background jobs)

## Frontend

* Vue 3
* Nuxt 4
* TypeScript
* Tailwind CSS
* Pinia
* pnpm workspaces (monorepo: two apps + shared packages)

## Infrastructure

* Docker / Docker Compose
* GitHub / GitHub Actions
* Nginx
* DigitalOcean (Droplets, Managed PostgreSQL, Spaces for object storage)

## Future Infrastructure

* Kubernetes
* OpenSearch (search upgrade path from PostgreSQL FTS)
* pgvector (embeddings for AI features)
* RabbitMQ (cross-module / future cross-service messaging)

---

# Architecture

## Architecture Style

Use **Modular Monolith**.

Do **NOT** use microservices for the foreseeable roadmap.

Modules must be designed so they can be extracted into services later if necessary. See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [ADR-0001](docs/DECISIONS.md).

## Clean Architecture

Each module contains:

* Domain — entities, value objects, business rules. Depends on nothing.
* Application — use cases, commands/queries, DTOs, port interfaces.
* Infrastructure — EF Core persistence, external integrations (storage, email, LLM).
* API — thin controllers / minimal endpoints and contracts.

Rules:

* Domain must not depend on Infrastructure.
* Controllers are thin; business logic lives in Application; business rules live in Domain.
* Modules remain loosely coupled and are enforced in CI by an architecture-fitness test.

## Module Layout

```
Modules/
├── Identity/
├── Content/        (the CMS: articles, blocks, versioning, taxonomy)
├── Media/          (asset upload/storage)
├── Search/         (indexing + query)
└── Platform/       (shared kernel: audit, errors, caching, outbox scaffolding)
```

Future modules (later phases): `Learning` (paths/courses/lessons/progress), `Assessment`
(quizzes/projects), `Billing`, `AI`, `Playground`, `Community`, `Enterprise`, `Analytics`,
`Notification`.

See [docs/MODULES.md](docs/MODULES.md).

---

# Inter-Module Communication

* Modules MUST NOT read or write another module's tables directly.
* Modules communicate through published **integration events** (in-process via a mediator) and public
  **application-service contracts**.
* Effects that must be reliable but may be eventually consistent use a **transactional outbox**.
* Each module owns its own EF Core schema / table prefix to keep future extraction mechanical.
* Module boundaries are enforced in CI (NetArchTest or equivalent).

---

# Tenancy Model

DataBro is **B2C-first**. See [ADR-0002](docs/DECISIONS.md).

* There is a **single global content catalog**. All learners see the same articles/courses.
* `User` is a first-class **global** aggregate — there is **no row-level tenant discriminator** on
  the schema.
* **Enterprise** (Phase 4) introduces an `Organization` aggregate that owns seats, cohorts, and
  private progress dashboards **over shared content**. It is a bolt-on scope, not a rewrite.

Do NOT stamp `TenantId`/`CompanyId` onto entities. This is a deliberate departure from transactional
multi-tenant systems.

---

# Content Model (Core Domain)

The educational hierarchy is:

```
Learning Path → Course → Module → Lesson → Content Blocks → Quiz → Project → Certificate
```

Foundational rule: **an Article and a Lesson are the same primitive** — a renderable unit composed of
typed **Content Blocks**. A Lesson is a Content unit that additionally belongs to a Module and carries
learning metadata. Build the content/versioning engine once. See [ADR-0007](docs/DECISIONS.md) and
[docs/CONTENT_MODEL.md](docs/CONTENT_MODEL.md).

Content storage:

* A Content unit has **typed blocks persisted as JSONB**, versioned as **draft** and **published**
  snapshots. See [ADR-0004](docs/DECISIONS.md).
* Every Content unit carries SEO metadata (slug, meta description, canonical, OpenGraph, JSON-LD) and
  a `visibility` field (`Public` / `Premium`) reserved from day one, even though billing is Phase 3.

Every Lesson (Phase 2+) must define:

* Learning objectives
* Prerequisites
* Estimated study time
* Difficulty
* Exercises
* Summary
* Related lessons

---

# Frontend Architecture

Two applications in a **pnpm monorepo**. See [ADR-0005](docs/DECISIONS.md) and
[docs/FRONTEND_ARCHITECTURE.md](docs/FRONTEND_ARCHITECTURE.md).

```
frontend/
├── apps/
│   ├── site/   (public content — Nuxt SSG/ISR, SEO + CDN critical)
│   └── app/    (authenticated learner app — SSR/SPA: dashboard, progress, playground)
└── packages/
    ├── ui/         (design system + Tailwind preset)
    ├── api-client/ (typed API client)
    └── types/      (shared TypeScript types)
```

* Boundary is drawn by **rendering need**, not by feature. Anything that must be indexed/cached lives
  in `site`.
* A **premium** Content unit renders its SEO metadata and preview on `site` while gating the full body
  behind auth. `site` is NOT "logged-out only."
* No hardcoded design tokens or duplicated auth/API logic across apps — shared via `packages/*`.

---

# Identity & Authorization

## Authentication

* Public self-registration with email verification.
* Password login: JWT access token + refresh token.
* Social login: **Google** and **GitHub** (this audience expects GitHub).
* Built on ASP.NET Core Identity.

## Authorization

* Use RBAC. Never hardcode permissions.
* Phase 1 roles: `Reader`, `Author`, `Editor`, `Admin`.
* Permission naming: `Content.View`, `Content.Create`, `Content.Edit`, `Content.Publish`,
  `Content.Delete`, `Media.Upload`, `User.Manage`.
* Publishing is a distinct permission from authoring (an Author drafts; an Editor/Admin publishes).

See [docs/SECURITY.md](docs/SECURITY.md).

---

# SEO

SEO is a **cross-cutting concern, not a module**. See [docs/SEO.md](docs/SEO.md).

* Every public Content unit exposes: canonical URL, unique slug, meta title/description, OpenGraph +
  Twitter cards, and JSON-LD structured data (`Article`, later `Course`).
* Platform services provide `sitemap.xml`, `robots.txt`, and RSS.
* The `site` app renders via SSG/ISR so pages are static-fast and crawlable.
* Slugs are immutable once published; changing a slug creates a 301 redirect record.

---

# Search

* Phase 1: **PostgreSQL full-text search** (tsvector, weighted, trigram fallback for typos).
* Future: **OpenSearch** for relevance, facets, and scale — an ADR'd upgrade, not day one.
* Future: **pgvector** semantic search for AI-powered discovery.

---

# AI Features (Future)

Planned: AI Tutor, AI Code Reviewer, AI Quiz Generator, AI Learning Recommendation, AI Search, AI
Exercise Generator.

* **Never tightly couple business logic to a single LLM provider.** All AI access goes through an
  `ILlmProvider` / `IEmbeddingProvider` abstraction in the `AI` module.
* Prompts, model IDs, and provider selection are configuration, not hardcoded.

---

# Interactive Playground (Future)

Sandboxed Python/SQL execution is a security-critical subsystem (Phase 3). Architecture must not
assume where execution happens:

* Candidate approaches: client-side (Pyodide/WASM, sql.js) vs. server-side isolated sandboxes
  (Judge0 / gVisor / Firecracker).
* The decision is deferred to a dedicated ADR when the Playground module begins.

---

# API Standards

Base route: `/api/v1`

Examples:

```
/api/v1/articles
/api/v1/categories
/api/v1/tags
/api/v1/search
```

## Response Envelope

Success:

```json
{ "success": true, "data": {} }
```

Failure:

```json
{ "success": false, "error": { "code": "validation_failed", "message": "…", "details": [] } }
```

See [docs/API_SPEC.md](docs/API_SPEC.md) and [docs/ERROR_HANDLING.md](docs/ERROR_HANDLING.md).

---

# Database Standards

## Primary Keys

Use GUID (UUID) for business entities. Avoid `INT IDENTITY` for domain aggregates.

## Standard Audit Fields

Every table contains:

* CreatedAt / CreatedBy
* UpdatedAt / UpdatedBy
* DeletedAt / DeletedBy / IsDeleted (soft delete)

## Content-Specific

* Content bodies are stored as JSONB (`draft_blocks`, `published_blocks`).
* Slugs are unique and indexed; redirects are tracked.

See [docs/DATABASE.md](docs/DATABASE.md).

---

# Coding Standards

## C#

`Article`, `ContentBlock`, `LearningPath`, `PublishArticleCommand`.

## SQL (tables)

`Articles`, `ContentBlocks`, `Categories`, `Tags`.

## API (routes)

`articles`, `content-blocks`, `learning-paths`.

Avoid: God classes, massive services, circular dependencies, shared mutable state, static business
logic, duplicated code. Prefer: SOLID, dependency injection, Clean Architecture, lightweight DDD.

See [docs/CODING_STANDARDS.md](docs/CODING_STANDARDS.md).

---

# Testing Strategy

Required: Unit tests + Integration tests.

High-priority coverage:

* Content publishing/versioning
* Authorization and tenancy boundaries
* SEO metadata and redirects
* Search indexing

See [docs/TESTING.md](docs/TESTING.md).

---

# Internationalization

* Documentation language: **English**.
* Application default language: **English**. Secondary language: **Bahasa Indonesia**.
* No hardcoded UI text — all strings go through the i18n layer; locale dictionaries stay structurally
  identical (same keys on both sides).
* Adding or changing any user-facing string is not complete until both locales are updated.

Note: article *content* itself is authored per-locale as separate Content units linked by a
translation group; the i18n rule above governs **UI chrome**, not long-form article bodies.

---

# Documentation Rules

Documentation is part of the product. Whenever a major change occurs, update:

* Architecture, Database, API Spec, Module docs, Business Rules, Decisions (ADR).

Documentation must remain synchronized with implementation. See
[docs/README.md](docs/README.md) for the index and [docs/STATUS.md](docs/STATUS.md) for current state.

## CHANGELOG Rules

`CHANGELOG.md` (repo root) is the immutable historical record. A task is not complete until it is
updated.

* Reverse chronological order — newest entries at the top.
* Entries use ids `CHG-NNNN` (sequential, zero-padded to four digits), never reused or renumbered.
* Timestamps are always UTC, formatted `YYYY-MM-DD HH:mm:ss UTC`.
* Rollbacks are recorded as new entries, never by editing the original.

---

# Version Control

* Commit messages are concise and imperative.
* **Do NOT attribute commits to AI assistants.** No AI co-author trailers on any commit or PR.
* Never commit secrets, connection strings, or API keys.
* Commit or push only when explicitly asked.

---

# Non-Negotiable Rules

1. Use .NET 9.
2. Use PostgreSQL.
3. Use Vue 3 + Nuxt 4 + TypeScript + Tailwind.
4. Use Modular Monolith + Clean Architecture.
5. DataBro is B2C-first — no row-level tenant discriminator; Organization is a Phase 4 bolt-on.
6. Article and Lesson share one Content/block engine.
7. Content blocks are stored as versioned JSONB (draft + published).
8. Two-app frontend in a pnpm monorepo with shared packages.
9. Premium content still exposes SEO metadata/preview publicly.
10. Modules never access another module's tables directly; communicate via events/contracts.
11. Design modules for future service extraction.
12. Never physically delete content — soft delete and version history.
13. Use RBAC; permissions are configurable; publishing is distinct from authoring.
14. Never hard-couple to a single LLM, payment, or email provider — abstract behind interfaces.
15. SEO is load-bearing: slugs, canonical, structured data, sitemap from Phase 1.
16. Slugs are immutable once published; slug changes create 301 redirects.
17. Reserve `visibility` (Public/Premium) on content from day one; billing arrives Phase 3.
18. Documentation must be maintained continuously; every major decision is an ADR.
19. English is the default UI language; Bahasa Indonesia must be supported for UI chrome.
20. Security and privacy take precedence over convenience.
21. Do not attribute commits to AI assistants.
