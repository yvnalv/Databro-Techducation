<script setup lang="ts">
/**
 * Page header band (docs/UI_PATTERNS.md §1.2).
 *
 * Flat `surface-sunken`, not the reference's pink→violet gradient: a flat band renders faster,
 * screenshots and prints predictably, and does not fight a light-first theme (DESIGN_SYSTEM §1.6).
 *
 * Used on index-style pages only. Article pages deliberately have no band — it would push the body
 * below the fold on the page where reading time matters most.
 */
withDefaults(
  defineProps<{ title: string; subtitle?: string; eyebrow?: string; align?: "center" | "start" }>(),
  { align: "center" },
);
</script>

<template>
  <div class="border-b border-line bg-surface-sunken">
    <div
      class="mx-auto max-w-shell px-4 py-12 sm:px-6 sm:py-16"
      :class="align === 'center' ? 'text-center' : ''"
    >
      <slot name="breadcrumb" />

      <p
        v-if="eyebrow"
        class="text-sm font-semibold uppercase tracking-wide text-accent"
        :class="$slots.breadcrumb ? 'mt-4' : ''"
      >
        {{ eyebrow }}
      </p>

      <h1
        class="font-display text-3xl font-bold tracking-tight text-ink sm:text-4xl"
        :class="eyebrow || $slots.breadcrumb ? 'mt-3' : ''"
      >
        {{ title }}
      </h1>

      <p
        v-if="subtitle"
        class="mt-4 text-lg text-ink-muted"
        :class="align === 'center' ? 'mx-auto max-w-2xl' : 'max-w-2xl'"
      >
        {{ subtitle }}
      </p>

      <slot name="meta" />
    </div>
  </div>
</template>
