<script setup lang="ts">
import { DbThemeToggle } from "@databro/ui";
import type { RailGroup } from "~/components/AppRail.vue";

/**
 * CMS shell, for everything under `/studio` (docs/UI_PATTERNS.md §7, ADR-0015).
 *
 * Same rail and same frame as the learner shell — one app, one navigation model. What differs is
 * what the rail contains, which is the only thing that should differ.
 */
const { t } = useI18n();
const { theme } = useTheme();
const { collapsed } = useRail();
const { user, logout } = useAuth();
const config = useRuntimeConfig();

const publicSiteUrl = computed(() => config.public.siteUrl as string);

// Resolved here in `setup`, NOT inline in the template. `resolveComponent("NuxtLink")` inside a
// template expression does not resolve: Vue falls back to treating the name as a native tag and
// renders a literal `<nuxtlink>` element, which displays its children and is not a link. The same
// is true of `:is="'NuxtLink'"` as a bare string. Both shipped once and produced a rail whose items
// looked perfect and did nothing on click.
const NuxtLink = resolveComponent("NuxtLink");

// Lessons sits beside Courses rather than under it: a lesson body exists independently of any
// curriculum and can belong to several, so nesting it would imply an ownership that is not there.
const groups = computed<RailGroup[]>(() => [
  {
    label: t("chrome.openStudio"),
    items: [
      { label: t("nav.articles"), icon: "articles", to: "/studio" },
      { label: t("nav.courses"), icon: "courses", to: "/studio/courses", prefix: true },
      { label: t("nav.paths"), icon: "paths", to: "/studio/learning-paths", prefix: true },
      { label: t("nav.lessons"), icon: "lessons", to: "/studio/lessons", prefix: true },
      { label: t("nav.quizzes"), icon: "quizzes", to: "/studio/quizzes", prefix: true },
      { label: t("nav.taxonomy"), icon: "taxonomy", to: "/studio/taxonomy", prefix: true },
    ],
  },
  {
    items: [
      // Back to the learner side of the same app. Every editor here is also a learner, and without
      // this the Studio is a room with no door (ADR-0015).
      { label: t("chrome.backToLearning"), icon: "dashboard", to: "/" },
      { label: t("chrome.viewSite"), icon: "site", href: publicSiteUrl.value },
    ],
  },
]);

const initial = computed(() => user.value?.displayName?.trim().charAt(0).toUpperCase() ?? "?");
</script>

<template>
  <div class="min-h-screen bg-surface font-sans text-ink antialiased">
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:m-3 focus:rounded-control focus:bg-accent focus:px-4 focus:py-2 focus:text-accent-on"
    >
      {{ t("chrome.skipToContent") }}
    </a>

    <div class="db-app-shell flex gap-4 py-4 lg:gap-6 lg:py-6">
      <AppRail
        v-model:collapsed="collapsed"
        :groups="groups"
        :labels="{ nav: t('chrome.sectionsLabel'), collapse: t('chrome.collapseNav'), expand: t('chrome.expandNav') }"
        class="sticky top-6 hidden h-[calc(100vh-3rem)] lg:flex"
      />

      <div class="flex min-w-0 max-w-app flex-1 flex-col gap-4 lg:gap-6">
        <header
          class="flex min-h-16 flex-wrap items-center gap-x-3 gap-y-2 rounded-panel border border-line bg-surface-raised px-4 py-3 shadow-card sm:px-5"
        >
          <NuxtLink to="/studio" class="text-accent-strong lg:hidden"><AppBrandMark /></NuxtLink>

          <!-- Below `lg` the rail is hidden, so its destinations move into a scrolling row here. -->
          <nav :aria-label="t('chrome.sectionsLabel')" class="-mx-1 min-w-0 overflow-x-auto lg:hidden">
            <ul class="flex items-center gap-1 px-1">
              <li v-for="item in groups.flatMap((g) => g.items)" :key="item.label">
                <component
                  :is="item.href ? 'a' : NuxtLink"
                  v-bind="item.href ? { href: item.href } : { to: item.to }"
                  class="whitespace-nowrap rounded-control px-3 py-2 text-sm font-medium text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
                >
                  {{ item.label }}
                </component>
              </li>
            </ul>
          </nav>

          <div class="ms-auto flex items-center gap-2 sm:gap-3">
            <DbThemeToggle v-model="theme" :label="t('theme.dark')" />

            <LocaleSwitch />

            <span class="flex items-center gap-2">
              <span
                class="flex h-9 w-9 items-center justify-center rounded-full bg-accent text-xs font-semibold text-accent-on"
                aria-hidden="true"
              >
                {{ initial }}
              </span>
              <span class="hidden text-sm font-medium text-ink lg:block">
                {{ user?.displayName }}
              </span>
            </span>

            <button
              type="button"
              class="whitespace-nowrap rounded-control px-3 py-1.5 text-sm font-medium text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
              @click="logout"
            >
              {{ t("chrome.signOut") }}
            </button>
          </div>
        </header>

        <main id="main" class="min-w-0 flex-1 pb-6">
          <slot />
        </main>
      </div>
    </div>
  </div>
</template>
