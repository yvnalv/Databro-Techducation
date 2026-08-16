import { allPublishedArticles, siteUrl, xml } from "../utils/catalogue";

/**
 * RSS 2.0 feed (docs/SEO.md §6).
 *
 * English only, and deliberately so: an RSS channel declares one `language`, and mixing locales in
 * one feed gives every subscriber half the items in a language they did not ask for. A per-locale
 * feed is the correct shape when `/id` has enough content to warrant one.
 *
 * Summaries only, never full article bodies. The body is typed blocks, so rendering it to feed HTML
 * would be a second renderer to keep in step with the real one — the exact drift the shared block
 * registry exists to prevent.
 */
const FEED_LIMIT = 25;

export default defineEventHandler(async (event) => {
  const origin = siteUrl(event);
  const articles = (await allPublishedArticles(event).catch(() => []))
    .filter((article) => article.locale === "en")
    .slice(0, FEED_LIMIT);

  const rfc822 = (value?: string) => (value ? new Date(value).toUTCString() : undefined);
  const latest = rfc822(articles[0]?.publishedAt) ?? new Date().toUTCString();

  const items = articles
    .map((article) => {
      const link = `${origin}/articles/${article.slug}`;
      const published = rfc822(article.publishedAt);

      return [
        "    <item>",
        `      <title>${xml(article.title)}</title>`,
        `      <link>${xml(link)}</link>`,
        // Permanently stable identity: the slug cannot change without a 301 (CT-2/CT-3), so a
        // reader's client will never show an old item as new.
        `      <guid isPermaLink="true">${xml(link)}</guid>`,
        `      <description>${xml(article.summary)}</description>`,
        article.author ? `      <dc:creator>${xml(article.author.displayName)}</dc:creator>` : null,
        article.category ? `      <category>${xml(article.category.name)}</category>` : null,
        published ? `      <pubDate>${published}</pubDate>` : null,
        "    </item>",
      ]
        .filter(Boolean)
        .join("\n");
    })
    .join("\n");

  const body = `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:dc="http://purl.org/dc/elements/1.1/">
  <channel>
    <title>DataBro</title>
    <link>${xml(origin)}</link>
    <description>Practical writing on AI, data and software engineering.</description>
    <language>en</language>
    <lastBuildDate>${latest}</lastBuildDate>
    <atom:link href="${xml(`${origin}/feed.xml`)}" rel="self" type="application/rss+xml"/>
${items}
  </channel>
</rss>
`;

  setHeader(event, "Content-Type", "application/rss+xml; charset=utf-8");
  setHeader(event, "Cache-Control", "public, max-age=600, s-maxage=3600");
  return body;
});
