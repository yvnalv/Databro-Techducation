<script setup lang="ts">
import { computed, inject, provide } from "vue";
import type { ContentBlock, ListBlock, ListItem, RichText as RichTextContent } from "@databro/types";
import RichText from "./RichText";
import { toRichText } from "./rich-text";
import { MAX_NESTING_DEPTH, nestingDepthKey } from "./context";
// Circular by module graph (registry -> ListBlock -> BlockRenderer -> registry), which ES module
// live bindings resolve: BlockRenderer only touches the registry at render time, by which point
// every module has finished evaluating.
import BlockRenderer from "./BlockRenderer.vue";

const props = defineProps<{ data: ListBlock["data"] }>();

interface NormalizedItem {
  content: RichTextContent;
  blocks: ContentBlock[];
}

// Items are either the ADR-0009 object form or a pre-ADR-0009 plain string.
const items = computed<NormalizedItem[]>(() =>
  (props.data?.items ?? []).map((item) => {
    if (typeof item === "string") return { content: toRichText(null, item), blocks: [] };

    const typed = item as ListItem;
    return {
      content: toRichText(typed?.content),
      blocks: Array.isArray(typed?.blocks) ? typed.blocks : [],
    };
  }),
);

// Depth guard: nested blocks are dropped past the cap rather than recursing without bound.
const depth = inject(nestingDepthKey, 0);
const canNest = computed(() => depth < MAX_NESTING_DEPTH);
provide(nestingDepthKey, depth + 1);
</script>

<template>
  <component
    :is="data?.ordered ? 'ol' : 'ul'"
    :class="data?.ordered ? 'list-decimal' : 'list-disc'"
    class="pl-6"
  >
    <li v-for="(item, index) in items" :key="index">
      <RichText :content="item.content" />

      <!-- A step can carry a code sample or callout of its own (ADR-0009). Rendered through the
           registry, so nested blocks behave exactly like top-level ones. -->
      <div v-if="canNest && item.blocks.length" class="mt-2">
        <BlockRenderer v-for="block in item.blocks" :key="block.id" :block="block" />
      </div>
    </li>
  </component>
</template>
