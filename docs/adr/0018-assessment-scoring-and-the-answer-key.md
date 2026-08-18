# ADR-0018 — Assessment: separate learner and authoring shapes, and all-or-nothing scoring

Status: Accepted
Date: 2026-08-18
Deciders: DataBro core

## Context

Phase 2's last unbuilt module. A quiz belongs to a lesson, a learner attempts it, and the attempt is
scored. Three questions had to be answered before any of it was worth writing.

**Where does the answer key go?** A quiz is useless without one and worthless if it leaks. The
failure is silent in a way most are not: a quiz that ships its own answers renders correctly, scores
correctly, and passes every test that does not specifically look for it.

**What counts as a correct multiple-choice answer?** Two of three right and one wrong is not
obviously worth zero, and not obviously worth anything else either.

**Does passing gate anything?** Lesson completion already exists in Learning, and coupling them is a
one-line change that would be very hard to walk back.

## Decision

### 1. Learner and authoring DTOs are separate types, not one type with a nullable field

`ChoiceDto(Id, Text)` and `AuthoringChoiceDto(Id, Text, IsCorrect)`. The learner shape **has no field
to leak into**.

A single `ChoiceDto` with `bool? IsCorrect` would be smaller and would put the answer key one
forgotten null-check away from the public path. Types are the enforcement here because a rule that
depends on remembering is a rule that holds until the day someone adds an endpoint in a hurry.

The projection to learner shape lives in exactly one function, so there is one place where a choice
becomes learner-visible.

### 2. The key is released at exactly one moment: submission

An in-progress attempt carries no results. Once submitted, the attempt is closed and cannot change,
so the same data stops being *the answers* and becomes *feedback* — including the author's
explanation, which is written to be read after the fact.

### 3. Scoring is all-or-nothing, including multiple-choice

The selection must be exactly the correct set: every right choice, no wrong ones.

Partial credit sounds kinder and is arbitrary. There is no defensible number for "two of three right,
one wrong", and every scheme that invents one rewards selecting broadly — the learner who ticks
everything scores better than the one who thought about it. A question that genuinely needs partial
credit is a question that should have been split, and the author can split it.

### 4. Scoring happens in the domain, from the stored key

The submission request carries selections only. There is no score field to fake, and a test submits a
body containing `score: 999` alongside wrong answers to prove it scores zero.

### 5. `Quiz` and `QuizAttempt` are separate aggregate roots

The same split as Course/Enrollment and for the same reason: a quiz is authored rarely by one person,
while attempts are written constantly by many learners each touching only their own.

### 6. Passing does **not** gate lesson completion

`QuizAttemptSubmitted` is raised as a plain domain event and is deliberately **not** an integration
event. Whether a quiz must be passed before a lesson counts as complete is a real product decision
nobody has made, and wiring it now would make it by accident. The event carries `LessonId` so
promoting it later needs no cross-module lookup.

## Alternatives considered

* **One DTO with `IsCorrect` omitted at serialisation time** — rejected. Conditional serialisation is
  configuration that lives away from the type, and "why is this field missing sometimes" is a worse
  thing to debug than two explicit shapes.
* **Partial credit proportional to correct selections** — rejected above. Considered seriously,
  because it is what most platforms do; rejected because none of them can explain the number either.
* **Negative marking for wrong selections** — rejected. It punishes uncertainty rather than measuring
  knowledge, and on a learning platform the goal is that people attempt things.
* **A single `Quiz` aggregate owning attempts** — rejected. Every learner would contend on one
  aggregate, and submitting one answer would load an entire question bank.
* **Free-text and code-graded questions** — deferred. They need a grader, and the honest options are
  an LLM (Phase 3) or the Playground (Phase 3). Three types that score unambiguously beats a dozen
  where "close enough" needs a judgement the platform cannot make.

## Consequences

* Positive: no learner-facing shape can carry the answer key, and it is the type system saying so
  rather than a convention.
* Positive: scoring is unambiguous and explainable to a learner in one sentence.
* Negative: all-or-nothing will feel harsh on a five-option multiple-choice question. The remedy is
  authoring guidance, not a scoring formula.
* Negative: only three question types. Anything needing judgement waits for a grader.
* Obligates: a CMS surface — a quiz is currently authored over the API, exactly as learning paths
  were before CHG-0044.
* Obligates: the learner-facing UI, and with it the decision in (6), which will be easier to make
  once someone has taken a quiz on screen.

## References

* [ADR-0013](0013-learning-curriculum-invariants.md) — the aggregate-boundary reasoning this follows.
* [ADR-0017](0017-transactional-outbox.md) — why `QuizAttemptSubmitted` stays internal for now.
* [CHG-0049](../../CHANGELOG.md)
