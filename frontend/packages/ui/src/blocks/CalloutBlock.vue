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
  <aside :role="role" :data-variant="variant" class="border-l-4 p-4">
    <p><RichText :content="content" /></p>
  </aside>
</template>
