import type { PageMeta } from "@databro/types";

/**
 * SEO for a paginated listing page (homepage, category, tag).
 *
 * Each page is **self-canonical** — page 2 canonicalises to itself, not to page 1. Canonicalising
 * every page to the first would tell a crawler the deeper pages are duplicates, and the articles
 * only listed there would lose their discovery path.
 *
 * `rel=prev/next` is emitted as a courtesy for engines that still read it; Google dropped it as an
 * indexing signal in 2019, so the load-bearing part is the crawlable `<a>` links rendered by
 * PaginationNav.
 */
export function useListingSeo(options: {
  title: string;
  description: string;
  /** Locale-agnostic path, e.g. `/categories/machine-learning`. */
  path: string;
  meta: PageMeta;
}) {
  const config = useRuntimeConfig();
  const { locale, locales } = useI18n();
  const localePath = useLocalePath();

  const origin = config.public.siteUrl.replace(/\/$/, "");
  const pageUrl = (page: number) =>
    `${origin}${localePath(options.path)}${page > 1 ? `?page=${page}` : ""}`;

  const { page, totalPages } = options.meta;

  // Page 2+ of a listing carries no unique content of its own, so the title is disambiguated to
  // avoid duplicate-title warnings while staying human-readable.
  const title = page > 1 ? `${options.title} — ${page}` : options.title;

  useHead({
    htmlAttrs: { lang: locale.value },
    link: [
      { rel: "canonical", href: pageUrl(page) },
      ...(page > 1 ? [{ rel: "prev" as const, href: pageUrl(page - 1) }] : []),
      ...(page < totalPages ? [{ rel: "next" as const, href: pageUrl(page + 1) }] : []),
      ...locales.value.map((l) => {
        const code = typeof l === "string" ? l : l.code;
        return {
          rel: "alternate" as const,
          hreflang: code,
          href: `${origin}${localePath(options.path, code)}${page > 1 ? `?page=${page}` : ""}`,
          type: "text/html",
        };
      }),
    ],
  });

  useSeoMeta({
    title,
    description: options.description,
    ogTitle: title,
    ogDescription: options.description,
    ogType: "website",
    ogUrl: pageUrl(page),
    ogSiteName: "DataBro",
    ogLocale: locale.value,
  });
}
