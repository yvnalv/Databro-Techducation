# ADR-0017 — A transactional outbox, one table per module

Status: Accepted
Date: 2026-08-18
Deciders: DataBro core

## Context

`Platform/Messaging` has been a single marker interface since the project began. Aggregates raise
domain events into a list; nothing reads it. Every event raised so far has been dropped on the floor
when the entity left the change tracker.

The outbox reached the top of the next-up list three times and was skipped twice, deliberately, for
having no consumer — the same reasoning [ADR-0014](0014-search-across-modules.md) used to settle
search without one. [ADR-0016](0016-transactional-email-transport.md) supplies one:
`CourseCompleted` → a completion email. That effect has exactly the shape an outbox exists for. It
**must** happen if the completion happened, and it **need not** happen in the same request — a
learner should not wait on an SMTP round trip to watch their progress bar reach 100%, and a mail
server being down must not roll back the fact that they finished a course.

Without an outbox there are only two places to put that send, and both are wrong:

* **Before the commit** — the mail goes out and the transaction rolls back. The learner is told they
  finished something the database says they did not.
* **After the commit** — the process dies in between and nothing, anywhere, records that a message
  was owed.

## Decision

**Write the message as a row in the same `SaveChanges` as the state change, and let a background
worker deliver it. One outbox table per module.**

* `OutboxMessage` in Platform, with `Type`, `Payload`, attempts, `NextAttemptAt`, `Error` and a
  dead-letter flag. Not an `Entity` — audit columns and a soft-delete filter are meaningless on a
  queue row.
* `OutboxInterceptor` collects domain events from tracked aggregates during `SavingChanges`, so the
  rows join the transaction already in flight. No application code publishes anything.
* **Opt-in per event type.** A domain event crosses a module boundary only by also implementing
  `IIntegrationEvent` *and* being registered with a contract name. Two gates, because most domain
  events are internal bookkeeping and publishing all of them would make every internal rename
  someone else's breaking change.
* **Contract names are hand-written, never derived from the CLR type.** A queued row outlives the
  code that wrote it: an assembly-qualified name baked into it makes renaming a class silently
  undeliver every message already queued — a refactor that breaks production days later, in a way no
  compiler catches.
* `OutboxProcessor<TContext>` drains one module's table, dispatching to
  `IIntegrationEventHandler<T>` resolved from DI. A Hangfire sweep runs it minutely, alongside the
  scheduled-publish sweep.
* Failures back off exponentially and **park** after eight attempts. A dead-lettered message is
  never deleted — it is exactly the thing someone needs to read afterwards.

**Delivery is at-least-once, and this is stated in the handler interface rather than in a document.**
The process can die between the effect and the row being marked processed, and there is no ordering
of those two writes that avoids it. Handlers must be idempotent; that is the price of the guarantee,
not an oversight.

## Alternatives considered

* **One shared `platform.outbox_messages` table mapped into every module's context** — rejected. The
  row must be written by the same `DbContext` as the state change or it is not in the same
  transaction, so every module maps it anyway; and two contexts mapping one physical table leaves
  "whose migration creates it" with no good answer. Per-module also keeps rule 10 intact and makes
  extraction mechanical: a module that becomes a service takes its queue with it.
* **A message broker now (RabbitMQ)** — rejected, and it is not even the same thing. A broker moves
  messages between processes; an outbox makes the *decision to send* atomic with the state change.
  A broker without an outbox has the identical dual-write problem. RabbitMQ becomes relevant at
  Phase 4 when there are services to talk between, and it will sit behind this, not replace it.
* **Publishing in-process from the application service after commit** — rejected as the bug this
  exists to prevent.
* **Deriving the stored type name from the CLR type** — rejected above. It is the cheap option that
  fails only under refactoring, which is to say it fails eventually and never in a test.
* **Deleting processed rows** — deferred, not rejected. Keeping them makes the table an audit of what
  the system decided to do, which is worth having while volume is negligible. A retention sweep is
  owed before it is not.

## Consequences

* Positive: finishing a course sends mail that survives the API being killed mid-transaction, and a
  handler that throws is retried rather than lost.
* Positive: domain events finally mean something. `Enrolled`, `ArticlePublished` and the rest can be
  promoted one at a time, each a deliberate act.
* Negative: **handlers must be idempotent**, and nothing enforces it. A second congratulation email
  is a nuisance; a second certificate would not be, so certificates will need a real dedupe key.
* Negative: delivery is up to a minute late. Acceptable for email; a future effect that needs to be
  prompt gets an immediate nudge after commit with the sweep as the safety net, not a shorter cron.
* Negative: processed rows accumulate. Retention is owed.
* Obligates: a retention sweep, and an operational view of dead-lettered messages — a parked row is
  currently only visible in the database.

## References

* [ADR-0014](0014-search-across-modules.md) — the precedent for not building this before a consumer
  existed.
* [ADR-0016](0016-transactional-email-transport.md) — the consumer.
* [CHG-0046](../../CHANGELOG.md)
