<script setup lang="ts">
/**
 * Site header (docs/UI_PATTERNS.md §1.1).
 *
 * Three zones, as in the reference: brand left, navigation centre, actions right. Sticky with a
 * translucent backdrop so content scrolling beneath stays legible.
 */
const { t, locale, locales, setLocale } = useI18n();
const localePath = useLocalePath();

const switchableLocales = computed(() =>
  locales.value.map((l) =>
    typeof l === "string" ? { code: l, name: l } : { code: l.code, name: l.name ?? l.code },
  ),
);

const navigation = computed(() => [
  { label: t("nav.articles"), to: localePath("/") },
]);
</script>

<template>
  <header class="sticky top-0 z-40 border-b border-line bg-surface/85 backdrop-blur">
    <div class="db-shell flex h-16 items-center justify-between gap-6">
      <NuxtLink
        :to="localePath('/')"
        class="text-accent transition-opacity hover:opacity-80"
        :aria-label="t('site.name')"
      >
        <BrandMark class="text-accent [&>span:last-child]:text-ink" />
      </NuxtLink>

      <nav :aria-label="t('nav.primaryLabel')" class="hidden md:block">
        <ul class="flex items-center gap-6">
          <li v-for="item in navigation" :key="item.label">
            <NuxtLink
              :to="item.to"
              class="text-sm font-medium text-ink-muted transition-colors hover:text-ink"
            >
              {{ item.label }}
            </NuxtLink>
          </li>
        </ul>
      </nav>

      <div class="flex items-center gap-3">
        <label class="flex items-center">
          <span class="sr-only">{{ t("nav.languageLabel") }}</span>
          <select
            :value="locale"
            class="h-9 rounded-md border border-line-strong bg-surface px-2 text-sm text-ink-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/25"
            @change="setLocale(($event.target as HTMLSelectElement).value as typeof locale)"
          >
            <option v-for="l in switchableLocales" :key="l.code" :value="l.code">
              {{ l.name }}
            </option>
          </select>
        </label>
      </div>
    </div>
  </header>
</template>
