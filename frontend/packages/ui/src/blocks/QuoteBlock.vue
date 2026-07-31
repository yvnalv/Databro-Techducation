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
    <blockquote class="border-l-4 border-accent pl-5">
      <p class="text-lg italic text-ink sm:text-xl"><RichText :content="content" /></p>
    </blockquote>
    <!-- figcaption/cite rather than a bare paragraph, so the attribution is machine-readable. -->
    <figcaption v-if="data?.attribution" class="mt-2 pl-5 text-sm text-ink-muted">
      <!-- The dash is decoration, so it sits outside <cite> and is hidden from assistive tech:
           the citation's machine-readable text stays exactly the attribution. -->
      <span aria-hidden="true">— </span><cite class="not-italic">{{ data.attribution }}</cite>
    </figcaption>
  </figure>
</template>
