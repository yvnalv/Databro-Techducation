import type { Article } from "@databro/types";

/**
 * Emits the full SEO surface for an article (docs/SEO.md, CLAUDE.md rule 15): canonical URL,
 * meta title/description, OpenGraph + Twitter cards, and JSON-LD `Article` structured data.
 *
 * Rule 9: a Premium article still publishes complete metadata and a preview. Gating hides the
 * body, never the metadata - the page must stay indexable and shareable.
 */
export function useArticleSeo(article: Article) {
  const config = useRuntimeConfig();
  const { locale, locales } = useI18n();
  const localePath = useLocalePath();

  const origin = config.public.siteUrl.replace(/\/$/, "");
  const absolute = (path: string) => `${origin}${path}`;

  // The article's own canonical wins if the author set one (e.g. a syndicated original);
  // otherwise the site's own URL for this locale is canonical. Slugs are immutable once
  // published (rule 16), so this URL is stable for the life of the article.
  const canonical = article.seo.canonicalUrl?.trim()
    ? article.seo.canonicalUrl
    : absolute(localePath(`/articles/${article.slug}`));

  const title = article.seo.metaTitle?.trim() || article.title;
  const description = article.seo.metaDescription?.trim() || article.summary;

  // Premium bodies are gated, but the metadata is not - so this stays the real description.
  const isPremium = article.visibility === "premium";

  useHead({
    htmlAttrs: { lang: locale.value },
    link: [
      { rel: "canonical", href: canonical },
      // hreflang alternates so the two locales are understood as translations, not duplicates.
      // Paths come from localePath(..., code) rather than being built by hand, so the URL
      // strategy stays defined in exactly one place (nuxt.config i18n).
      ...locales.value.map((l) => {
        const code = typeof l === "string" ? l : l.code;
        return {
          rel: "alternate" as const,
          hreflang: code,
          href: absolute(localePath(`/articles/${article.slug}`, code)),
          type: "text/html",
        };
      }),
    ],
  });

  useSeoMeta({
    title,
    description,
    // `robots` comes from the author-set SEO metadata and defaults to index,follow server-side.
    robots: article.seo.robots || "index,follow",

    ogType: "article",
    ogTitle: title,
    ogDescription: description,
    ogUrl: canonical,
    ogSiteName: "DataBro",
    ogLocale: locale.value,

    articlePublishedTime: article.publishedAt,
    articleAuthor: article.author ? [article.author.displayName] : undefined,

    twitterCard: "summary_large_image",
    twitterTitle: title,
    twitterDescription: description,
  });

  // JSON-LD. `isAccessibleForFree` + `hasPart` is the Google-documented way to declare paywalled
  // content: it keeps a Premium article indexable instead of looking like cloaking.
  useHead({
    script: [
      {
        type: "application/ld+json",
        innerHTML: JSON.stringify({
          "@context": "https://schema.org",
          "@type": "Article",
          headline: title,
          description,
          url: canonical,
          mainEntityOfPage: { "@type": "WebPage", "@id": canonical },
          inLanguage: article.locale,
          datePublished: article.publishedAt,
          author: article.author
            ? { "@type": "Person", name: article.author.displayName }
            : { "@type": "Organization", name: "DataBro" },
          publisher: { "@type": "Organization", name: "DataBro" },
          isAccessibleForFree: !isPremium,
          ...(isPremium
            ? {
                hasPart: {
                  "@type": "WebPageElement",
                  isAccessibleForFree: false,
                  cssSelector: ".databro-premium-body",
                },
              }
            : {}),
        }),
      },
    ],
  });
}
