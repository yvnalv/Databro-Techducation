<script setup lang="ts">
import type { ArticleSummary, Paged } from "@databro/types";

const { t } = useI18n();
const route = useRoute();
const client = useApiClient();

const page = computed(() => Number(route.query.page ?? 1) || 1);

// useAsyncData so the list is fetched during SSR/prerender and serialized into the payload -
// the page must be complete in the initial HTML for crawlers.
const { data } = await useAsyncData<Paged<ArticleSummary>>(
  () => `articles:page:${page.value}`,
  () => client.listArticles({ page: page.value }),
  {
    watch: [page],
    // The homepage is prerendered; an API hiccup should degrade to an empty list rather than
    // failing the whole build.
    default: () => ({ items: [], meta: { page: 1, pageSize: 0, total: 0, totalPages: 0 } }),
  },
);

const articles = computed(() => data.value?.items ?? []);
const meta = computed(() => data.value!.meta);

assertPageInRange(meta.value);

useListingSeo({
  title: t("site.tagline"),
  description: t("site.description"),
  path: "/",
  meta: meta.value,
});
</script>

<template>
  <div class="mx-auto max-w-3xl px-6 py-16">
    <p class="text-sm font-semibold uppercase tracking-wide text-brand-600">{{ t("site.name") }}</p>
    <h1 class="mt-3 text-4xl font-bold tracking-tight">{{ t("site.tagline") }}</h1>
    <p class="mt-4 text-lg text-slate-600">{{ t("site.description") }}</p>

    <h2 class="mt-16 text-2xl font-semibold">{{ t("articles.listTitle") }}</h2>

    <ArticleList :articles="articles" />
    <PaginationNav :meta="meta" base-path="/" />
  </div>
</template>
