<script setup lang="ts">
/**
 * Brand mark, matching the public site's. Duplicated deliberately rather than shared: it is the
 * only asset the two apps have in common, and moving it into `@databro/ui` would make that package
 * carry brand identity as well as design system. Revisit if a third surface needs it.
 */
withDefaults(defineProps<{ withWordmark?: boolean }>(), { withWordmark: true });
</script>

<template>
  <span class="inline-flex items-center gap-2">
    <svg viewBox="0 0 32 32" class="h-8 w-8 shrink-0" role="img" aria-label="DataBro" fill="none">
      <rect width="32" height="32" rx="8" fill="currentColor" />
      <rect x="8" y="17" width="4" height="7" rx="1.5" fill="white" opacity="0.55" />
      <rect x="14" y="13" width="4" height="11" rx="1.5" fill="white" opacity="0.78" />
      <rect x="20" y="8" width="4" height="16" rx="1.5" fill="white" />
    </svg>

    <span v-if="withWordmark" class="font-display text-lg font-bold tracking-tight text-ink">
      DataBro
    </span>
  </span>
</template>
