// DataBro **authenticated** app (ADR-0015). Two audiences, one shell:
//   /         the learner — dashboard, progress, later the playground
//   /studio   the CMS — article and lesson editors, course builder, taxonomy
//
// Not "the learner app" and not "the CMS": the boundary against `site` is indexability, and both of
// these are authenticated, dynamic and never indexed. Lesson *reading* is content and lives on
// `site`. See docs/FRONTEND_ARCHITECTURE.md.

export default defineNuxtConfig({
  compatibilityDate: "2025-01-01",
  ssr: true,

  modules: ["@nuxtjs/tailwindcss", "@pinia/nuxt", "@nuxtjs/i18n"],

  build: {
    transpile: ["@databro/ui", "@databro/api-client", "@databro/types"],
  },

  css: [
    // Same fonts and tokens as the public site: the CMS is a different surface, not a different
    // product, and a second design language would be a second thing to maintain.
    // Poppins is not published as a variable font, so the four weights the system uses are imported
    // individually (ADR-0020 §3). Importing the whole family would ship nine weights to every page.
    "@fontsource/poppins/400.css",
    "@fontsource/poppins/500.css",
    "@fontsource/poppins/600.css",
    "@fontsource/poppins/700.css",
    "@fontsource-variable/jetbrains-mono",
    "@databro/ui/tokens.css",
    // The block editor renders live preview through the same ContentRenderer as the site, so it
    // needs the same maths stylesheet.
    "katex/dist/katex.min.css",
  ],

  runtimeConfig: {
    // Server-only: how the Nuxt server reaches the API during SSR. Inside a container `localhost`
    // is this container, not the API. Empty means "same as public" (both running on the host).
    apiInternalBaseUrl: process.env.NUXT_API_INTERNAL_BASE_URL ?? "",

    public: {
      // Browser-visible, so this must be an address the browser can reach.
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL ?? "http://localhost:5158",
      // Where "view site" points — the public app, which is a separate deployment.
      siteUrl: process.env.NUXT_PUBLIC_SITE_URL ?? "http://localhost:3000",
    },
  },

  // English default, Bahasa Indonesia secondary (CLAUDE.md rule 19). The dependency was here from
  // the start but the module was never registered, so every string was English-only; ADR-0015 wires
  // it up.
  //
  // `no_prefix`, unlike the site's `prefix_except_default`: nothing here is indexed, so there is no
  // /id/* namespace to earn. A locale prefix on an app route would be URL noise that buys a crawler
  // benefit no crawler will ever collect.
  i18n: {
    strategy: "no_prefix",
    defaultLocale: "en",
    locales: [
      { code: "en", language: "en-US", file: "en.json", name: "English" },
      { code: "id", language: "id-ID", file: "id.json", name: "Bahasa Indonesia" },
    ],
    detectBrowserLanguage: {
      useCookie: true,
      // Shared with the site, so a learner who picks Indonesian there does not switch back to
      // English crossing into their dashboard.
      cookieKey: "databro_locale",
      redirectOn: "no prefix",
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
      script: [
        {
          // Stamps `data-theme` from the cookie **before first paint**, so there is no flash of the
          // wrong theme. The same script runs on `site`, where it is load-bearing for a second
          // reason: that app serves ISR-cached HTML, and a theme rendered into the markup would be
          // one visitor's choice served to everyone who hit the cache after them (ADR-0020 §6).
          innerHTML:
            '(function(){try{var m=document.cookie.match(/(?:^|; )databro_theme=(light|dark)/);' +
            'document.documentElement.setAttribute("data-theme",m?m[1]:"light")}catch(e){}})()',
          tagPosition: "head",
        },
      ],
      titleTemplate: "%s · DataBro",
    },
  },
});
