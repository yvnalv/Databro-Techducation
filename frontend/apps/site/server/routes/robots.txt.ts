import { siteUrl } from "../utils/catalogue";

/**
 * robots.txt (docs/SEO.md §6).
 *
 * Served from the *site* origin, not the API: a crawler fetches `https://databro.id/robots.txt`, so
 * it has to come from the host that owns that origin.
 *
 * Deliberately permissive — this is a content site whose whole strategy is being indexed. The
 * disallows cover surfaces that are either not content or actively harmful to index: the
 * authenticated app is a separate origin and carries `X-Robots-Tag: noindex` of its own.
 */
export default defineEventHandler((event) => {
  const origin = siteUrl(event);

  const body = [
    "User-agent: *",
    "Allow: /",
    "",
    "# Paginated listings past the first page are thin and near-duplicate; the articles they",
    "# contain are all reachable from the sitemap, so there is nothing lost by not crawling them.",
    "Disallow: /*?page=",
    "",
    "# Internal search results: thin, near-duplicate, and infinitely generatable. The page also",
    "# sends `noindex, follow`, which this disallow deliberately does not replace — a crawler that",
    "# obeys the disallow never fetches the page and so never sees the meta tag.",
    "Disallow: /search",
    "Disallow: /*/search",
    "",
    `Sitemap: ${origin}/sitemap.xml`,
    "",
  ].join("\n");

  setHeader(event, "Content-Type", "text/plain; charset=utf-8");
  // Rarely changes; a day of edge caching costs nothing and saves the origin a request per crawl.
  setHeader(event, "Cache-Control", "public, max-age=3600, s-maxage=86400");
  return body;
});
