import { LOCALES, allPublishedArticles, siteUrl, taxonomy, xml } from "../utils/catalogue";

/**
 * sitemap.xml (docs/SEO.md §6).
 *
 * The crawler's entry point to the whole catalogue. Without it, deep articles are only discoverable
 * by following links, and paginated listings are disallowed in robots.txt — so this file is what
 * actually gets everything indexed.
 *
 * Every URL is emitted **once per locale**, each carrying `xhtml:link` alternates for the whole set
 * plus `x-default`. Listing only the English URL would leave the Indonesian pages undiscovered;
 * listing them without alternates would look like duplicate content rather than translations.
 */
interface SitemapEntry {
  /** Locale-agnostic path, e.g. `/articles/rag`. */
  path: string;
  lastmod?: string;
  changefreq: "daily" | "weekly" | "monthly";
  priority: string;
}

export default defineEventHandler(async (event) => {
  const origin = siteUrl(event);

  // A sitemap that 500s tells a crawler nothing; one missing section still gets the rest indexed.
  const [articles, terms] = await Promise.all([
    allPublishedArticles(event).catch(() => []),
    taxonomy(event).catch(() => ({ categories: [], tags: [] })),
  ]);

  const entries: SitemapEntry[] = [
    { path: "/", changefreq: "daily", priority: "1.0" },

    ...articles.map((article) => ({
      path: `/articles/${article.slug}`,
      lastmod: article.publishedAt,
      changefreq: "monthly" as const,
      priority: "0.8",
    })),

    // Only populated categories: a sitemap entry for an empty listing is an invitation to a dead
    // end, and matches the rule the category tiles already follow.
    ...terms.categories
      .filter((category) => category.articleCount > 0)
      .map((category) => ({
        path: `/categories/${category.slug}`,
        changefreq: "weekly" as const,
        priority: "0.6",
      })),

    ...terms.tags.map((tag) => ({
      path: `/tags/${tag.slug}`,
      changefreq: "weekly" as const,
      priority: "0.4",
    })),
  ];

  // The home entry is `/`, and `${origin}${prefix}/` keeps it a real URL rather than a bare origin
  // — `https://databro.id/id/` for the prefixed locale, `https://databro.id/` for the default.
  const url = (path: string, prefix: string) =>
    path === "/" ? `${origin}${prefix}/` : `${origin}${prefix}${path}`;

  const body = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">
${entries
  .flatMap((entry) =>
    LOCALES.map((locale) => {
      const alternates = LOCALES.map(
        (alt) =>
          `    <xhtml:link rel="alternate" hreflang="${alt.code}" href="${xml(url(entry.path, alt.prefix))}"/>`,
      )
        .concat(
          `    <xhtml:link rel="alternate" hreflang="x-default" href="${xml(url(entry.path, ""))}"/>`,
        )
        .join("\n");

      return [
        "  <url>",
        `    <loc>${xml(url(entry.path, locale.prefix))}</loc>`,
        entry.lastmod ? `    <lastmod>${xml(entry.lastmod)}</lastmod>` : null,
        `    <changefreq>${entry.changefreq}</changefreq>`,
        `    <priority>${entry.priority}</priority>`,
        alternates,
        "  </url>",
      ]
        .filter(Boolean)
        .join("\n");
    }),
  )
  .join("\n")}
</urlset>
`;

  setHeader(event, "Content-Type", "application/xml; charset=utf-8");
  setHeader(event, "Cache-Control", "public, max-age=600, s-maxage=3600");
  return body;
});
