<script setup lang="ts">
import { computed } from "vue";
import type { HeadingBlock } from "@databro/types";

const props = defineProps<{ data: HeadingBlock["data"] }>();

// h1 belongs to the article title, so block headings start at h2 and the document keeps a
// single, well-formed outline. An out-of-range level is clamped rather than trusted.
const tag = computed(() => {
  const level = props.data.level;
  return level === 2 || level === 3 || level === 4 ? `h${level}` : "h2";
});

/** Stable anchor id so headings can be deep-linked and a table of contents can be built later. */
const anchor = computed(() =>
  props.data.text
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, "")
    .trim()
    .replace(/\s+/g, "-"),
);
</script>

<template>
  <component :is="tag" :id="anchor" class="scroll-mt-24 font-semibold">
    {{ data.text }}
  </component>
</template>
