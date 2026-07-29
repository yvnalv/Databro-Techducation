# ADR-0001 — Modular Monolith with Clean Architecture

Status: Accepted
Date: 2026-07-29
Deciders: Project owner

## Context

DataBro is a long-term, solo-built SaaS learning platform whose full surface (content, learning,
billing, AI, playground, enterprise) is large but will be discovered incrementally. We need clean
internal boundaries for maintainability and future scale, without paying the operational cost of a
distributed system while the product is still forming.

## Decision

Build a **Modular Monolith**: one deployable backend partitioned into independent modules, each using
**Clean Architecture** (Domain / Application / Infrastructure / Api). Modules own their own schema and
communicate only via application-service contracts and integration events (in-process mediator +
transactional outbox). Boundaries are enforced in CI by architecture-fitness tests.

## Alternatives considered

* **Microservices** — clean boundaries but heavy operational overhead (distributed transactions,
  network failure modes, multi-repo/CD complexity) that a solo developer cannot justify pre-scale.
  Rejected.
* **Layered monolith (no module boundaries)** — simplest short-term, but tends toward a big ball of
  mud; expensive to later separate concerns. Rejected.

## Consequences

* Positive: fast local development, single deploy, strong internal boundaries, and *mechanical*
  extraction to services later (swap in-process mediator for a broker).
* Trade-offs: discipline required to keep modules from coupling; enforced via NetArchTest and the
  no-cross-schema rule.
* Obligates: each module owns its schema; cross-module interaction only via contracts/events; outbox
  for reliable side effects.

## References

[ARCHITECTURE.md](../ARCHITECTURE.md), [MODULES.md](../MODULES.md).
