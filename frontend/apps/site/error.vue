<script setup lang="ts">
import type { NuxtError } from "#app";

/**
 * Error page (docs/UI_PATTERNS.md §6).
 *
 * Reference composition: an oversized ghosted status numeral with the message overlaid, a muted
 * explanation, and a primary action back to safety. The reference keeps its newsletter band here;
 * DataBro drops it — asking for an email address on a broken page is the wrong moment.
 *
 * Nuxt renders error.vue outside the layout, so the chrome is repeated minimally.
 */
const props = defineProps<{ error: NuxtError }>();

const { t } = useI18n();
const localePath = useLocalePath();

const isNotFound = computed(() => props.error?.statusCode === 404);
const title = computed(() => (isNotFound.value ? t("error.notFoundTitle") : t("error.genericTitle")));
const message = computed(() =>
  isNotFound.value ? t("error.notFoundMessage") : t("error.genericMessage"),
);

// Error pages must never be indexed, whatever status they carry.
useSeoMeta({ title: title.value, robots: "noindex,nofollow" });
</script>

<template>
  <div class="flex min-h-screen flex-col bg-surface font-sans text-ink antialiased">
    <header class="border-b border-line">
      <div class="db-shell flex h-16 items-center">
        <NuxtLink :to="localePath('/')" class="text-accent-strong" :aria-label="t('site.name')">
          <BrandMark class="text-accent-strong [&>span:last-child]:text-ink" />
        </NuxtLink>
      </div>
    </header>

    <main class="flex flex-1 items-center">
      <div class="db-shell py-20 text-center">
        <div class="relative">
          <!-- Decorative ghost numeral; the heading overlaid on it carries the meaning. -->
          <p
            class="select-none font-display text-[8rem] font-extrabold leading-none text-line sm:text-[12rem]"
            aria-hidden="true"
          >
            {{ error?.statusCode ?? 500 }}
          </p>

          <h1
            class="absolute inset-0 flex items-center justify-center px-4 font-display text-3xl font-bold tracking-tight text-ink sm:text-4xl"
          >
            {{ title }}
          </h1>
        </div>

        <p class="mx-auto mt-6 max-w-xl text-lg text-ink-muted">{{ message }}</p>

        <p class="mt-8">
          <NuxtLink
            :to="localePath('/')"
            class="inline-flex h-12 items-center justify-center rounded-control bg-accent px-6 text-base font-medium text-accent-on transition-colors hover:bg-accent-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong focus-visible:ring-offset-2"
            @click="clearError({ redirect: '/' })"
          >
            {{ t("error.backHome") }}
          </NuxtLink>
        </p>
      </div>
    </main>
  </div>
</template>
