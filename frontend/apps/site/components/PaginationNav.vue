<script setup lang="ts">
import type { PageMeta } from "@databro/types";

/**
 * Crawlable pagination (docs/UI_PATTERNS.md §2, DESIGN_SYSTEM §5.8).
 *
 * Every page is a real `<a href="?page=N">`, because a crawler cannot press a button or follow a
 * cursor. This is the whole reason indexable listings use offset paging rather than the cursor
 * convention in docs/API_SPEC.md §3 — and why the reference's "Load More" button is not adopted.
 */
const props = defineProps<{ meta: PageMeta; basePath: string }>();

const { t } = useI18n();
const localePath = useLocalePath();

// `basePath` may already carry a query string — `/search?q=rag` — so the separator cannot be a
// hardcoded `?`.
const pageLink = (page: number) =>
  localePath(
    page <= 1
      ? props.basePath
      : `${props.basePath}${props.basePath.includes("?") ? "&" : "?"}page=${page}`,
  );

// A short window around the current page: enough for a crawler to walk the whole set one hop at a
// time, without emitting hundreds of links on a large archive.
const pages = computed(() => {
  const { page, totalPages } = props.meta;
  const from = Math.max(1, page - 2);
  const to = Math.min(totalPages, page + 2);
  return Array.from({ length: Math.max(0, to - from + 1) }, (_, i) => from + i);
});

const itemClass =
  "inline-flex h-10 min-w-10 items-center justify-center rounded-md px-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2";
</script>

<template>
  <nav
    v-if="meta.totalPages > 1"
    :aria-label="t('pagination.label')"
    class="mt-12 flex items-center justify-center gap-1.5"
  >
    <NuxtLink
      v-if="meta.page > 1"
      :to="pageLink(meta.page - 1)"
      rel="prev"
      :class="[itemClass, 'text-ink-muted hover:bg-surface-sunken hover:text-ink']"
    >
      {{ t("pagination.previous") }}
    </NuxtLink>

    <NuxtLink
      v-for="page in pages"
      :key="page"
      :to="pageLink(page)"
      :aria-current="page === meta.page ? 'page' : undefined"
      :class="[
        itemClass,
        page === meta.page
          ? 'bg-accent text-ink-inverted'
          : 'text-ink-muted hover:bg-surface-sunken hover:text-ink',
      ]"
    >
      {{ page }}
    </NuxtLink>

    <NuxtLink
      v-if="meta.page < meta.totalPages"
      :to="pageLink(meta.page + 1)"
      rel="next"
      :class="[itemClass, 'text-ink-muted hover:bg-surface-sunken hover:text-ink']"
    >
      {{ t("pagination.next") }}
    </NuxtLink>
  </nav>
</template>
