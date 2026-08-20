<script setup lang="ts">
import { computed } from "vue";

/**
 * Card (docs/DESIGN_SYSTEM.md §5.3).
 *
 * A white card on the cream page. It is the fill that separates it, with a quiet hairline and a
 * wide, low-opacity shadow doing the rest — on a cream ground a hard shadow reads as dirt.
 *
 * `tone="inverse"` is the emphasis card: near-black in light mode, cream in dark. It is the device
 * that replaced the gradient band (ADR-0020), and the only place the raw brand teal and the lime
 * appear at full strength on a light page.
 *
 * The card is deliberately never itself a link: the heading inside it is. Making the whole card an
 * anchor gives it an accessible name of the card's entire text content.
 */
type Tone = "raised" | "inverse" | "sunken";

const props = withDefaults(
  defineProps<{ as?: string | object; tone?: Tone; interactive?: boolean; padded?: boolean }>(),
  { as: "div", tone: "raised", interactive: false, padded: true },
);

const TONES: Record<Tone, string> = {
  raised: "border-line bg-surface-raised shadow-card",
  inverse: "border-transparent bg-surface-inverse text-ink-inverted shadow-lift",
  // Recessed rather than lifted, so it carries no shadow at all: table headers, code, empty states.
  sunken: "border-line bg-surface-sunken",
};

const classes = computed(() => [
  "rounded-card border",
  TONES[props.tone],
  props.padded ? "p-6" : "",
  props.interactive ? "transition-shadow duration-150 hover:shadow-lift" : "",
]);
</script>

<template>
  <component :is="as" :class="classes"><slot /></component>
</template>
