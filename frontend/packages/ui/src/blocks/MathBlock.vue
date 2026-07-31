<script setup lang="ts">
import { computed } from "vue";
import type { MathBlock } from "@databro/types";
import { renderMath } from "./katex";

const props = defineProps<{ data: MathBlock["data"] }>();

// Display mode: centred, larger operators. See katex.ts for the v-html rationale.
const html = computed(() => renderMath(props.data.latex ?? "", true));
</script>

<template>
  <!-- Wrapped so a wide equation scrolls itself rather than forcing the page sideways. -->
  <div class="databro-math-block overflow-x-auto">
    <!-- eslint-disable-next-line vue/no-v-html -- KaTeX output, see katex.ts -->
    <div v-html="html" />
  </div>
</template>
