<script setup lang="ts">
// Site chrome. Every string goes through i18n (CLAUDE.md rule 19) - no literals in the template.
// Visual design is deliberately minimal here; the design system is a separate discussion.
const { t, locale, locales, setLocale } = useI18n();
const localePath = useLocalePath();

const switchableLocales = computed(() =>
  locales.value.map((l) => (typeof l === "string" ? { code: l, name: l } : { code: l.code, name: l.name ?? l.code })),
);
</script>

<template>
  <div class="flex min-h-screen flex-col bg-surface font-sans text-ink antialiased">
    <!-- Keyboard users land here first; visible only when focused. -->
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:m-3 focus:rounded-card focus:bg-accent focus:px-4 focus:py-2 focus:text-ink-inverted"
    >
      {{ t("nav.skipToContent") }}
    </a>

    <header class="sticky top-0 z-40 border-b border-line bg-surface/85 backdrop-blur">
      <nav class="mx-auto flex max-w-shell items-center justify-between gap-4 px-6 py-4">
        <NuxtLink :to="localePath('/')" class="text-lg font-semibold tracking-tight">
          {{ t("site.name") }}
        </NuxtLink>

        <div class="flex items-center gap-5">
          <NuxtLink
            :to="localePath('/')"
            class="text-sm font-medium text-ink-muted transition-colors hover:text-ink"
          >
            {{ t("nav.articles") }}
          </NuxtLink>

          <label class="flex items-center gap-2">
            <span class="sr-only">{{ t("nav.languageLabel") }}</span>
            <select
              :value="locale"
              class="rounded-card border border-line bg-surface px-2 py-1 text-sm text-ink-muted"
              @change="setLocale(($event.target as HTMLSelectElement).value as typeof locale)"
            >
              <option v-for="l in switchableLocales" :key="l.code" :value="l.code">{{ l.name }}</option>
            </select>
          </label>
        </div>
      </nav>
    </header>

    <main id="main" class="flex-1">
      <slot />
    </main>

    <footer class="mt-24 border-t border-line">
      <div class="mx-auto max-w-shell px-6 py-10 text-sm text-ink-muted">
        &copy; {{ new Date().getFullYear() }} {{ t("site.name") }}. {{ t("footer.rights") }}
      </div>
    </footer>
  </div>
</template>
