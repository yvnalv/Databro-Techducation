<script setup lang="ts">
import { computed } from "vue";

/**
 * Button (docs/DESIGN_SYSTEM.md §5.1).
 *
 * Renders as whatever `as` says — a `<button>` by default, or a link component the consumer passes
 * in (NuxtLink). The package deliberately does not import NuxtLink itself: `@databro/ui` must stay
 * framework-agnostic so both apps, and eventually a Storybook, can use it.
 */
type Variant = "primary" | "secondary" | "soft" | "outline" | "ghost" | "danger";
type Size = "sm" | "md" | "lg";

const props = withDefaults(
  defineProps<{
    variant?: Variant;
    size?: Size;
    /** Element or component to render. `button` by default; pass NuxtLink for navigation. */
    as?: string | object;
    block?: boolean;
    disabled?: boolean;
  }>(),
  { variant: "primary", size: "md", as: "button", block: false, disabled: false },
);

const VARIANTS: Record<Variant, string> = {
  primary: "bg-accent text-ink-inverted hover:bg-accent-hover",
  secondary: "bg-secondary text-ink-inverted hover:bg-secondary-hover",
  soft: "bg-accent-subtle text-accent hover:bg-accent-subtle/70",
  outline: "border border-line-strong text-ink hover:bg-surface-sunken",
  ghost: "text-ink-muted hover:bg-surface-sunken hover:text-ink",
  danger: "bg-danger text-ink-inverted hover:opacity-90",
};

const SIZES: Record<Size, string> = {
  sm: "h-8 gap-1.5 px-3 text-sm",
  md: "h-10 gap-2 px-4 text-sm",
  lg: "h-12 gap-2 px-6 text-base",
};

const classes = computed(() => [
  // The focus ring is never removed — it is the only affordance a keyboard user has.
  "inline-flex items-center justify-center rounded-md font-medium transition-colors duration-150",
  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-surface",
  "disabled:pointer-events-none disabled:opacity-50",
  VARIANTS[props.variant],
  SIZES[props.size],
  props.block ? "w-full" : "",
]);
</script>

<template>
  <component
    :is="as"
    :class="classes"
    :disabled="as === 'button' && disabled ? true : undefined"
    :aria-disabled="as !== 'button' && disabled ? 'true' : undefined"
  >
    <slot name="leading" />
    <slot />
    <slot name="trailing" />
  </component>
</template>
