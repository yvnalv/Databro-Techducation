// DataBro authenticated learner app (dashboard, progress, CMS authoring, later the playground).
// Dynamic and behind auth — not SEO-optimized. See docs/FRONTEND_ARCHITECTURE.md.

export default defineNuxtConfig({
  compatibilityDate: "2025-01-01",
  ssr: true,

  modules: ["@nuxtjs/tailwindcss", "@pinia/nuxt"],

  build: {
    transpile: ["@databro/ui", "@databro/api-client", "@databro/types"],
  },

  runtimeConfig: {
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL ?? "http://localhost:5158",
    },
  },

  // Authenticated app: nothing here is publicly indexed. `robots: false` was not a real Nuxt
  // route rule (it belongs to a robots module that is not installed), so it silently did
  // nothing. X-Robots-Tag is the actual mechanism and covers non-HTML responses too.
  routeRules: {
    "/**": { headers: { "X-Robots-Tag": "noindex, nofollow" } },
  },

  app: {
    head: {
      htmlAttrs: { lang: "en" },
      titleTemplate: "%s · DataBro",
    },
  },
});
