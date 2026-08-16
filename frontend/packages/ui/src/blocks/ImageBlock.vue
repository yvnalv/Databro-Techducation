<script setup lang="ts">
import { computed, inject } from "vue";
import type { ImageBlock } from "@databro/types";
import { defaultMediaResolver, mediaResolverKey } from "./context";

const props = defineProps<{ data: ImageBlock["data"] }>();

const resolveMedia = inject(mediaResolverKey, defaultMediaResolver);
const media = computed(() => resolveMedia(props.data.mediaId));

/**
 * Candidate sources, narrowest first. Empty while an asset is still processing (ADR-0011), in which
 * case `srcset` is omitted entirely and the browser just uses `src` — a half-built srcset would be
 * worse than none.
 *
 * The original is included as the widest candidate so a viewport bigger than the largest variant
 * still gets a sharp image rather than an upscaled one.
 */
const srcset = computed(() => {
  const asset = media.value;
  if (!asset || asset.variants.length === 0) return undefined;

  return [...asset.variants.map((v) => `${v.url} ${v.width}w`), `${asset.url} ${asset.width}w`].join(
    ", ",
  );
});

/**
 * The article measure caps around 720px, so below that breakpoint an image is full-width and above
 * it never exceeds the column. Without `sizes`, a browser assumes 100vw and downloads a needlessly
 * large candidate on a wide screen.
 */
const sizes = "(max-width: 768px) 100vw, 720px";

// `alt` comes from the block, not the asset: the same image can mean different things in different
// articles, and the block-level text is the one an author wrote for this context. An empty string is
// meaningful in HTML — it marks the image decorative — so it is passed through rather than defaulted.
</script>

<template>
  <figure>
    <img
      v-if="media"
      :src="media.url"
      :srcset="srcset"
      :sizes="srcset ? sizes : undefined"
      :alt="data.alt"
      :width="media.width || undefined"
      :height="media.height || undefined"
      loading="lazy"
      decoding="async"
      class="h-auto max-w-full rounded-card border border-line"
    />
    <!-- The id could not be resolved: the asset was deleted, or the host supplied no resolver.
         Kept in the flow rather than dropped so the document structure and caption survive. -->
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
