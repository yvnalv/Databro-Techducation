<script setup lang="ts">
import { computed, provide, reactive } from "vue";
import type { ContentDocument } from "@databro/types";
import BlockRenderer from "./BlockRenderer.vue";
import {
  defaultMediaResolver,
  mediaResolverKey,
  rendererOptionsKey,
  type MediaResolver,
} from "./context";

/**
 * Renders a versioned content document (docs/CONTENT_MODEL.md, ADR-0004, ADR-0007).
 *
 * The same component renders an Article today and a Lesson in Phase 2 - they are one primitive,
 * so there is exactly one renderer.
 */
const props = withDefaults(
  defineProps<{
    document: ContentDocument;
    /** Show a placeholder for unrenderable block types. Off for readers, on for CMS preview. */
    showUnknownBlocks?: boolean;
    /**
     * Resolves an ImageBlock's `mediaId` to a renderable asset. Hosts usually pass
     * {@link mediaResolverFor} over the `media` map the API ships with the article.
     */
    resolveMedia?: MediaResolver;
  }>(),
  { showUnknownBlocks: false, resolveMedia: undefined },
);

// Provided rather than prop-drilled: only some leaf blocks need these, and threading them
// through every block component would couple all ten to concerns two of them have.
// Both stay reactive - a captured value would freeze the CMS preview's toggle.
provide(mediaResolverKey, (mediaId: string) => (props.resolveMedia ?? defaultMediaResolver)(mediaId));
provide(rendererOptionsKey, reactive({ showUnknownBlocks: computed(() => props.showUnknownBlocks) }));
</script>

<template>
  <div class="databro-content">
    <!-- Keyed by block id: ids are stable across edits, so Vue patches rather than rebuilds. -->
    <BlockRenderer v-for="block in document.blocks" :key="block.id" :block="block" />
  </div>
</template>
