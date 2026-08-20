# ADR-0019 — Social login: a manual OAuth handler, signed state, and a code-exchange handoff

Status: Accepted
Date: 2026-08-20
Deciders: DataBro core

## Context

Social login (Google and GitHub) is Phase 1 scope that was never built. The endpoints have sat under
an explicit "Not built" heading in [API_SPEC.md](../API_SPEC.md) rather than pretending to exist, and
[LOCAL_DEVELOPMENT.md](../LOCAL_DEVELOPMENT.md#social-login-setup-google-and-github) already documents
the OAuth-app registration the owner must do. What was undecided was *how* the flow is built, and four
things had to be answered before any of it was worth writing.

**Where does the OAuth handshake live?** ASP.NET Core ships `AddGoogle` / `AddOAuth` remote handlers,
but they assume a cookie-based sign-in scheme: the handler correlates the round-trip with a temporary
external cookie and calls `SignInAsync`. This host has none — it is **bearer-only** (`AddIdentityCore`
+ JWT, no `SignInManager`, no cookie middleware). Adopting the built-in handlers means adding cookie
authentication to a host that has deliberately never had it.

**How does the API hand tokens to the app?** The app (`apps/app`, `:3001`) stores its access and
refresh tokens in JS-readable cookies and lives on a **different origin** from the API (`:5158`). An
OAuth callback is a top-level browser navigation that lands on the API — which therefore cannot set
the app's cookies. The tokens have to cross an origin boundary somehow.

**What stops the callback being forged?** Without the built-in handler's correlation cookie, CSRF on
the callback is our problem to solve.

**When two identities claim the same person, what links them?** [ID-3](../BUSINESS_RULES.md) says a
user may attach multiple external logins to one account, matched by verified email. The failure mode
is a silent duplicate account.

## Decision

### 1. A thin, manual OAuth 2.0 handler behind `IExternalIdentityProvider`

`GoogleProvider` and `GitHubProvider` each build an authorization URL, exchange the returned code for
an access token, and fetch a `(email, emailVerified, displayName)` triple. Nothing else about the
provider leaks upward; `AuthService` sees an abstraction, not an HTTP client. This mirrors rule 14's
spirit — the same reason `ILlmProvider` and `IEmailSender` exist — even though the rule names LLM,
payment and email vendors specifically.

Rejecting the built-in remote handlers is the load-bearing call here. They are the conventional choice
and would be less code, but only after adding a cookie sign-in scheme whose sole job is to survive one
redirect. A bearer-only host that grows a cookie scheme for one feature has acquired a second
authentication model that every future reader has to account for. The manual handshake is perhaps
sixty lines per provider against exactly the two endpoints the doc already specifies, and it leaves
the host's auth story single.

### 2. Google needs one call; GitHub needs two

Google's userinfo response carries `email` and `email_verified`, so one call settles identity. GitHub's
`/user` returns `null` for email whenever the user keeps it private — common — so `GitHubProvider`
requests `read:user user:email` and calls `/user/emails` to find the **primary, verified** address. A
GitHub account that exposes no verified email is **refused**, not linked: without the second call a
private-email sign-in would silently create a duplicate account instead of linking to the existing one,
which is the one outcome ID-3 exists to prevent.

### 3. Provider-verified email is the identity; it confirms the account

The email the provider vouches for is trusted as confirmed — that is the entire point of delegating
authentication. So a social sign-in that creates a new user sets `EmailConfirmed = true` and assigns
`Reader`, and a social sign-in whose email matches an existing **unconfirmed** account confirms it in
passing. An unverified provider email is never trusted: Google `email_verified: false` and a GitHub
address that is not a verified primary are both refused rather than linked, because linking on an
unverified address is an account-takeover primitive.

### 4. Linking is by verified email, recorded through Identity's login store

On callback, `LinkOrCreateAsync` looks up the user by verified email. Found → `AddLoginAsync` attaches
the `(provider, providerKey)` pair and tokens are issued. Not found → a confirmed user is created, then
linked. The link lives in ASP.NET Identity's own user-logins table, which `AddEntityFrameworkStores`
already provides — [SECURITY.md](../SECURITY.md) §1 calls this `external_logins` conceptually; the
physical table is Identity's, in the `identity` schema, and is not worth reimplementing to rename.

### 5. State is a signed, expiring token — no server session

The `state` parameter is an HMAC-signed payload carrying a nonce, an issued-at timestamp, and the
post-login redirect target, signed with the JWT key already in configuration. The callback verifies
the signature and a short expiry before doing anything. This replaces the built-in handler's
correlation cookie with something a bearer-only host can carry: nothing to store, nothing to clean up,
and a forged or replayed callback fails signature or freshness. The redirect target travels **inside**
the signed state rather than as an open query parameter, so it cannot be tampered into an open
redirect, and it is validated against the same site-origin allowlist the password login already uses.

### 6. The token handoff is an application-level authorization code, never a URL token

The callback does **not** put tokens in the redirect URL. It mints a short-lived, single-use code,
stores the freshly issued token pair against it, and redirects the app to a receiver route with only
that `?code`. The app POSTs the code back once to exchange it for the tokens, then stores the session
exactly as password login does.

Putting tokens in the URL — query or fragment — is the discarded OAuth *implicit flow*. A URL leaks
into browser history, `Referer` headers, and any log or proxy on the path (RFC 6749 §10.3, RFC 6819,
the OAuth 2.0 Security BCP). A fragment is not sent to servers but still lands in history and is
readable by any script on the receiver page. The code-exchange pattern is OAuth's own authorization
code flow applied to our own boundary: the only thing in the URL is a value that is useless after one
use and expires in seconds.

The code store is `IDistributedCache`, **backed by Redis** in dev and production — which finally gives
the provisioned-but-unused Redis a job ([O-4](../OPEN_ITEMS.md)) — and by an in-memory distributed
cache in tests, so the exchange is exercised without a Redis container. The abstraction is the point:
the store holds a secret for seconds and a swap of backing is a one-line registration.

## Alternatives considered

* **ASP.NET's `AddGoogle` / `AddOAuth` with a cookie sign-in scheme** — rejected per (1). Less code, but
  it introduces a second authentication model to a host that has one on purpose, for the lifetime of
  the feature, not just the handshake.
* **Tokens in the redirect fragment** — rejected per (6). It is the lighter build and needs no store,
  but it is the implicit-flow smell: tokens in history, readable by any script that runs on the
  receiver route. The one-time code costs a Redis round-trip and a POST to avoid all of it.
* **A correlation/state cookie instead of signed state** — rejected. It is what the built-in handler
  does, and it drags in the cookie scheme (1) rejects. A signed token needs no storage and no
  same-site dance across the provider redirect.
* **A bespoke `external_logins` table** — rejected. Identity's user-logins store already models exactly
  `(provider, key, user)` and is wired by `AddEntityFrameworkStores`. Renaming it for cosmetic
  agreement with the doc buys nothing and forfeits the maintained store.
* **Creating the user unconfirmed and emailing a verification link** — rejected. The provider has
  already verified the address; a second confirmation would be theatre, and worse, it would block a
  legitimate first sign-in behind an email round-trip for no security gain.

## Consequences

* Positive: the host stays bearer-only. Social login adds two endpoints and a provider abstraction, not
  a second auth model.
* Positive: no token ever appears in a URL, in history, or in a log.
* Positive: Redis is finally load-bearing (O-4), behind an abstraction that does not care it is Redis.
* Positive: ID-3 holds by construction — a returning social user with a matching verified email links
  rather than duplicates, and an unverified email is refused rather than trusted.
* Negative: the manual handshake owns code the framework would otherwise own — token exchange, error
  mapping, the GitHub second call. It is small and covered by tests against a faked provider, but it is
  ours to maintain.
* Negative: an unverified GitHub email is a dead end at sign-in with a message rather than a silent
  account. That is the correct trade, but it is a support question waiting to be asked.
* Obligates: the four OAuth secrets exist only in `.env` (owner-registered, M-3); `.env.example` gains
  four empty, labelled slots and nothing more.
* Obligates: a production deploy needs a **second GitHub OAuth app** (one callback URL per app) and a
  Redis instance the API can reach — both noted in LOCAL_DEVELOPMENT and DEPLOYMENT.

## References

* [ADR-0015](0015-authenticated-app-hosts-both-audiences.md) — why the app that receives the handoff
  serves both audiences, and the redirect-allowlist reasoning reused for the state's return target.
* [ADR-0016](0016-transactional-email-transport.md) — the provider-behind-an-abstraction precedent.
* [BUSINESS_RULES.md](../BUSINESS_RULES.md) — ID-3.
* [SECURITY.md](../SECURITY.md) §1.
* [LOCAL_DEVELOPMENT.md](../LOCAL_DEVELOPMENT.md#social-login-setup-google-and-github) — OAuth-app
  registration (M-3).
* [CHANGELOG.md](../../CHANGELOG.md) — CHG-0061.
</content>
</invoke>
