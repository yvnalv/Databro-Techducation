<script setup lang="ts">
import { computed } from "vue";
import type { HeadingBlock } from "@databro/types";
import { headingAnchor } from "./headings";

const props = defineProps<{ data: HeadingBlock["data"] }>();

// h1 belongs to the article title, so block headings start at h2 and the document keeps a
// single, well-formed outline. An out-of-range level is clamped rather than trusted.
const tag = computed(() => {
  const level = props.data.level;
  return level === 2 || level === 3 || level === 4 ? `h${level}` : "h2";
});

// Shared with the table of contents (see headings.ts) so the id it links to is always the id
// stamped here.
const anchor = computed(() => headingAnchor(props.data.text ?? ""));
</script>

<template>
  <!-- scroll-mt keeps a deep-linked heading clear of a sticky header rather than flush to it. -->
  <component
    :is="tag"
    :id="anchor"
    class="scroll-mt-24 font-display font-semibold tracking-tight text-ink"
    :class="{
      'text-2xl sm:text-3xl': tag === 'h2',
      'text-xl sm:text-2xl': tag === 'h3',
      'text-lg sm:text-xl': tag === 'h4',
    }"
  >
    {{ data.text }}
  </component>
</template>
