<script setup lang="ts">
/**
 * Page header band (docs/UI_PATTERNS.md §1.2).
 *
 * The reference's pink→violet gradient, with centred white text and an optional breadcrumb above.
 * Gradient stops are sampled from the reference screenshots and live in `tokens.css`.
 *
 * Used on index-style pages. The article page deliberately has no band — it would push the body
 * below the fold on the page where reading time matters most.
 */
withDefaults(
  defineProps<{ title: string; subtitle?: string; eyebrow?: string; align?: "center" | "start" }>(),
  { align: "center" },
);
</script>

<template>
  <div class="db-gradient-band">
    <div
      class="mx-auto max-w-shell px-4 py-14 sm:px-6 sm:py-20"
      :class="align === 'center' ? 'text-center' : ''"
    >
      <slot name="breadcrumb" />

      <p
        v-if="eyebrow"
        class="text-sm font-semibold uppercase tracking-wide text-white/80"
        :class="$slots.breadcrumb ? 'mt-4' : ''"
      >
        {{ eyebrow }}
      </p>

      <h1
        class="font-display text-3xl font-bold tracking-tight text-white sm:text-4xl lg:text-5xl"
        :class="eyebrow || $slots.breadcrumb ? 'mt-3' : ''"
      >
        {{ title }}
      </h1>

      <p
        v-if="subtitle"
        class="mt-4 text-lg text-white/85"
        :class="align === 'center' ? 'mx-auto max-w-2xl' : 'max-w-2xl'"
      >
        {{ subtitle }}
      </p>

      <slot name="meta" />
    </div>
  </div>
</template>
