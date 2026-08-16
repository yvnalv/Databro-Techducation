<script setup lang="ts">
import type { SearchResults } from "@databro/types";

/**
 * Search results (ADR-0010).
 *
 * **Deliberately `noindex, follow`.** Internal search result pages are the textbook case of thin,
 * near-duplicate, infinitely-generatable content; Google's own guidance is to keep them out of the
 * index. `follow` is kept so the articles listed here still receive the crawl path. robots.txt
 * disallows `/search` as well — belt and braces, since a disallowed page cannot be crawled to
 * discover its own noindex.
 *
 * Rendered server-side rather than as a client-only widget so a shared or bookmarked result URL
 * loads complete, and so the page works before hydration.
 */
const { t, locale } = useI18n();
const route = useRoute();
const client = useApiClient();

const query = computed(() => String(route.query.q ?? "").trim());
const page = computed(() => Number(route.query.page ?? 1) || 1);

const { data, error } = await useAsyncData(
  () => `search:${locale.value}:${query.value}:${page.value}`,
  async (): Promise<SearchResults> => {
    // Skipping the request for an empty query keeps the landing state ("type something") free of a
    // pointless round trip.
    if (!query.value) {
      return {
        items: [],
        meta: { page: 1, pageSize: 0, total: 0, totalPages: 0 },
        matchMode: "exact",
      };
    }

    return client.search({ q: query.value, locale: locale.value, page: page.value });
  },
  { watch: [query, page, locale] },
);

const results = computed<SearchResults>(
  () =>
    data.value ?? {
      items: [],
      meta: { page: 1, pageSize: 0, total: 0, totalPages: 0 },
      matchMode: "exact",
    },
);

// No `assertPageInRange` here, unlike the indexable listings: a `?page=999` search is noindexed and
// unreachable by a crawler, so an empty page is a dead end for exactly one person rather than an
// endless supply of thin pages. 404ing a search someone mistyped is the ruder answer.

const heading = computed(() =>
  query.value ? t("search.resultsTitle", { query: query.value }) : t("search.pageTitle"),
);

useSeoMeta({
  title: heading.value,
  description: t("search.description"),
  robots: "noindex, follow",
});
</script>

<template>
  <div>
    <PageHeader :eyebrow="t('search.eyebrow')" :title="heading">
      <template #meta>
        <div class="mx-auto mt-6 max-w-xl">
          <SearchBox :initial-query="query" />
        </div>
        <p v-if="query && !error" class="mt-4 text-sm text-white/75">
          {{ t("search.resultCount", results.meta.total) }}
        </p>
      </template>
    </PageHeader>

    <div class="db-shell py-14 sm:py-20">
      <p v-if="error" class="text-center text-ink-muted">{{ t("search.failed") }}</p>

      <p v-else-if="!query" class="text-center text-ink-muted">{{ t("search.prompt") }}</p>

      <template v-else-if="results.items.length">
        <!-- Says out loud that these are approximate. Presenting fallback matches as if they were
             exact is how a search box quietly stops being trusted. -->
        <p
          v-if="results.matchMode === 'fuzzy'"
          class="mb-8 rounded-lg border border-line bg-surface-sunken px-4 py-3 text-sm text-ink-muted"
        >
          {{ t("search.fuzzyNotice", { query }) }}
        </p>

        <ArticleList :articles="results.items" />
        <PaginationNav :meta="results.meta" :base-path="`/search?q=${encodeURIComponent(query)}`" />
      </template>

      <p v-else class="text-center text-ink-muted">{{ t("search.empty", { query }) }}</p>
    </div>
  </div>
</template>
