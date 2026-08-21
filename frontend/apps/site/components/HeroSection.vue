<script setup lang="ts">
import { DbButton } from "@databro/ui";

/**
 * Home hero (docs/UI_PATTERNS.md §4).
 *
 * The reference pairs the copy with an image collage and a floating stat card. Neither is here:
 * there is no photography, and a stat card would have to invent numbers. A confident type-led hero
 * is better than a real one padded with placeholders — and it keeps the LCP element text, which is
 * the fastest thing a hero can be.
 */
const { t } = useI18n();
const localePath = useLocalePath();

// Resolved rather than imported: `@databro/ui` stays framework-agnostic, so the consumer
// supplies the link component (FRONTEND_ARCHITECTURE / DbButton).
const NuxtLink = resolveComponent("NuxtLink");

const highlights = computed(() => [
  t("home.highlightOne"),
  t("home.highlightTwo"),
  t("home.highlightThree"),
]);
</script>

<template>
  <section class="border-b border-line bg-surface-sunken">
    <div class="db-shell py-16 sm:py-24">
      <div class="max-w-3xl">
        <p
          class="inline-flex items-center gap-2 rounded-full bg-accent-subtle px-3 py-1 text-xs font-semibold uppercase tracking-wide text-accent-strong"
        >
          {{ t("site.name") }}
        </p>

        <h1
          class="mt-5 font-display text-4xl font-extrabold leading-[1.1] tracking-tight text-ink sm:text-5xl lg:text-6xl"
        >
          {{ t("home.heroTitle") }}
        </h1>

        <p class="mt-6 max-w-2xl text-lg leading-relaxed text-ink-muted sm:text-xl">
          {{ t("home.heroSubtitle") }}
        </p>

        <ul class="mt-8 flex flex-wrap gap-x-6 gap-y-3">
          <li
            v-for="highlight in highlights"
            :key="highlight"
            class="flex items-center gap-2 text-sm font-medium text-ink-muted"
          >
            <svg class="h-4 w-4 shrink-0 text-accent-strong" viewBox="0 0 20 20" fill="none" aria-hidden="true">
              <circle cx="10" cy="10" r="9" fill="currentColor" opacity="0.14" />
              <path
                d="m6 10.5 2.5 2.5L14 7.5"
                stroke="currentColor"
                stroke-width="1.75"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
            {{ highlight }}
          </li>
        </ul>

        <div class="mt-10 flex flex-wrap gap-3">
          <!-- Through DbButton rather than a hand-rolled copy of it: this markup duplicated the
               `lg` variant exactly, so a change to button padding or focus ring would have moved
               every button on the platform except this one. -->
          <DbButton :as="NuxtLink" :to="localePath('/')" size="lg">
            {{ t("home.primaryCta") }}
          </DbButton>
        </div>
      </div>
    </div>
  </section>
</template>
