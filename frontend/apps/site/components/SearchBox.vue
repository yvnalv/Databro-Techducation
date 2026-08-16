<script setup lang="ts">
/**
 * Site search input (docs/UI_PATTERNS.md §1.1).
 *
 * A real `<form method="get">` pointing at `/search`, not a JS handler. Submitting produces a
 * shareable URL, works before hydration, and lets the browser's own autofill and Enter handling do
 * their jobs — none of which a click listener gives for free.
 */
const props = defineProps<{ initialQuery?: string }>();

const { t } = useI18n();
const localePath = useLocalePath();

const query = ref(props.initialQuery ?? "");

// Kept in step when navigating between result pages, which reuses this component.
watch(
  () => props.initialQuery,
  (value) => {
    query.value = value ?? "";
  },
);
</script>

<template>
  <form
    :action="localePath('/search')"
    method="get"
    role="search"
    class="relative flex items-center"
  >
    <label class="sr-only" for="site-search">{{ t("nav.search") }}</label>
    <svg
      class="pointer-events-none absolute left-3 h-4 w-4 text-ink-subtle"
      viewBox="0 0 20 20"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      aria-hidden="true"
    >
      <circle cx="9" cy="9" r="6" />
      <path d="m14 14 4 4" stroke-linecap="round" />
    </svg>
    <input
      id="site-search"
      v-model="query"
      type="search"
      name="q"
      autocomplete="off"
      :placeholder="t('search.placeholder')"
      class="h-9 w-full rounded-md border border-line-strong bg-surface pl-9 pr-3 text-sm text-ink placeholder:text-ink-subtle focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/25"
    />
    <!-- Visually redundant next to Enter, but a keyboard- or screen-reader user needs a real
         submit control, and it is what makes the form work without JS. -->
    <button type="submit" class="sr-only">{{ t("search.submit") }}</button>
  </form>
</template>
