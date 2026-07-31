<script setup lang="ts">
import { computed, inject } from "vue";
import type { ImageBlock } from "@databro/types";
import { defaultMediaResolver, mediaResolverKey } from "./context";

const props = defineProps<{ data: ImageBlock["data"] }>();

const resolveMediaUrl = inject(mediaResolverKey, defaultMediaResolver);
const src = computed(() => resolveMediaUrl(props.data.mediaId));

// `alt` is required by the contract, so it is always emitted. An empty string is meaningful in
// HTML - it marks the image decorative - so it is passed through rather than defaulted.
</script>

<template>
  <figure>
    <img
      v-if="src"
      :src="src"
      :alt="data.alt"
      loading="lazy"
      decoding="async"
      class="h-auto max-w-full rounded-card border border-line"
    />
    <!-- Media module not implemented: the id cannot be resolved to a URL yet. Kept in the flow
         (rather than dropped) so the document structure and caption survive. -->
    <div
      v-else
      role="img"
      :aria-label="data.alt"
      data-placeholder="media"
      class="rounded-card border border-dashed border-line-strong bg-surface-sunken px-4 py-10 text-center text-sm text-ink-subtle"
    >
      {{ data.alt }}
    </div>
    <figcaption v-if="data.caption" class="mt-2 text-center text-sm text-ink-muted">
      {{ data.caption }}
    </figcaption>
  </figure>
</template>
