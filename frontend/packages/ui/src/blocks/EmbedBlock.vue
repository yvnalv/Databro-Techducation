<script setup lang="ts">
import { computed } from "vue";
import type { EmbedBlock } from "@databro/types";
import { isSafeLink, resolveEmbed } from "./embed-providers";

const props = defineProps<{ data: EmbedBlock["data"] }>();

// Never frames an arbitrary author-supplied URL - see embed-providers.ts for why.
const target = computed(() => resolveEmbed(props.data.url));
const safeLink = computed(() => (isSafeLink(props.data.url) ? props.data.url : null));
</script>

<template>
  <figure v-if="target" :data-provider="target.provider">
    <iframe
      :src="target.embedUrl"
      :title="target.title"
      loading="lazy"
      referrerpolicy="strict-origin-when-cross-origin"
      sandbox="allow-scripts allow-same-origin allow-presentation allow-popups"
      allowfullscreen
      class="aspect-video w-full border-0"
    ></iframe>
  </figure>

  <!-- Not allowlisted: degrade to a link rather than framing an unknown origin. -->
  <p v-else-if="safeLink">
    <a :href="safeLink" rel="nofollow noopener noreferrer" target="_blank">{{ safeLink }}</a>
  </p>

  <!-- Not even a usable URL (e.g. a javascript: scheme). Render nothing. -->
</template>
