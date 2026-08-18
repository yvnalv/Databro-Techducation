# ADR-0016 — Transactional email: SMTP behind a Platform abstraction, provider deferred

Status: Accepted
Date: 2026-08-18
Deciders: DataBro core

## Context

Nothing on the platform could send mail. `IEmailSender` existed but lived in **Identity**, described
exactly one message (`SendEmailConfirmationAsync`), and its only implementation logged a token and
returned. Consequences that had been accumulating:

* Email verification is not enforced (`RequireConfirmedEmail=false`) because it could not be — a user
  who had to confirm would never receive anything to confirm with.
* `CourseCompleted` and `Enrolled` (CHG-0040) have nowhere to go.
* The **transactional outbox** has been top of the next-up list twice and skipped twice, correctly,
  for having no consumer. Email is the obvious first one.

CLAUDE.md rule 14 requires that no business logic hard-couples to a provider. The open question was
not *whether* to abstract but what to put behind it now, given no SaaS account exists.

## Decision

**A provider-agnostic `IEmailSender` in `Platform.Abstractions`, two implementations in a new
`Platform.Email`, selected by configuration.**

* `EmailMessage(To, Subject, HtmlBody, TextBody)` — a to-address, a subject, and both bodies. The
  transport never knows what an email is *about*.
* `LoggingEmailSender` — the default, and development-only.
* `SmtpEmailSender` — SMTP submission, pointed at **Mailpit** locally and a relay in deployed
  environments.
* Templating, localisation and the decision of what to say stay in the module raising the message.
  Identity keeps a port named for meaning (`IIdentityEmails`) over the transport.

**SMTP uses `System.Net.Mail`, with no package.** MailKit is the conventional choice and was tried
first, but every published version — including the newest — carries GHSA-9j88-vvj5-vhgr, so there is
no patched release to move to. Taking an open moderate advisory into the build for a transport this
small is the wrong trade (rule 20). `System.Net.Mail` is enough for submission.

**A SaaS provider (Resend, Postmark, SES) is deliberately not chosen.** SMTP is the lowest common
denominator every one of them speaks, so it is a working default that commits to nothing. The choice
gets made when there is deliverability to care about — a domain, SPF/DKIM, a bounce rate — none of
which exist yet.

**Email verification stays unenforced for now.** The transport unblocks it, but turning it on is a
separate decision with its own migration: every existing account, including the seeded local admin,
would be locked out until confirmed.

## Alternatives considered

* **MailKit** — rejected on the advisory above, not on merit. Revisit if the transport ever needs
  OAuth2 to a mailbox or IMAP, by which point there may be a fix.
* **A SaaS HTTP client now (Resend)** — rejected as premature. It needs an account, a verified domain
  and a key we do not have, and would couple the first implementation to a vendor before the
  abstraction had ever been exercised against a second.
* **Keep `IEmailSender` in Identity and give it more methods** — rejected. Learning will need to send
  a completion email, and it must not depend on Identity to do it. A transport is Platform's by the
  same reasoning as `IClock`.
* **A template engine (Razor, Fluid)** — rejected for two emails. A `.cshtml` that must be copied
  into a container is a deployment step waiting to be forgotten. Revisit at the fifth template.

## Consequences

* Positive: any module can send mail without knowing how it travels, which is what the outbox needs
  before it can carry anything.
* Positive: **email is visible in development.** Mailpit captures everything at
  `http://localhost:8025`, so a verification link is clicked rather than pasted out of a log.
* Negative: `System.Net.Mail.SmtpClient` has no cancellable send, so cancellation is honoured at the
  call boundary rather than mid-flight.
* Negative: emails are localised from configuration, not per user — there is no locale column on a
  user yet. The structure takes a locale; only the source of it is provisional.
* Obligates: choose a deliverability provider before any real deploy; SMTP to a relay is a
  development answer.
* Obligates: enforcing verification, now unblocked, as its own change with an account migration.
* Unblocks: the **transactional outbox**, which now has a genuine first consumer in
  `CourseCompleted` → completion email.

## References

* [ADR-0014](0014-search-across-modules.md) — the precedent for not building infrastructure before a
  consumer exists.
* [CHG-0045](../../CHANGELOG.md)
* [SECURITY.md](../SECURITY.md)
