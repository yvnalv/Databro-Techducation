import { ApiClientError } from "@databro/api-client";
import type { Article } from "@databro/types";
import { apiClient } from "../../utils/catalogue";
import { highlightDocument } from "../../utils/highlight";

/**
 * An article plus its server-computed syntax highlighting.
 *
 * The page fetches through here rather than calling the API directly so that highlighting is
 * present on **both** paths into an article: the initial server render and a client-side navigation
 * from a listing. Highlighting inside the page's own `useAsyncData` would only cover the first —
 * the handler re-runs in the browser on navigation, where Shiki deliberately does not exist — and a
 * reader would see highlighted code on reload and plain code when following a link.
 *
 * The API's status codes are passed through unchanged: a missing or unpublished slug must reach the
 * page as a real 404 so it can check for a redirect and then render a genuine 404, never a 200 with
 * empty content and never a 503 (docs/SEO.md §4).
 */
export default defineEventHandler(async (event) => {
  const slug = getRouterParam(event, "slug");

  if (!slug) {
    throw createError({ statusCode: 400, statusMessage: "missing_slug" });
  }

  let article: Article;

  try {
    article = await apiClient(event).getArticle(slug);
  } catch (cause) {
    if (cause instanceof ApiClientError) {
      throw createError({ statusCode: cause.status, statusMessage: cause.code });
    }

    // The API is unreachable, which is not the same as the page being missing.
    throw createError({ statusCode: 503, statusMessage: "api_unavailable" });
  }

  return {
    article,
    // Never fatal: unhighlighted code is a cosmetic loss, and failing the request over it would
    // turn a working article into an error page.
    highlighted: await highlightDocument(article.content).catch(() => ({})),
  };
});
