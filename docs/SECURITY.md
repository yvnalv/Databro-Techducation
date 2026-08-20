# DataBro — Security

Security and privacy are default requirements, not features. This document covers authentication,
authorization, data protection, and abuse prevention.

## 1. Authentication

* **Registration:** public self-service with mandatory **email verification** before privileged
  actions. Passwords hashed with a strong adaptive algorithm (ASP.NET Core Identity default — PBKDF2;
  Argon2id acceptable).
* **Login:** email + password → short-lived **JWT access token** + long-lived **refresh token**.
* **Refresh tokens:** stored hashed, rotated on use, revocable; reuse detection invalidates the chain.
* **Social login:** Google and GitHub via OAuth 2.0 (ADR-0019), through an `IExternalIdentityProvider`
  abstraction rather than a provider SDK. External identities are linked to an account by **verified
  email** (ID-3) and stored in ASP.NET Identity's own user-logins table (the conceptual
  `external_logins`). CSRF on the callback is closed by a **signed, expiring `state`** — no correlation
  cookie, since the host is bearer-only. The callback hands the app a **single-use code** (in
  `IDistributedCache`/Redis) that the app exchanges for tokens: no token ever travels in a URL, which
  keeps this off the discarded implicit-flow path. A provider-verified email confirms the account; an
  unverified one is refused, never linked.
* **Password reset:** single-use, time-limited, hashed tokens.
* Access tokens are signed (asymmetric key preferred) with a short expiry; the signing key is a secret,
  never committed.

## 2. Authorization (RBAC)

* Role-based. Phase 1 roles: **Reader**, **Author**, **Editor**, **Admin**.
* Permissions are explicit and configurable, never hardcoded in business logic. Examples:
  `Content.View`, `Content.Create`, `Content.Edit`, `Content.Publish`, `Content.Delete`,
  `Media.Upload`, `Taxonomy.Manage`, `User.Manage`.
* **Separation of authoring and publishing:** an Author can create/edit drafts (`Content.Create/Edit`)
  but publishing requires `Content.Publish` (Editor/Admin). The creator is not necessarily the publisher.
* Authorization is enforced server-side at the Application layer; the frontend only reflects it.

## 3. Tenancy & data visibility

* B2C-first: no row-level tenant wall. Visibility is enforced by **content status + visibility +
  ownership/role**:
  * Public reads see only `published` + (`public` or premium-preview) content.
  * Drafts/unpublished are visible only to authorized roles (author of record / Editor / Admin).
  * Premium bodies (P3) are gated by the entitlement service; unauthorized users get preview + 404 on
    the gated body, never a leak.

## 4. Transport & data protection

* HTTPS everywhere (TLS termination at Nginx/CDN); HSTS.
* Secrets (DB, JWT key, OAuth secrets, Spaces keys) via environment/secret store — never in source or
  images. See [DEPLOYMENT.md](DEPLOYMENT.md).
* PII minimization: store only what's needed (email, display name, optional profile). Support account
  deletion/export (privacy compliance; expand with legal needs).
* Backups encrypted; least-privilege DB credentials per environment.

## 5. Input handling & content safety

* All input validated server-side (never trust the client).
* Content blocks are sanitized on render: rich-text marks are allowlisted; `embed` blocks only render
  **allowlisted providers**; no arbitrary HTML/iframes/script. Prevents stored XSS via authored content.
### File uploads (implemented — [ADR-0011](adr/0011-media-storage-and-image-processing.md))

File upload is the single most abused endpoint shape on the web, and "an Author is a trusted user"
is not a security model. The defences, and what each one is actually for:

* **Identified by magic bytes.** The `Content-Type` header and the filename extension are both
  supplied by the caller, and the caller uploading something hostile is exactly the one who will lie
  about them. Neither is consulted.
* **Re-encoded, never passed through.** Validating an upload and then storing the original still
  stores the original: a polyglot that is a valid JPEG *and* a valid HTML document passes every
  header check and is then served from our domain. Decoding to pixels and re-encoding cannot carry
  the non-image portion. This is the load-bearing defence — the allowlist alone is not.
* **EXIF, IPTC and XMP stripped.** A phone photo routinely carries GPS coordinates; an author
  dragging one into the editor is not consenting to publish their location.
* **No SVG.** It is XML, it can execute script, and unlike a raster format it cannot be neutralised
  by re-encoding. If vector assets are ever needed they get their own decision and sanitiser.
* **Dimensions read from the header before decoding.** The byte limit does not catch a decompression
  bomb: a 100 MB flat-colour PNG is a few hundred KB compressed and roughly 14 GB decoded. Caps are
  10 MB, 12,000px per side, and 50 megapixels — the last catches shapes the per-side cap misses.
* **Storage keys are generated**, never derived from the client's filename, so a filename of
  `../../../etc/passwd.php` cannot influence where anything lands or what it is called.
* **Served from a separate origin** (object storage / CDN), never from the API's, so a stored file
  can never inherit the API's origin privileges.
* Uploading requires `Media.Upload`; the library listing requires it too — the *files* are public,
  the index of them is not.

## 6. Web app protections

* CSRF: token-based auth (Bearer) avoids cookie-CSRF for the API; if cookies are used for the app,
  apply anti-CSRF + `SameSite`.
* CORS: explicit allowlist of the `site`/`app` origins.
* Security headers: CSP (restrict script/style/img/connect/frame sources), `X-Content-Type-Options`,
  `Referrer-Policy`, `Permissions-Policy`.
* SQL injection: parameterized queries only (EF Core); no string-concatenated SQL.

## 7. Abuse & rate limiting

* Redis-backed rate limits on: registration, login, password reset, email verification resend, and
  search. Progressive backoff / lockout on repeated auth failures.
* Bot mitigation on public forms (registration, newsletter) — CAPTCHA/honeypot as needed.

## 8. Auditing

* Security-relevant events logged: logins (success/failure), role/permission changes, publish/unpublish,
  deletes. Include actor, timestamp, target, and `traceId`. Audit records are append-only.

## 9. Dependencies & supply chain

* Pin dependencies; automated vulnerability scanning (Dependabot / `dotnet list package --vulnerable`,
  `pnpm audit`) in CI.
* Container images built from minimal, patched base images.

## 10. Future (by phase)

* **Billing (P3):** never store raw card data — delegate to the payment provider; verify webhooks by
  signature.
* **AI (P3):** treat prompts/outputs as untrusted; guard against prompt injection in tutor features;
  do not send secrets/PII to LLM providers.
* **Playground (P3):** strong sandbox isolation is a hard requirement (dedicated ADR).
* **Enterprise (P4):** org-scoped access control introduced with the `Organization` aggregate.
