<script setup lang="ts">
import { computed, inject } from "vue";
import type { CodeBlock } from "@databro/types";
import { codeHighlighterKey, defaultCodeHighlighter } from "./context";

const props = defineProps<{ data: CodeBlock["data"] }>();

/**
 * Highlighting is looked up, never computed here (docs/DESIGN_SYSTEM.md).
 *
 * The host highlights on the server and passes the result down; a reader downloads no grammar set,
 * and there is no hydration mismatch because server and client read the same map. When no
 * highlighter is supplied — the CMS preview — this falls back to plain text, which is still
 * perfectly readable code.
 */
const highlight = inject(codeHighlighterKey, defaultCodeHighlighter);

const highlighted = computed(() => {
  const code = props.data?.code;
  return code ? highlight(code, props.data?.language ?? "") : null;
});

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

    <!-- The second and last deliberate `v-html` in this renderer, alongside KaTeX
         (docs/SECURITY.md §5). Shiki HTML-escapes the sample it is given and emits only spans with
         inline colours, so the author's code cannot become markup; the string is produced by us on
         the server from plain text, never supplied by a client. -->
    <!-- eslint-disable vue/no-v-html -- Shiki output: HTML we generate on the server from the
         sample's plain text, never a string supplied by a client. Shiki escapes what it is given
         and emits only coloured spans, so an author's code cannot become markup. See
         apps/site/server/utils/highlight.ts and docs/SECURITY.md §5.
         Scoped disable rather than disable-next-line: the attribute sits several lines into this
         tag, and disable-next-line only covers the line immediately after it. -->
    <pre
      v-if="highlighted"
      :class="languageClass"
      class="databro-code overflow-x-auto px-4 py-3.5 font-mono text-sm leading-relaxed"
      v-html="highlighted"
    ></pre>
    <!-- eslint-enable vue/no-v-html -->

    <pre
      v-else
      :class="languageClass"
      class="overflow-x-auto px-4 py-3.5 font-mono text-sm leading-relaxed text-ink"
    ><code :class="languageClass">{{ data?.code }}</code></pre>

    <!-- Visually separated from the source above: output is a result, not something to copy.
         Marked up as <samp> so it is never mistaken for code or syntax-highlighted. -->
    <div v-if="output" data-code-output class="border-t border-line bg-surface-raised/40">
      <pre
        class="overflow-x-auto px-4 py-3 font-mono text-sm leading-relaxed text-ink-muted"
      ><samp>{{ output }}</samp></pre>
    </div>
  </figure>
</template>
