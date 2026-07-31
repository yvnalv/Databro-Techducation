<script setup lang="ts">
import { computed } from "vue";

/**
 * Card (docs/DESIGN_SYSTEM.md §5.3).
 *
 * Separated primarily by a 1px border, with shadow as secondary reinforcement — that is what keeps
 * a light-mode page calm rather than making it look like floating panels.
 *
 * The card is deliberately never itself a link: the heading inside it is. Making the whole card an
 * anchor gives it an accessible name of the card's entire text content.
 */
const props = withDefaults(
  defineProps<{ as?: string | object; interactive?: boolean; padded?: boolean }>(),
  { as: "div", interactive: false, padded: true },
);

const classes = computed(() => [
  "rounded-card border border-line bg-surface-raised shadow-card",
  props.padded ? "p-6" : "",
  props.interactive ? "transition-shadow duration-150 hover:shadow-card-hover" : "",
]);
</script>

<template>
  <component :is="as" :class="classes"><slot /></component>
</template>
