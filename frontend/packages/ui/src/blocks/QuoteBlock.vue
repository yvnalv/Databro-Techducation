<script setup lang="ts">
import { computed } from "vue";
import type { QuoteBlock } from "@databro/types";
import RichText from "./RichText";
import { toRichText } from "./rich-text";

const props = defineProps<{ data: QuoteBlock["data"] }>();

const content = computed(() => toRichText(props.data?.content, props.data?.text));
</script>

<template>
  <figure>
    <blockquote class="border-l-4 pl-4">
      <p><RichText :content="content" /></p>
    </blockquote>
    <!-- figcaption/cite rather than a bare paragraph, so the attribution is machine-readable. -->
    <figcaption v-if="data?.attribution">
      <cite>{{ data.attribution }}</cite>
    </figcaption>
  </figure>
</template>
