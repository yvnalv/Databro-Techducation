<script setup lang="ts">
import { inject } from "vue";
import { defaultRendererOptions, rendererOptionsKey } from "./context";

defineProps<{ type: string }>();

// Content outlives renderers. A published document can contain a block type added after this
// bundle shipped, so the renderer must degrade rather than throw.
//
// The public site hides these (a reader should never see scaffolding); the CMS preview shows them
// so an author can see that something is present but unrenderable. See RendererOptions.
const options = inject(rendererOptionsKey, defaultRendererOptions);
</script>

<template>
  <div v-if="options.showUnknownBlocks" data-unknown-block :data-block-type="type" class="border border-dashed p-4">
    Unsupported block type: <code>{{ type }}</code>
  </div>
</template>
