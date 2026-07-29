# ADR-0002 — B2C-first: no row-level multi-tenancy

Status: Accepted
Date: 2026-07-29
Deciders: Project owner

## Context

A reference transactional system (an ERP) stamps `TenantId`/`CompanyId` on every row for hard tenant
isolation. It is tempting to copy that. But DataBro's core asset is the **opposite**: a single global
catalog of content that every learner sees. Learners are global users; content is shared. Enterprise
team features are a late-phase concern.

## Decision

DataBro is **B2C-first**. There is a **single global content catalog** and `User` is a global
aggregate. **No row-level tenant discriminator** is added to the schema. Enterprise (Phase 4)
introduces an `Organization` aggregate that owns seats/cohorts/private dashboards **over the shared
content** — a bolt-on scope, not a schema-wide discriminator.

## Alternatives considered

* **Multi-tenant from day one** (tenant column everywhere) — premature for a content platform; fights
  the shared-catalog and read/cache model, and adds complexity with no Phase 1 benefit. Rejected.
* **Separate DB per tenant** — irrelevant for B2C shared content; only meaningful for isolated
  enterprise data far in the future. Rejected now.

## Consequences

* Positive: simpler schema, natural fit for shared content, clean caching/SEO, no tenant-scoping tax on
  every query.
* Trade-offs: when Enterprise arrives, org-scoping is added deliberately to the few entities that need
  it (progress, dashboards, seats), not retrofitted globally.
* Obligates: visibility is enforced by content status + visibility + role/ownership, not by a tenant
  wall (see [SECURITY.md](../SECURITY.md)).

## References

[CLAUDE.md](../../CLAUDE.md) → Tenancy Model; [DATABASE.md](../DATABASE.md).
