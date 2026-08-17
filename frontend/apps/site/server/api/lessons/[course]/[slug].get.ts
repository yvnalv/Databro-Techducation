import { ApiClientError } from "@databro/api-client";
import type { LessonPage } from "@databro/types";
import { apiClient } from "../../../utils/catalogue";
import { highlightDocument } from "../../../utils/highlight";

/**
 * A lesson page plus its server-computed syntax highlighting.
 *
 * Mirrors `api/articles/[slug]` and for the same reason: highlighting has to be present on **both**
 * paths into a lesson — the initial server render, and a client-side navigation from the course page
 * or the previous lesson's Next link. Doing it in the page's own `useAsyncData` would cover only the
 * first, because the handler re-runs in the browser where Shiki deliberately does not exist, and a
 * reader would see highlighted code on reload and plain code when clicking Next. On a course this is
 * worse than on an article: moving lesson to lesson is the normal way to read one.
 *
 * Status codes pass through unchanged, so an unpublished course or lesson reaches the page as a real
 * 404 rather than a 200 with nothing in it (docs/SEO.md §4).
 */
export default defineEventHandler(async (event) => {
  const course = getRouterParam(event, "course");
  const slug = getRouterParam(event, "slug");

  if (!course || !slug) {
    throw createError({ statusCode: 400, statusMessage: "missing_slug" });
  }

  let page: LessonPage;

  try {
    page = await apiClient(event).getLessonPage(course, slug);
  } catch (cause) {
    if (cause instanceof ApiClientError) {
      throw createError({ statusCode: cause.status, statusMessage: cause.code });
    }

    throw createError({ statusCode: 503, statusMessage: "api_unavailable" });
  }

  return {
    page,
    // The lesson's blocks arrive as a plain array; `highlightDocument` wants a document, and
    // wrapping is cheaper than a second traversal helper that would then need its own tests.
    //
    // Never fatal: unhighlighted code is a cosmetic loss, and failing over it would turn a working
    // lesson into an error page.
    highlighted: await highlightDocument({
      version: 1,
      blocks: page.lesson.blocks,
    }).catch(() => ({})),
  };
});
