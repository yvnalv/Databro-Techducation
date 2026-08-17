// DataBro public content site.
// SEO- and cache-critical: content pages are pre-rendered (SSG) with incremental revalidation (ISR)
// per docs/FRONTEND_ARCHITECTURE.md and docs/SEO.md. This is auth-aware, not logged-out-only.

export default defineNuxtConfig({
  compatibilityDate: "2025-01-01",
  ssr: true,

  modules: ["@nuxtjs/tailwindcss", "@pinia/nuxt", "@nuxtjs/i18n"],

  css: [
    // Fonts are self-hosted, not fetched from Google's CDN: a third-party request on every page is
    // both a privacy leak and a render-blocking dependency on the SEO-critical path. Variable fonts
    // so one file covers every weight.
    "@fontsource-variable/inter",
    "@fontsource-variable/plus-jakarta-sans",
    "@fontsource-variable/jetbrains-mono",
    // Semantic design tokens (the CSS custom properties the Tailwind preset maps to) plus the
    // article rhythm. Must load before Tailwind's utilities resolve against them.
    "@databro/ui/tokens.css",
    // KaTeX ships its own stylesheet; without it, math renders as unpositioned glyphs. Loaded here
    // rather than imported by the renderer so it is bundled once, not per block.
    "katex/dist/katex.min.css",
  ],

  // Workspace TS packages are transpiled by Vite.
  build: {
    transpile: ["@databro/ui", "@databro/api-client", "@databro/types"],
  },

  runtimeConfig: {
    // Server-only: how the Nuxt server reaches the API during SSR/prerender. Separate from the
    // public URL because they are genuinely different addresses in a containerised run - inside
    // the site container `localhost` is the site container itself, not the API. Empty means
    // "same as public", which is correct when both run on the host.
    apiInternalBaseUrl: process.env.NUXT_API_INTERNAL_BASE_URL ?? "",

    public: {
      // Browser-visible, so this must be an address the browser can reach.
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL ?? "http://localhost:5158",
      // Absolute origin of the public site. Canonical URLs, OpenGraph and JSON-LD must be
      // absolute (docs/SEO.md) - a relative canonical is worse than none at all.
      siteUrl: process.env.NUXT_PUBLIC_SITE_URL ?? "http://localhost:3000",
    },
  },

  // English default, Bahasa Indonesia secondary (CLAUDE.md rule 19). `prefix_except_default`
  // keeps canonical English URLs clean while giving /id/* its own indexable namespace.
  i18n: {
    strategy: "prefix_except_default",
    defaultLocale: "en",
    locales: [
      { code: "en", language: "en-US", file: "en.json", name: "English" },
      { code: "id", language: "id-ID", file: "id.json", name: "Bahasa Indonesia" },
    ],
    detectBrowserLanguage: {
      // A crawler must get the same HTML for a URL every time, so never redirect on
      // Accept-Language. The cookie only remembers an explicit user choice.
      useCookie: true,
      cookieKey: "databro_locale",
      redirectOn: "no prefix",
      alwaysRedirect: false,
    },
  },

  // Hybrid rendering: static content, revalidated periodically (ISR).
  routeRules: {
    // ISR, **not** `prerender: true`. Prerendering runs at image-build time, when no API is
    // reachable — so the shipped HTML was the "we could not load the articles" fallback with zero
    // article links, served forever because a prerendered page is never re-rendered. It was also
    // wrong on its own terms: a homepage listing the latest articles cannot be frozen at build time
    // or publishing something new would never appear until the next deploy.
    "/": { isr: 600 },
    "/articles/**": { isr: 3600 },
    "/categories/**": { isr: 3600 },
    "/tags/**": { isr: 3600 },
    // Shorter than an article's hour: a curriculum changes while a course is live — lessons get
    // added, reordered, published — and a stale outline misrepresents what someone is signing up to.
    "/courses/**": { isr: 900 },

    // Search is per-query, so no caching — and noindex as a real response header, not only the meta
    // tag the page renders. A header cannot be missed by a crawler that gives up on the HTML.
    "/search": { headers: { "X-Robots-Tag": "noindex, follow" } },
    "/*/search": { headers: { "X-Robots-Tag": "noindex, follow" } },
  },

  app: {
    head: {
      htmlAttrs: { lang: "en" },
      titleTemplate: "%s · DataBro",
      link: [
        // Feed autodiscovery. Without this tag a reader app cannot find the feed from the site URL
        // alone, which is how most people actually subscribe.
        {
          rel: "alternate",
          type: "application/rss+xml",
          title: "DataBro",
          href: "/feed.xml",
        },
      ],
    },
  },
});
