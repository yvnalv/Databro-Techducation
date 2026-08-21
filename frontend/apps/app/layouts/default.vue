<script setup lang="ts">
import { DbThemeToggle } from "@databro/ui";
import type { RailGroup } from "~/components/AppRail.vue";

/**
 * Learner shell — the default chrome of the authenticated app (ADR-0015).
 *
 * Shares the rail and the frame with the CMS shell in `studio.vue`. It used to carry a top bar
 * instead, on the reasoning that a learner has two or three destinations while an editor has a
 * dozen, so a permanent 240px rail would be mostly empty here. A rail that collapses to 68px
 * answers that, and one navigation model across the app is worth more than two kept in agreement.
 *
 * The frame is `.db-app-shell`: full-bleed with a 16/20/24px gutter, so the rail sits against the
 * window edge the way chrome should. The *content* column is capped at `max-w-app` instead. Running
 * the app through the site's `.db-shell` put the rail ~90px in from the edge, which read as a page
 * floating inside the browser rather than an app filling it.
 */
const { t } = useI18n();
const { theme } = useTheme();
// `v-model:collapsed` drives the rail; the composable's own `toggle` is not needed here.
const { collapsed } = useRail();
const { user, logout } = useAuth();
const { canAuthor } = useRoles();
const config = useRuntimeConfig();

const publicSiteUrl = computed(() => config.public.siteUrl as string);

// Resolved here in `setup`, NOT inline in the template. `resolveComponent("NuxtLink")` inside a
// template expression does not resolve: Vue falls back to treating the name as a native tag and
// renders a literal `<nuxtlink>` element, which displays its children and is not a link. The same
// is true of `:is="'NuxtLink'"` as a bare string. Both shipped once and produced a rail whose items
// looked perfect and did nothing on click.
const NuxtLink = resolveComponent("NuxtLink");

const groups = computed<RailGroup[]>(() => [
  {
    items: [
      { label: t("nav.dashboard"), icon: "dashboard", to: "/" },
      // The catalogue is the public site's, not a second copy here: browsing courses is indexable
      // content and belongs to `site` (ADR-0015).
      { label: t("nav.browse"), icon: "browse", href: `${publicSiteUrl.value}/courses` },
    ],
  },
  // Shown only to someone who can actually author. A UX affordance, never a security boundary: the
  // API authorises every request on its own (docs/SECURITY.md §2), and a learner who types /studio
  // gets a UI that will not load rather than data.
  {
    // No group label: the group holds one item and that item is already called "Studio". A heading
    // repeating its only child is noise.
    items: canAuthor.value
      ? [{ label: t("chrome.openStudio"), icon: "articles", to: "/studio", prefix: true }]
      : [],
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
      <!-- Below `lg` the rail would eat the page. The same destinations stay reachable from the
           header's scrolling nav row. -->
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
          <NuxtLink to="/" class="text-accent-strong lg:hidden"><AppBrandMark /></NuxtLink>

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
