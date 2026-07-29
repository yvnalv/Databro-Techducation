# DataBro — Coding Standards

Readability over cleverness. Consistency over personal preference. These standards are enforced by
linters/analyzers and CI where possible.

## 1. General

* Prefer clear names over comments; comment the *why*, not the *what*.
* Small, single-responsibility units. No God classes, no massive services.
* No duplicated business logic; extract shared logic into the right layer.
* Fail fast with clear errors; validate at boundaries.
* No dead code, no commented-out code in commits.

## 2. Backend (C# / .NET 9)

### Structure
* Clean Architecture per module: Domain / Application / Infrastructure / Api. Dependencies point inward.
* Controllers/endpoints are **thin** — they translate HTTP to Application calls and back. No business
  logic.
* Business rules live in **Domain**; use cases (orchestration) live in **Application**; persistence and
  integrations live in **Infrastructure**.

### Naming
* Types: `PascalCase` (`Article`, `PublishArticleCommand`, `ArticleRepository`).
* Interfaces: `I`-prefixed (`IArticleRepository`, `ILlmProvider`).
* Async methods end with `Async`.
* Commands/queries: `VerbNounCommand` / `GetNounQuery`.

### Patterns
* CQRS-lite: commands (writes) and queries (reads) separated in Application; a mediator dispatches them.
* Repositories expose intent, not `IQueryable`, across module boundaries.
* Validation via FluentValidation at the Application boundary; invariants enforced in Domain.
* Prefer immutability for value objects; rich domain entities (behavior, not anemic bags).
* No static mutable state; everything via DI.
* Return `Result`/typed errors from Application; map to the HTTP envelope in Api.

### EF Core
* Module-owned `DbContext` and schema. Configurations in `IEntityTypeConfiguration<T>` classes.
* Migrations reviewed and named meaningfully. No auto-migrate in production.
* No cross-module navigation properties/FKs; reference other modules by id.
* Global query filter for soft delete; `IgnoreQueryFilters` only in reviewed admin paths.

### Async & performance
* Async all the way for I/O; no `.Result`/`.Wait()`.
* Read paths are cache-aware (Redis) with explicit invalidation on writes.

## 3. Frontend (TypeScript / Vue 3 / Nuxt 4)

* TypeScript **strict** mode across all workspaces. No `any` without justification.
* Composition API + `<script setup>`. Components presentational where possible.
* Data fetching in composables/pages; shared logic in `packages/*`, never copied between apps.
* Pinia for client state; server state via Nuxt data utilities.
* Styling via Tailwind + the shared `packages/ui` preset — single source of design tokens. No ad-hoc
  hex colors or duplicated component styles.
* All user-facing strings via i18n (`t('…')`); `en` and `id` dictionaries structurally identical.
* Content-block rendering uses the shared renderer registry (one renderer, both apps).

## 4. API & contracts

* REST conventions per [API_SPEC.md](API_SPEC.md); errors per [ERROR_HANDLING.md](ERROR_HANDLING.md).
* DTOs are the contract; `packages/types` mirrors them; keep them in lockstep.

## 5. Naming across layers (example)

| Concept | C# type | SQL table | API route |
|---|---|---|---|
| Article | `Article` | `articles` | `articles` |
| Content block | `ContentBlock` | (JSONB in `articles`) | `content-blocks` (P2 where standalone) |
| Learning path | `LearningPath` | `learning_paths` | `learning-paths` |

## 6. Git & reviews

* Small, focused commits; imperative messages (`Add article publish command`).
* **No AI attribution** on commits or PRs.
* A change is not done until: tests pass, docs/ADR/CHANGELOG updated, both i18n locales updated (if UI
  strings changed).

## 7. Tooling

* Backend: `.editorconfig`, nullable reference types on, analyzers as warnings-as-errors in CI,
  NetArchTest for boundaries.
* Frontend: ESLint + Prettier + `vue-tsc` typecheck in CI.
