<script setup lang="ts">
import { computed } from "vue";

/**
 * Chip / badge (docs/DESIGN_SYSTEM.md §5.4).
 *
 * Status variants always render a text label as well as a tint — colour never carries meaning on
 * its own (DESIGN_SYSTEM §6).
 */
type Tone = "category" | "tag" | "premium" | "success" | "warning" | "danger" | "info" | "neutral";

const props = withDefaults(
  defineProps<{ tone?: Tone; as?: string | object }>(),
  { tone: "neutral", as: "span" },
);

const TONES: Record<Tone, string> = {
  // Mint, matching the reference's post-category chip — it reads as a label rather than a link,
  // which is what stops it competing with the blue title beneath it.
  category: "bg-success-subtle text-success",
  tag: "border border-line bg-surface text-ink-muted",
  premium: "bg-premium-subtle text-premium",
  success: "bg-success-subtle text-success",
  warning: "bg-warning-subtle text-warning",
  danger: "bg-danger-subtle text-danger",
  info: "bg-info-subtle text-info",
  neutral: "bg-surface-sunken text-ink-muted",
};

const classes = computed(() => [
  "inline-flex items-center gap-1 rounded-sm px-2 py-0.5 text-xs font-medium",
  TONES[props.tone],
]);
</script>

<template>
  <component :is="as" :class="classes"><slot /></component>
</template>
