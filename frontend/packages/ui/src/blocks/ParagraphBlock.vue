<script setup lang="ts">
import type { ParagraphBlock } from "@databro/types";

defineProps<{ data: ParagraphBlock["data"] }>();

// `data.marks` (inline bold/italic/link spans) is declared in @databro/types but its shape is not
// specified yet, so it is deliberately ignored here. Text is interpolated, never v-html: block
// data is author-supplied and reaches this renderer straight out of JSONB, so treating it as
// markup would make the CMS an XSS vector. Rich text arrives as a structured mark renderer, not
// as raw HTML.
</script>

<template>
  <p>{{ data.text }}</p>
</template>
