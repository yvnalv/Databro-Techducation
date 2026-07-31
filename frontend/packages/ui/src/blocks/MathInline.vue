<script setup lang="ts">
import { computed } from "vue";
import { renderMath } from "./katex";

const props = defineProps<{ latex: string }>();

// See katex.ts for why v-html is acceptable here and nowhere else in the renderer.
const html = computed(() => renderMath(props.latex, false));
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html -- KaTeX output, see katex.ts -->
  <span class="databro-math-inline" v-html="html" />
</template>
