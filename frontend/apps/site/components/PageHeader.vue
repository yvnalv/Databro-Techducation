<script setup lang="ts">
/**
 * Page header band (docs/UI_PATTERNS.md §1.2).
 *
 * A solid `accent-deep` band with centred light text and an optional breadcrumb above.
 *
 * It used to be a pink→violet gradient. ADR-0020 removed every gradient from the system, and the
 * replacement is deliberately not another gradient: emphasis now comes from a flat inverted surface,
 * which reads as deliberate where a two-stop blend reads as decoration.
 *
 * Text on it uses `ink-on-deep` rather than `ink-inverted`. `accent-deep` is pure black in *both*
 * themes, so a token that flips with the theme would go dark-on-black the moment dark mode ships.
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
  <div class="bg-accent-deep">
    <div
      class="db-shell py-14 sm:py-20"
      :class="align === 'center' ? 'text-center' : ''"
    >
      <slot name="breadcrumb" />

      <p
        v-if="eyebrow"
        class="text-sm font-semibold uppercase tracking-wide text-ink-on-deep/80"
        :class="$slots.breadcrumb ? 'mt-4' : ''"
      >
        {{ eyebrow }}
      </p>

      <h1
        class="font-display text-3xl font-bold tracking-tight text-ink-on-deep sm:text-4xl lg:text-5xl"
        :class="eyebrow || $slots.breadcrumb ? 'mt-3' : ''"
      >
        {{ title }}
      </h1>

      <p
        v-if="subtitle"
        class="mt-4 text-lg text-ink-on-deep/85"
        :class="align === 'center' ? 'mx-auto max-w-2xl' : 'max-w-2xl'"
      >
        {{ subtitle }}
      </p>

      <slot name="meta" />
    </div>
  </div>
</template>
