<script setup lang="ts">
/**
 * Learner shell — the default chrome of the authenticated app (ADR-0015).
 *
 * A top bar rather than the CMS's sidebar. A learner has two or three destinations and spends their
 * time reading; an editor navigates constantly between a dozen. Giving both the same sidebar would
 * hand the learner a permanent 240px of empty rail.
 */
const { t } = useI18n();
const { user, logout } = useAuth();
const { canAuthor } = useRoles();
const config = useRuntimeConfig();

const publicSiteUrl = computed(() => config.public.siteUrl as string);

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
      {{ t("chrome.skipToContent") }}
    </a>

    <header class="border-b border-line bg-surface">
      <div class="mx-auto flex h-16 max-w-5xl items-center gap-6 px-4 sm:px-6">
        <NuxtLink to="/" class="text-accent"><AppBrandMark /></NuxtLink>

        <nav :aria-label="t('chrome.sectionsLabel')">
          <ul class="flex items-center gap-1">
            <li>
              <NuxtLink
                to="/"
                :aria-current="isActive('/') ? 'page' : undefined"
                class="rounded-md px-3 py-2 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
                :class="
                  isActive('/')
                    ? 'bg-accent-subtle text-accent'
                    : 'text-ink-muted hover:bg-surface-sunken hover:text-ink'
                "
              >
                {{ t("nav.dashboard") }}
              </NuxtLink>
            </li>
            <li>
              <!-- The catalogue is the public site's, not a second copy here: browsing courses is
                   indexable content and belongs to `site` (ADR-0015). -->
              <a
                :href="`${publicSiteUrl}/courses`"
                class="rounded-md px-3 py-2 text-sm font-medium text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
              >
                {{ t("nav.browse") }}
              </a>
            </li>
          </ul>
        </nav>

        <div class="ms-auto flex items-center gap-3">
          <!-- Shown only to someone who can actually author. A UX affordance, never a security
               boundary: the API authorises every request on its own (docs/SECURITY.md §2), and a
               learner who types /studio gets a UI that will not load rather than data. -->
          <NuxtLink
            v-if="canAuthor"
            to="/studio"
            class="hidden rounded-md px-3 py-1.5 text-sm font-medium text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent sm:block"
          >
            {{ t("chrome.openStudio") }}
          </NuxtLink>

          <LocaleSwitch />

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
            {{ t("chrome.signOut") }}
          </button>
        </div>
      </div>
    </header>

    <main id="main" class="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-10">
      <slot />
    </main>
  </div>
</template>
