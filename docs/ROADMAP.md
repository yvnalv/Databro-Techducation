# DataBro — Roadmap

Phased delivery. Each phase builds on the last without rewriting prior work. Dates are intentionally
omitted (solo, long-term); phases are ordered, not scheduled.

---

## Phase 1 — Foundation & Content

Goal: a fast, SEO-strong article platform with a real in-house CMS. This is the acquisition wedge.

* Project scaffolding: backend solution (Modular Monolith), frontend monorepo (site + app + packages).
* **Identity:** registration, email verification, password + Google/GitHub login, JWT + refresh, RBAC
  (Reader/Author/Editor/Admin).
* **Content (CMS):** Article aggregate, typed JSONB content blocks, draft/publish + version history,
  scheduling, categories, tags, authors.
* **Media:** image upload to DO Spaces with responsive variants.
* **SEO:** slugs, canonical, meta, OpenGraph, JSON-LD, sitemap, robots, RSS, 301 redirect tracking.
* **Search:** PostgreSQL full-text search over published articles.
* **Public site:** article pages (SSG/ISR), category/tag pages, homepage, search UI.
* Infra: Docker Compose dev, CI (build/test/lint + architecture-fitness), staging deploy on DO.

Exit criteria: an editor can author, version, schedule, and publish an SEO-complete article that is
indexed, searchable, and served fast from the public site.

---

## Phase 2 — Learning

Goal: turn content into structured learning with progress.

* **Learning module:** Learning Paths → Courses → Modules → Lessons (Lessons reuse the Content engine).
* **Assessment module:** Quizzes (per lesson) with attempts and scoring.
* **Progress:** per-user lesson/course completion, resume, streaks.
* **Bookmarks** and saved content.
* Enrollment model (free enrollment; paid gating arrives Phase 3).
* Course/lesson authoring surface added to the CMS (extends the existing content engine).

---

## Phase 3 — Monetization & Interactivity

Goal: revenue and hands-on practice.

* **Billing module:** subscription plans, checkout, entitlements engine (provider-abstracted, e.g.
  Stripe). Activate the reserved `visibility = Premium` gating.
* **AI module:** AI Tutor and AI-assisted features behind `ILlmProvider`; embeddings via pgvector.
* **Playground module:** sandboxed Python/SQL execution (execution-strategy ADR at module start).
* **Projects & Certificates:** project submissions and issued certificates.

---

## Phase 4 — Scale

Goal: community, teams, insight, reach.

* **Community:** discussions, comments, Q&A.
* **Enterprise:** `Organization` aggregate — seats, cohorts, private dashboards over shared content.
* **Analytics:** learning analytics, content performance, funnels.
* **Mobile app.**
* Infra scale-out: OpenSearch, possible Kubernetes, RabbitMQ for cross-service messaging.

---

## Cross-cutting, continuous

* Documentation and ADRs kept in sync with implementation.
* Test coverage for content publishing, authorization, SEO, and search from Phase 1 onward.
* Accessibility and i18n maintained as features ship.
