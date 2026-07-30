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
  <div class="min-h-screen bg-white text-slate-900">
    <!-- Keyboard users land here first; visible only when focused. -->
    <a href="#main" class="sr-only focus:not-sr-only focus:absolute focus:p-4">
      {{ t("nav.skipToContent") }}
    </a>

    <header class="border-b">
      <nav class="mx-auto flex max-w-3xl items-center justify-between gap-4 px-6 py-4">
        <NuxtLink :to="localePath('/')" class="font-semibold">{{ t("site.name") }}</NuxtLink>

        <div class="flex items-center gap-4">
          <NuxtLink :to="localePath('/')">{{ t("nav.articles") }}</NuxtLink>

          <label class="flex items-center gap-2">
            <span class="sr-only">{{ t("nav.languageLabel") }}</span>
            <select
              :value="locale"
              class="border px-2 py-1"
              @change="setLocale(($event.target as HTMLSelectElement).value as typeof locale)"
            >
              <option v-for="l in switchableLocales" :key="l.code" :value="l.code">{{ l.name }}</option>
            </select>
          </label>
        </div>
      </nav>
    </header>

    <main id="main">
      <slot />
    </main>

    <footer class="mt-16 border-t">
      <div class="mx-auto max-w-3xl px-6 py-8 text-sm text-slate-600">
        &copy; {{ new Date().getFullYear() }} {{ t("site.name") }}. {{ t("footer.rights") }}
      </div>
    </footer>
  </div>
</template>
