# DataBro — Glossary (Ubiquitous Language)

Shared vocabulary. Use these terms consistently in code, docs, and UI.

## Content domain

* **Content Unit** — a renderable piece of learning material composed of typed content blocks. Both
  Articles and Lessons are Content Units.
* **Article** — a standalone, SEO-oriented Content Unit (Phase 1's primary content type).
* **Content Block** — a typed element of a Content Unit (`heading`, `paragraph`, `code`, etc.), stored
  as JSON.
* **Draft** — the mutable working copy of a Content Unit (`draft_blocks`).
* **Published snapshot** — the immutable public copy (`published_blocks`) produced at publish time.
* **Version** — an append-only historical snapshot in `article_versions`.
* **Slug** — the URL-safe identifier for a Content Unit; immutable once published.
* **Visibility** — `Public` or `Premium`; premium gates the body (P3) but never the SEO metadata.
* **Category** — a hierarchical taxonomy node an article belongs to (at most one).
* **Tag** — a flat taxonomy label; an article may have many.
* **Redirect** — a stored 301 mapping from an old path to a new one.
* **Translation group** — a set of locale variants of the same article, linked by `translation_group_id`.

## Learning domain (Phase 2+)

* **Learning Path** — an ordered sequence of Courses toward a goal.
* **Course** — a structured collection of Modules.
* **Course Module** — a grouping of Lessons within a Course. (Distinct from a *code module*.)
* **Lesson** — a Content Unit that belongs to a Course Module and carries learning metadata.
* **Progress** — a user's completion state for lessons/courses.
* **Enrollment** — a user's association with a Course.
* **Quiz** — an assessment bound to a Lesson.
* **Project** — an applied deliverable (P3).
* **Certificate** — proof of course completion (P3).

## Platform / architecture

* **Module (code)** — an independently-bounded backend component (Clean Architecture), e.g. `Content`,
  `Identity`. Not to be confused with a *Course Module*.
* **Integration Event** — a domain-meaningful message published across module boundaries (e.g.
  `ArticlePublished`).
* **Application-service contract** — a public interface a module exposes for in-process consumption.
* **Outbox** — the transactional table ensuring reliable event dispatch.
* **Shared Kernel (`Platform`)** — cross-cutting building blocks shared by all modules.

## Identity

* **Reader** — a standard learner account.
* **Author** — may create/edit drafts.
* **Editor** — may publish/unpublish/schedule and manage taxonomy.
* **Admin** — full administrative access.

## Frontend

* **site** — the public, SEO-critical Nuxt app (SSG/ISR).
* **app** — the authenticated Nuxt app (dashboard, authoring, later playground).
* **Renderer registry** — the shared mapping of block `type` → Vue component, used by both apps.

## Enterprise (Phase 4)

* **Organization** — a team/company buyer that owns seats and cohorts over shared content. The only
  place org-scoping enters the model.
* **Seat** — a paid membership slot within an Organization.
* **Cohort** — a group of learners within an Organization progressing together.
