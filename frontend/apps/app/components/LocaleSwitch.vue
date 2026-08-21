<script setup lang="ts">
/**
 * Language selector, mirroring the site's (CLAUDE.md rule 19).
 *
 * Shared with the site through the `databro_locale` cookie, so a learner who reads the catalogue in
 * Indonesian does not land in an English dashboard one click later.
 */
const { t, locale, locales, setLocale } = useI18n();

const options = computed(() =>
  locales.value.map((l) => (typeof l === "string" ? { code: l, name: l } : { code: l.code, name: l.name ?? l.code })),
);
</script>

<template>
  <label class="flex items-center">
    <span class="sr-only">{{ t("chrome.languageLabel") }}</span>
    <select
      :value="locale"
      class="h-9 rounded-control border border-line-strong bg-surface-raised px-2 text-sm text-ink-muted focus:border-accent-strong focus:outline-none focus:ring-2 focus:ring-accent-strong/25"
      @change="setLocale(($event.target as HTMLSelectElement).value as typeof locale)"
    >
      <option v-for="l in options" :key="l.code" :value="l.code">{{ l.name }}</option>
    </select>
  </label>
</template>
