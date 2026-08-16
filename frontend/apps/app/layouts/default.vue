<script setup lang="ts">
/**
 * CMS shell (docs/UI_PATTERNS.md §7).
 *
 * Sidebar plus main, following the reference's dashboard — but without its gradient band and the
 * profile card overlapping it. The CMS is a tool: that flourish costs vertical space on the one
 * surface where density actually matters.
 */
const { user, logout } = useAuth();
const config = useRuntimeConfig();

const publicSiteUrl = computed(() => config.public.siteUrl as string);

const navigation = [
  { label: "Articles", to: "/", icon: "articles" },
  { label: "Taxonomy", to: "/taxonomy", icon: "taxonomy" },
];

const route = useRoute();
const isActive = (to: string) => (to === "/" ? route.path === "/" : route.path.startsWith(to));

const initial = computed(() => user.value?.displayName?.trim().charAt(0).toUpperCase() ?? "?");
</script>

<template>
  <div class="min-h-screen bg-surface-sunken font-sans text-ink antialiased">
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:m-3 focus:rounded-md focus:bg-accent focus:px-4 focus:py-2 focus:text-ink-inverted"
    >
      Skip to content
    </a>

    <div class="flex min-h-screen">
      <!-- Sidebar. Fixed width so the main column's measure does not shift between pages. -->
      <aside class="hidden w-60 shrink-0 border-r border-line bg-surface lg:block">
        <div class="flex h-16 items-center border-b border-line px-5">
          <NuxtLink to="/" class="text-accent"><AppBrandMark /></NuxtLink>
        </div>

        <nav aria-label="Sections" class="p-3">
          <ul class="space-y-1">
            <li v-for="item in navigation" :key="item.to">
              <NuxtLink
                :to="item.to"
                :aria-current="isActive(item.to) ? 'page' : undefined"
                class="flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
                :class="
                  isActive(item.to)
                    ? 'bg-accent-subtle text-accent'
                    : 'text-ink-muted hover:bg-surface-sunken hover:text-ink'
                "
              >
                {{ item.label }}
              </NuxtLink>
            </li>
          </ul>
        </nav>
      </aside>

      <div class="flex min-w-0 flex-1 flex-col">
        <header class="flex h-16 items-center justify-between gap-4 border-b border-line bg-surface px-4 sm:px-6">
          <!-- Brand repeats here only where the sidebar is hidden. -->
          <NuxtLink to="/" class="text-accent lg:hidden"><AppBrandMark /></NuxtLink>

          <div class="ms-auto flex items-center gap-3">
            <a
              :href="publicSiteUrl"
              target="_blank"
              rel="noopener"
              class="hidden text-sm font-medium text-ink-muted transition-colors hover:text-ink sm:block"
            >
              View site ↗
            </a>

            <span class="flex items-center gap-2">
              <span
                class="flex h-8 w-8 items-center justify-center rounded-full bg-accent-subtle text-xs font-semibold text-accent"
                aria-hidden="true"
              >
                {{ initial }}
              </span>
              <span class="hidden text-sm font-medium text-ink sm:block">
                {{ user?.displayName }}
              </span>
            </span>

            <button
              type="button"
              class="rounded-md px-3 py-1.5 text-sm font-medium text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
              @click="logout"
            >
              Sign out
            </button>
          </div>
        </header>

        <main id="main" class="flex-1 p-4 sm:p-6 lg:p-8">
          <slot />
        </main>
      </div>
    </div>
  </div>
</template>
