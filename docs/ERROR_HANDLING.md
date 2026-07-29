# DataBro — Error Handling

Consistent, predictable errors across the API. All non-2xx responses use the failure envelope.

## 1. Failure envelope

```json
{
  "success": false,
  "error": {
    "code": "validation_failed",
    "message": "One or more fields are invalid.",
    "details": [
      { "field": "email", "message": "Email is already registered." }
    ],
    "traceId": "0af7651916cd43dd8448eb211c80319c"
  }
}
```

* `code` — stable, machine-readable, snake_case. Clients branch on this, never on `message`.
* `message` — human-readable, safe to display; localizable on the client.
* `details` — optional array of field-level or item-level errors.
* `traceId` — correlation id for logs (also in the response header).

## 2. HTTP status mapping

| Status | When | `code` examples |
|---|---|---|
| 400 | Malformed request / validation | `validation_failed`, `invalid_request` |
| 401 | Missing/invalid/expired auth | `unauthenticated`, `token_expired` |
| 403 | Authenticated but not allowed | `forbidden`, `insufficient_permission` |
| 404 | Resource not found (or not visible) | `not_found` |
| 409 | Conflict / invariant violation | `slug_taken`, `already_published`, `conflict` |
| 422 | Semantically invalid domain action | `business_rule_violation` |
| 429 | Rate limited | `rate_limited` |
| 500 | Unexpected server error | `internal_error` |
| 503 | Dependency unavailable | `service_unavailable` |

## 3. Principles

* **Never leak internals.** No stack traces, SQL, or provider errors in responses. Log them with a
  `traceId`; return a generic `internal_error`.
* **Not-found over forbidden for hidden resources.** To avoid leaking existence of drafts/premium
  content to unauthorized users, return `404` rather than `403` where disclosure matters.
* **Validation is layered.** Request-shape validation (FluentValidation) → domain invariants (raise
  `business_rule_violation`). Domain rules live in Domain, not controllers.
* **Idempotent actions** return a success envelope even if already in the target state where sensible
  (e.g. publishing an already-published article may return `409 already_published` or a no-op success
  — pick one per endpoint and document it).

## 4. Domain errors

* Domain raises typed exceptions/results (e.g. `SlugAlreadyTakenException`,
  `ArticleNotPublishableException`). A global exception-handling middleware maps them to the envelope +
  status. Controllers stay thin.

## 5. Validation errors

* Collected and returned together in `details` (don't fail on first error) for form-friendly UX.

## 6. Logging & tracing

* Every request has a `traceId` (propagated via `traceparent`); included in logs and error responses.
* 5xx errors are logged at error level with full context; 4xx at information/warning.

## 7. Frontend handling

* `api-client` normalizes all failures to a typed shape and surfaces `code` for branching; UI messages
  are localized from `code` where user-facing.
