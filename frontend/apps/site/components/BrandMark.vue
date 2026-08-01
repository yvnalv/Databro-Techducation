<script setup lang="ts">
/**
 * DataBro brand mark — placeholder for a real logo.
 *
 * Deliberately geometric and single-colour so it inherits `currentColor` and works on both the
 * light header and the dark footer without a second asset. The glyph reads as stacked data bars
 * rising to the right: "data" plus "progress", which is the product in two shapes.
 *
 * Replace with a designed mark before launch; nothing else needs to change, because every usage
 * goes through this component.
 */
withDefaults(defineProps<{ withWordmark?: boolean }>(), { withWordmark: true });
</script>

<template>
  <span class="inline-flex items-center gap-2">
    <svg
      viewBox="0 0 32 32"
      class="h-8 w-8 shrink-0"
      role="img"
      aria-label="DataBro"
      fill="none"
    >
      <rect width="32" height="32" rx="8" fill="currentColor" />
      <rect x="8" y="17" width="4" height="7" rx="1.5" fill="white" opacity="0.55" />
      <rect x="14" y="13" width="4" height="11" rx="1.5" fill="white" opacity="0.78" />
      <rect x="20" y="8" width="4" height="16" rx="1.5" fill="white" />
    </svg>

    <span v-if="withWordmark" class="font-display text-lg font-bold tracking-tight">
      DataBro
    </span>
  </span>
</template>
