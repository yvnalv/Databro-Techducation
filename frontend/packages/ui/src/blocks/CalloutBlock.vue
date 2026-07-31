<script setup lang="ts">
import { computed } from "vue";
import type { CalloutBlock } from "@databro/types";
import RichText from "./RichText";
import { toRichText } from "./rich-text";

const props = defineProps<{ data: CalloutBlock["data"] }>();

const VARIANTS = ["info", "tip", "warning", "danger"] as const;

const variant = computed(() =>
  VARIANTS.includes(props.data?.variant) ? props.data.variant : "info",
);

// `role="note"` for the advisory variants and `role="alert"` for danger, so the distinction
// survives for screen readers rather than living only in the (later) visual treatment.
const role = computed(() => (variant.value === "danger" ? "alert" : "note"));

const content = computed(() => toRichText(props.data?.content, props.data?.text));
</script>

<template>
  <!-- Colour alone never carries the meaning: the variant is also exposed via role and data
       attribute, so the distinction survives for assistive tech and in monochrome. -->
  <aside
    :role="role"
    :data-variant="variant"
    class="rounded-card border-l-4 bg-surface-sunken px-5 py-4"
    :class="{
      'border-note-info': variant === 'info',
      'border-note-tip': variant === 'tip',
      'border-note-warning': variant === 'warning',
      'border-note-danger': variant === 'danger',
    }"
  >
    <p class="text-base text-ink"><RichText :content="content" /></p>
  </aside>
</template>
