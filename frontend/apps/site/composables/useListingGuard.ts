import type { PageMeta } from "@databro/types";

/**
 * 404s a listing page whose `?page=` is past the end.
 *
 * Without this, `?page=999` renders an empty listing with a 200 — a soft 404. Crawlers enumerate
 * page URLs, so an unbounded supply of thin, indexable, near-duplicate pages is exactly what this
 * has to avoid (docs/SEO.md).
 *
 * Page 1 of a genuinely empty listing stays 200: an empty category is a real page, not a missing one.
 */
export function assertPageInRange(meta: PageMeta) {
  if (meta.page > 1 && meta.page > meta.totalPages) {
    throw createError({
      statusCode: 404,
      statusMessage: "page_out_of_range",
      fatal: true,
    });
  }
}
