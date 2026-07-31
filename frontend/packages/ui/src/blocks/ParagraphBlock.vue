<script setup lang="ts">
import { computed } from "vue";
import type { ParagraphBlock } from "@databro/types";
import RichText from "./RichText";
import { toRichText } from "./rich-text";

const props = defineProps<{ data: ParagraphBlock["data"] }>();

// `content` is the ADR-0009 node array; `text` is the pre-ADR-0009 plain string, still accepted so
// documents written before the change keep rendering.
const content = computed(() => toRichText(props.data?.content, props.data?.text));
</script>

<template>
  <p class="text-base text-ink sm:text-lg"><RichText :content="content" /></p>
</template>
