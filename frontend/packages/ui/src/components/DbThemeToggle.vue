<script setup lang="ts">
import { computed } from "vue";

/**
 * Light/dark switch (docs/DESIGN_SYSTEM.md §1.4, ADR-0020 §6).
 *
 * Presentational only. It knows the current theme and how to ask for the other one; it does not
 * know where that value is stored, which is the app's job — `@databro/ui` stays free of Nuxt
 * (`useCookie`, `useHead`) so both apps and a future Storybook can render it.
 *
 * A real `role="switch"` rather than two buttons or a checkbox: a screen reader announces "Dark
 * mode, switch, on/off", which is exactly what this control is. Two buttons would announce as two
 * unrelated actions and leave the current state implicit in styling.
 *
 * The label is passed in rather than hardcoded, because every user-facing string on this platform
 * goes through the i18n layer (CLAUDE.md rule 19) and this package has no locale of its own.
 */
const props = withDefaults(
  defineProps<{
    /** The theme in effect. */
    modelValue: "light" | "dark";
    /** Accessible name — e.g. "Dark mode". Rendered visually hidden. */
    label: string;
  }>(),
  {},
);

const emit = defineEmits<{ "update:modelValue": [value: "light" | "dark"] }>();

const isDark = computed(() => props.modelValue === "dark");

const toggle = () => emit("update:modelValue", isDark.value ? "light" : "dark");
</script>

<template>
  <button
    type="button"
    role="switch"
    :aria-checked="isDark"
    class="relative inline-flex h-9 w-16 shrink-0 items-center rounded-full border border-line-strong bg-surface-sunken transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong focus-visible:ring-offset-2 focus-visible:ring-offset-surface"
    @click="toggle"
  >
    <span class="sr-only">{{ label }}</span>

    <!-- Both icons are always rendered and always the same size; only the thumb moves. Swapping the
         glyph on toggle makes the control appear to change identity rather than change state. -->
    <span
      class="pointer-events-none absolute inset-y-0 start-0 flex w-8 items-center justify-center text-ink-subtle transition-opacity"
      :class="isDark ? 'opacity-40' : 'opacity-0'"
      aria-hidden="true"
    >
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
      </svg>
    </span>

    <span
      class="pointer-events-none absolute inset-y-0 end-0 flex w-8 items-center justify-center text-ink-subtle transition-opacity"
      :class="isDark ? 'opacity-0' : 'opacity-40'"
      aria-hidden="true"
    >
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z" />
      </svg>
    </span>

    <!-- `accent` is a fill, so the thumb carries `accent-on` for its glyph (ADR-0020 §2). -->
    <span
      class="pointer-events-none relative z-10 flex h-7 w-7 items-center justify-center rounded-full bg-accent text-accent-on shadow-card transition-transform duration-200"
      :class="isDark ? 'translate-x-8 rtl:-translate-x-8' : 'translate-x-1 rtl:-translate-x-1'"
      aria-hidden="true"
    >
      <svg v-if="isDark" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z" />
      </svg>
      <svg v-else width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
      </svg>
    </span>
  </button>
</template>
