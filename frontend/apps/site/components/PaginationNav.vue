<script setup lang="ts">
import type { PageMeta } from "@databro/types";

/**
 * Crawlable pagination.
 *
 * Every page is a real `<a href="?page=N">`, because a crawler cannot press a button or follow a
 * cursor. This is the whole reason indexable listings use offset paging rather than the cursor
 * convention in docs/API_SPEC.md §3.
 */
const props = defineProps<{ meta: PageMeta; basePath: string }>();

const { t } = useI18n();
const localePath = useLocalePath();

const pageLink = (page: number) =>
  localePath(page <= 1 ? props.basePath : `${props.basePath}?page=${page}`);

// A short window around the current page: enough for a crawler to walk the whole set one hop at a
// time, without emitting hundreds of links on a large archive.
const pages = computed(() => {
  const { page, totalPages } = props.meta;
  const from = Math.max(1, page - 2);
  const to = Math.min(totalPages, page + 2);
  return Array.from({ length: Math.max(0, to - from + 1) }, (_, i) => from + i);
});
</script>

<template>
  <nav v-if="meta.totalPages > 1" :aria-label="t('pagination.label')" class="mt-12 flex items-center gap-3">
    <NuxtLink v-if="meta.page > 1" :to="pageLink(meta.page - 1)" rel="prev">
      {{ t("pagination.previous") }}
    </NuxtLink>

    <NuxtLink
      v-for="page in pages"
      :key="page"
      :to="pageLink(page)"
      :aria-current="page === meta.page ? 'page' : undefined"
      :class="page === meta.page ? 'font-semibold underline' : undefined"
    >
      {{ page }}
    </NuxtLink>

    <NuxtLink v-if="meta.page < meta.totalPages" :to="pageLink(meta.page + 1)" rel="next">
      {{ t("pagination.next") }}
    </NuxtLink>
  </nav>
</template>
