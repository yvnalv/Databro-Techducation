<script setup lang="ts">
import { computed } from "vue";
import type { ContentBlock } from "@databro/types";
import { resolveBlockComponent } from "./registry";
import UnknownBlock from "./UnknownBlock.vue";

// Typed loosely on purpose: this is the boundary where an untyped JSONB document meets the typed
// registry. The block arrives from the API as JSON, so its `type` is a string that may not be a
// known BlockType - that is precisely the case this component exists to handle.
const props = defineProps<{ block: ContentBlock | { id: string; type: string; data?: unknown } }>();

const component = computed(() => resolveBlockComponent(props.block.type));
</script>

<template>
  <component :is="component" v-if="component" :data="block.data" />
  <UnknownBlock v-else :type="block.type" />
</template>
