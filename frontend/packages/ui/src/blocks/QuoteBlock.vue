<script setup lang="ts">
import type { QuoteBlock } from "@databro/types";

defineProps<{ data: QuoteBlock["data"] }>();
</script>

<template>
  <figure>
    <blockquote class="border-l-4 pl-4">
      <p>{{ data.text }}</p>
    </blockquote>
    <!-- figcaption/cite rather than a bare paragraph, so the attribution is machine-readable. -->
    <figcaption v-if="data.attribution">
      <cite>{{ data.attribution }}</cite>
    </figcaption>
  </figure>
</template>
