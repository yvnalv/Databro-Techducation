<script setup lang="ts">
import type { TocEntry } from "@databro/ui";

/**
 * Table of contents (docs/UI_PATTERNS.md §3).
 *
 * This is what earns the article page its second column. The reading column stays at its research-
 * backed measure; the width around it carries navigation instead of longer lines.
 *
 * Sticky, so it stays available through a long article. Scroll-spy highlights the section currently
 * in view — via IntersectionObserver rather than a scroll handler, so it costs nothing per frame.
 */
const props = defineProps<{ entries: TocEntry[] }>();

const { t } = useI18n();

const activeId = ref<string | null>(null);
let observer: IntersectionObserver | null = null;

onMounted(() => {
  if (!props.entries.length || typeof IntersectionObserver === "undefined") return;

  // The top band of the viewport is the "current" zone: a heading is active once it reaches the
  // upper third, which matches where a reader's attention actually is.
  observer = new IntersectionObserver(
    (records) => {
      const visible = records
        .filter((r) => r.isIntersecting)
        .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);

      if (visible[0]?.target.id) activeId.value = visible[0].target.id;
    },
    { rootMargin: "-80px 0px -66% 0px", threshold: 0 },
  );

  for (const entry of props.entries) {
    const el = document.getElementById(entry.id);
    if (el) observer.observe(el);
  }
});

onBeforeUnmount(() => observer?.disconnect());
</script>

<template>
  <nav v-if="entries.length" :aria-label="t('articles.tocLabel')" class="sticky top-24">
    <p class="font-display text-sm font-semibold uppercase tracking-wide text-ink">
      {{ t("articles.tocTitle") }}
    </p>

    <ul class="mt-4 space-y-1 border-l border-line">
      <li v-for="entry in entries" :key="entry.id">
        <!-- A real anchor, so it works without JS and is crawlable as an in-page link. -->
        <a
          :href="`#${entry.id}`"
          :aria-current="activeId === entry.id ? 'location' : undefined"
          class="-ml-px block border-l-2 py-1.5 text-sm transition-colors"
          :class="[
            entry.level === 3 ? 'pl-6' : 'pl-4',
            activeId === entry.id
              ? 'border-accent-strong font-medium text-accent-strong'
              : 'border-transparent text-ink-muted hover:border-line-strong hover:text-ink',
          ]"
        >
          {{ entry.text }}
        </a>
      </li>
    </ul>
  </nav>
</template>
