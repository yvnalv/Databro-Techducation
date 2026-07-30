import { createApiClient, ApiClientError, type ApiClient } from "@databro/api-client";

/**
 * The shared typed API client, configured from runtime config.
 *
 * Uses the platform `fetch` (present in both Node and the browser) rather than Nuxt's `$fetch`,
 * because the client is framework-agnostic by design and `app` will reuse it as-is.
 *
 * The site reads only public content, so no token provider is wired here. When premium gating
 * lands (Phase 3) this is the single place that changes.
 */
export function useApiClient(): ApiClient {
  const config = useRuntimeConfig();
  return createApiClient({ baseUrl: config.public.apiBaseUrl });
}

/**
 * Translates a failure into a Nuxt error so the framework renders the right page and, critically,
 * sends the right status code. An unpublished article must return a real 404 to crawlers, not a
 * 200 with an error message in the body, and not a 503 - a 503 tells a crawler "try again later"
 * and keeps a dead URL in the index (docs/SEO.md).
 *
 * Call this inside the useAsyncData handler, while the original ApiClientError is still intact:
 * useAsyncData re-wraps anything a handler throws, and ApiClientError carries its status on
 * `status`, which the wrapper does not know to read.
 */
export function toNuxtError(error: unknown) {
  if (error instanceof ApiClientError) {
    return createError({
      statusCode: error.status,
      statusMessage: error.code,
      fatal: true,
    });
  }

  // Already a Nuxt/H3 error (e.g. re-thrown after useAsyncData wrapped it): keep its status.
  const statusCode = (error as { statusCode?: unknown } | null | undefined)?.statusCode;
  if (typeof statusCode === "number" && statusCode >= 400) {
    return createError({ statusCode, fatal: true });
  }

  // Network failure or a genuinely unexpected throw: the API is unreachable, not the page missing.
  return createError({ statusCode: 503, statusMessage: "api_unavailable", fatal: true });
}
