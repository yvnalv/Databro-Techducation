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
  <div>
    <PageHeader
      :eyebrow="t('site.name')"
      :title="t('site.tagline')"
      :subtitle="t('site.description')"
    />

    <div class="mx-auto max-w-shell px-4 py-14 sm:px-6 sm:py-20">
      <h2 class="font-display text-2xl font-semibold tracking-tight text-ink">
        {{ t("articles.listTitle") }}
      </h2>

      <ArticleList :articles="articles" />
      <PaginationNav :meta="meta" base-path="/" />
    </div>
  </div>
</template>
