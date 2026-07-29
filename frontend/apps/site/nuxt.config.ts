// DataBro public content site.
// SEO- and cache-critical: content pages are pre-rendered (SSG) with incremental revalidation (ISR)
// per docs/FRONTEND_ARCHITECTURE.md and docs/SEO.md. This is auth-aware, not logged-out-only.

export default defineNuxtConfig({
  compatibilityDate: "2025-01-01",
  ssr: true,

  modules: ["@nuxtjs/tailwindcss", "@pinia/nuxt"],

  // Workspace TS packages are transpiled by Vite.
  build: {
    transpile: ["@databro/ui", "@databro/api-client", "@databro/types"],
  },

  runtimeConfig: {
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL ?? "http://localhost:5158",
    },
  },

  // Hybrid rendering: static content, revalidated periodically (ISR).
  routeRules: {
    "/": { prerender: true },
    "/articles/**": { isr: 3600 },
    "/categories/**": { isr: 3600 },
    "/tags/**": { isr: 3600 },
  },

  app: {
    head: {
      htmlAttrs: { lang: "en" },
      titleTemplate: "%s · DataBro",
    },
  },
});
