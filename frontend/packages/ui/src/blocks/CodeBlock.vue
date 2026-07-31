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
  <figure class="overflow-hidden rounded-card border border-line bg-surface-sunken">
    <figcaption
      v-if="data?.filename"
      class="border-b border-line px-4 py-2 font-mono text-xs text-ink-muted"
    >
      {{ data.filename }}
    </figcaption>

    <pre
      :class="languageClass"
      class="overflow-x-auto px-4 py-3.5 font-mono text-sm leading-relaxed text-ink"
    ><code :class="languageClass">{{ data?.code }}</code></pre>

    <!-- Visually separated from the source above: output is a result, not something to copy.
         Marked up as <samp> so it is never mistaken for code or syntax-highlighted. -->
    <div v-if="output" data-code-output class="border-t border-line bg-surface/40">
      <pre
        class="overflow-x-auto px-4 py-3 font-mono text-sm leading-relaxed text-ink-muted"
      ><samp>{{ output }}</samp></pre>
    </div>
  </figure>
</template>
