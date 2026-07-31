<script setup lang="ts">
import { computed } from "vue";
import type { CodeBlock } from "@databro/types";

const props = defineProps<{ data: CodeBlock["data"] }>();

// Syntax highlighting is intentionally not wired yet - it is a UI/UX decision (build-time
// highlighting such as Shiki vs. a client-side highlighter) with real trade-offs for SSG payload
// size and SEO. The markup below is the standard `language-*` convention every highlighter
// consumes, so adding one later is a drop-in and touches no page code.
const languageClass = computed(() => {
  const language = props.data?.language?.trim().toLowerCase();
  // Restricted to a token shape so a hostile value cannot break out into other class names.
  return language && /^[a-z0-9+#-]{1,24}$/.test(language) ? `language-${language}` : "language-plaintext";
});

// The "run this, get that" pattern: the result stays attached to the sample that produced it
// rather than floating in a second, unrelated code block.
const output = computed(() => props.data?.output?.replace(/\s+$/, "") || null);

// `runnable` belongs to the Phase 3 Playground; carried in the contract, ignored by this renderer.
</script>

<template>
  <figure>
    <figcaption v-if="data?.filename" class="font-mono text-xs">{{ data.filename }}</figcaption>

    <pre :class="languageClass" class="overflow-x-auto"><code :class="languageClass">{{ data?.code }}</code></pre>

    <!-- Marked up distinctly so it is never mistaken for source, and never highlighted as code. -->
    <div v-if="output" data-code-output>
      <pre class="overflow-x-auto"><samp>{{ output }}</samp></pre>
    </div>
  </figure>
</template>
