# DataBro — Product Requirements Document (PRD)

Status: Living document. Last reviewed 2026-07-29.

## 1. Vision

DataBro is a specialized online learning platform for AI, Data, and Software Engineering. It combines
high-quality written content (the acquisition wedge) with structured, interactive learning (the
retention and monetization engine). The long-term goal is a SaaS platform in the class of Real Python,
DataCamp, DeepLearning.AI, freeCodeCamp, Coursera, and Educative — but focused on the AI/Data/Software
domain.

## 2. Problem

Learners in AI/Data face fragmented resources: shallow blog posts, expensive bootcamps, and courses
that lack a coherent progression from fundamentals to applied projects. DataBro provides an opinionated,
structured path from article → course → project → certificate, with interactive practice and an AI
tutor.

## 3. Target Users (Personas)

* **The Self-Learner (primary, Phase 1)** — a developer or student learning Python/SQL/ML on their own
  time via searchable, high-quality tutorials. Arrives from Google. Wants depth and correctness.
* **The Structured Student (Phase 2)** — wants a guided path with courses, lessons, quizzes, and
  progress tracking.
* **The Practitioner (Phase 3)** — wants hands-on playgrounds, projects, certificates, and an AI tutor.
* **The Content Author/Editor (internal)** — creates and publishes content through the CMS.
* **The Enterprise Buyer (Phase 4)** — buys seats for a team, wants cohorts and analytics.

## 4. Positioning

* **Wedge:** SEO-driven articles/tutorials. The first thing that must be excellent.
* **Differentiator (later):** a coherent AI/Data curriculum with interactive practice and an AI tutor.
* **Moat:** content quality + learning-outcome data + community.

## 5. Scope by Phase

Full detail in [ROADMAP.md](ROADMAP.md). Summary:

* **Phase 1 (Foundation & Content):** architecture, auth, in-house CMS (articles), categories, tags,
  SEO, search.
* **Phase 2 (Learning):** learning paths, courses, modules, lessons, user progress, bookmarks, quizzes.
* **Phase 3 (Monetization & Interactivity):** billing, premium content, AI tutor, coding playground,
  certificates.
* **Phase 4 (Scale):** community, enterprise, analytics, mobile app.

## 6. Phase 1 Functional Requirements

### 6.1 Identity
* Public self-registration with email verification.
* Password login (JWT access + refresh) and social login (Google, GitHub).
* Roles: Reader, Author, Editor, Admin.
* Profile management; password reset.

### 6.2 Content (CMS)
* Authors create **Articles** composed of typed content blocks.
* Draft / publish workflow with version history.
* Scheduling (publish at a future time).
* Categories (hierarchical) and Tags (flat).
* Author profiles (byline, bio, avatar).
* SEO metadata per article (slug, meta, canonical, OG, JSON-LD).
* `visibility` field (Public/Premium) reserved; all Phase 1 content is Public.

### 6.3 Public Content Experience (site)
* Article reading pages (fast, SSG/ISR, crawlable).
* Category and tag listing pages.
* Homepage and topic landing pages.
* Search (keyword).
* RSS, sitemap, robots.

### 6.4 Search
* Keyword search over published articles (title, summary, body, tags) via PostgreSQL FTS.

### 6.5 Media
* Image upload to DigitalOcean Spaces; referenced by content blocks; responsive variants.

## 7. Non-Functional Requirements

* **Performance:** public article pages served static-fast (target < 1s TTFB via CDN/ISR); read paths
  cached in Redis.
* **SEO:** valid structured data, canonical URLs, sitemaps; Lighthouse SEO ≥ 95 on content pages.
* **Availability:** single-region to start; designed to scale horizontally on the read path.
* **Security:** OWASP-aligned; secrets never in source; least-privilege auth.
* **Accessibility:** WCAG 2.1 AA target for content pages.
* **Internationalization:** UI chrome in English + Bahasa Indonesia; article bodies authored per-locale.
* **Observability:** structured logging; health checks; error tracking.

## 8. Explicit Non-Goals (for now)

* No microservices.
* No row-level multi-tenancy (B2C-first — see [ADR-0002](DECISIONS.md)).
* No live code execution until Phase 3 (Playground).
* No native mobile app until Phase 4.
* No third-party headless CMS — the CMS is built in-house (see [ADR-0003](DECISIONS.md)).

## 9. Success Metrics

* Phase 1: organic traffic growth, indexed pages, article read-through rate, signup conversion.
* Phase 2: course enrollments, lesson completion, day-7/30 retention.
* Phase 3: paid conversion, MRR, playground usage, certificates issued.

## 10. Open Questions

Tracked in [STATUS.md](STATUS.md) and resolved via ADRs.
