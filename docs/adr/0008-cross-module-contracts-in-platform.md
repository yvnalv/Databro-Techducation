# ADR-0008 — Cross-module read contracts live in Platform

Status: Accepted
Date: 2026-07-30
Deciders: Project owner

## Context

Rendering an article requires a byline. Content owns only `AuthorId`; the display name lives on
Identity's `ApplicationUser`. This is the first case where one module needs data another module owns.

The constraints are already fixed:

* Modules must not read another module's tables (CLAUDE.md rule 10), and must stay extractable
  (rule 11).
* The architecture-fitness test `Application_should_not_depend_on_other_modules` fails the build on
  any `DataBro.Modules.X.*` → `DataBro.Modules.Y.*` reference, so `Content.Application` cannot
  reference `Identity.Application` even to consume an interface.
* The public read path is the SEO-critical, read-heavy, cached surface. Whatever we choose here is
  copied by every later module (Learning, Community, Assessment all need author/actor names).

## Decision

**Cross-module read contracts are declared as interfaces in the shared `Platform` kernel, and
implemented by the owning module's Infrastructure layer.** Consumers depend on the abstraction only
and never learn which module satisfies it.

The first instance is `DataBro.Platform.Abstractions.IUserDirectory`, returning a minimal
`UserSummary(Id, DisplayName, AvatarUrl)`. Identity registers `UserDirectory` against it;
`Content.Application` injects the interface.

Two shape rules, both load-bearing:

* **Batch, not per-item.** `GetUsersAsync(IReadOnlyCollection<Guid>)` — a per-item interface would
  invite N+1 lookups on list endpoints.
* **Partial results are legal.** Unresolvable ids are absent from the returned map, and the DTO's
  author is nullable. A deleted account must not break an article page.

## Alternatives considered

* **Denormalize an author snapshot into Content**, fed by an Identity integration event. Best
  long-term for a read-heavy cached path and the most extraction-friendly, but it needs the
  transactional outbox, which does not exist yet. Deferred, not rejected — see Consequences.
* **Compose the DTO in the API host**, which already references every module. Passes the fitness
  gate, but scatters mapping logic into the composition root and leaves `ArticleService` returning
  half-built DTOs.
* **Let the site call `/api/v1/users/{id}` separately.** Rejected: an extra round trip on the
  SSR/prerender critical path, and it pushes joining onto every client.
* **Relax the architecture test to allow Application→Application references.** Rejected: the gate is
  the only thing preventing gradual re-coupling, and weakening it for convenience defeats its
  purpose.

## Consequences

* Positive: one sanctioned pattern for every future cross-module read; the fitness gate stays strict;
  consumers are trivially testable against a fake.
* Trade-offs: `Platform` accumulates contract interfaces, so it must stay a *contracts* kernel — no
  implementations, no module-specific types leaking into `UserSummary`.
* Trade-offs: a synchronous in-process call per request. Acceptable while the read path is cached and
  both modules share a process; it becomes a network hop if Identity is ever extracted, which is
  precisely when the denormalization alternative should be revisited.
* Obligates: when the outbox lands, re-evaluate moving the byline to a denormalized snapshot fed by
  `UserProfileChanged`. That change is behind this interface and touches no consumer.
* Obligates: `AvatarUrl` stays null until Media exists — resolving a `MediaId` to a URL is Media's
  job, not Identity's.

## References

[ARCHITECTURE.md](../ARCHITECTURE.md); [MODULES.md](../MODULES.md); ADR-0001; CLAUDE.md rules 10–11.
