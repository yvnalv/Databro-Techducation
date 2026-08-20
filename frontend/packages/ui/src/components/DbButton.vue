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

// Every filled variant pairs a fill with its own `-on` text colour (ADR-0020). `text-ink-inverted`
// would be cream on teal here — 1.8:1, and invisible.
//
// `secondary` is the inverse surface rather than the lime: on a light page the second action is the
// black button, which is both what the references do and the only way a second fill stays legible.
// The lime lives on dark surfaces, as a chip.
const VARIANTS: Record<Variant, string> = {
  primary: "bg-accent text-accent-on hover:bg-accent-hover",
  secondary: "bg-surface-inverse text-ink-inverted hover:opacity-90",
  soft: "bg-accent-subtle text-accent-strong hover:bg-accent-subtle/60",
  outline: "border border-line-strong text-ink hover:bg-surface-sunken",
  ghost: "text-ink-muted hover:bg-surface-sunken hover:text-ink",
  danger: "bg-danger text-ink-inverted hover:opacity-90",
};

const SIZES: Record<Size, string> = {
  sm: "h-9 gap-1.5 px-3.5 text-sm",
  // `px-5`, not `px-4.5`: Tailwind's fractional spacing stops at 3.5, so `px-4.5` resolves to
  // nothing at all and the button renders with zero horizontal padding (UI-1).
  md: "h-10 gap-2 px-5 text-sm",
  lg: "h-12 gap-2 px-6 text-base",
};

const classes = computed(() => [
  // The focus ring is never removed — it is the only affordance a keyboard user has.
  "inline-flex items-center justify-center rounded-control font-medium transition-colors duration-150",
  // `accent-strong`, not `accent`: the raw brand teal is 1.5:1 against the page, so a focus ring
  // drawn in it would be decorative rather than visible.
  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong focus-visible:ring-offset-2 focus-visible:ring-offset-surface",
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
