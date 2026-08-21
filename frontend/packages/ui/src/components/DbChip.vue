<script setup lang="ts">
import { computed } from "vue";

/**
 * Chip / badge (docs/DESIGN_SYSTEM.md §5.4).
 *
 * Status variants always render a text label as well as a tint — colour never carries meaning on
 * its own (DESIGN_SYSTEM §6).
 */
type Tone =
  | "category"
  | "tag"
  | "accent"
  | "secondary"
  | "premium"
  | "success"
  | "warning"
  | "danger"
  | "info"
  | "neutral";

const props = withDefaults(
  defineProps<{ tone?: Tone; as?: string | object }>(),
  { tone: "neutral", as: "span" },
);

// Two families here, and the difference is deliberate.
//
// **Tinted** chips (status) pair a `-subtle` fill with its text-safe hue — they sit quietly in a
// table or beside a title. **Filled** chips (accent, secondary, premium) use the raw brand colour
// with its `-on` partner, and they shout; the lime one is legible precisely because it is a fill
// with near-black text and never lime type.
const TONES: Record<Tone, string> = {
  // Reads as a label rather than a link, which is what stops it competing with the title beneath.
  category: "bg-accent-subtle text-accent-strong",
  tag: "border border-line bg-surface-raised text-ink-muted",
  accent: "bg-accent text-accent-on",
  secondary: "bg-secondary text-secondary-on",
  premium: "bg-premium text-premium-on",
  success: "bg-success-subtle text-success",
  warning: "bg-warning-subtle text-warning",
  danger: "bg-danger-subtle text-danger",
  info: "bg-info-subtle text-info",
  neutral: "bg-surface-sunken text-ink-muted",
};

const classes = computed(() => [
  "inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold",
  TONES[props.tone],
]);
</script>

<template>
  <component :is="as" :class="classes"><slot /></component>
</template>
