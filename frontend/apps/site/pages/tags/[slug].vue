<script setup lang="ts">
import type { ArticleSummary, Paged, TaxonomyTerm } from "@databro/types";

const { t } = useI18n();
const route = useRoute();
const client = useApiClient();

const slug = computed(() => String(route.params.slug));
const page = computed(() => Number(route.query.page ?? 1) || 1);

const { data, error } = await useAsyncData(
  () => `tag:${slug.value}:${page.value}`,
  async () => {
    const [tag, articles] = await Promise.all([
      client.getTag(slug.value),
      client.listArticles({ tag: slug.value, page: page.value }),
    ]);
    return { tag, articles };
  },
  { watch: [slug, page] },
);

// An unknown tag must 404 rather than render an empty listing for crawlers to index.
if (error.value || !data.value) {
  throw toNuxtError(error.value ?? createError({ statusCode: 404 }));
}

const tag = computed<TaxonomyTerm>(() => data.value!.tag);
const articles = computed<Paged<ArticleSummary>>(() => data.value!.articles);

assertPageInRange(articles.value.meta);

const description = computed(() => t("tags.defaultDescription", { name: tag.value.name }));

// No BreadcrumbList here: tags are flat, so there is no hierarchy to describe. Claiming one would
// be structured data that misrepresents the site.
useListingSeo({
  title: t("tags.pageTitle", { name: tag.value.name }),
  description: description.value,
  path: `/tags/${slug.value}`,
  meta: articles.value.meta,
});
</script>

<template>
  <div class="mx-auto max-w-shell px-6 py-14 sm:py-20">
    <p class="text-sm font-semibold uppercase tracking-wide text-accent">{{ t("tags.eyebrow") }}</p>
    <h1 class="mt-3 font-display text-4xl font-bold tracking-tight">#{{ tag.name }}</h1>
    <p class="mt-4 text-lg text-ink-muted">{{ description }}</p>

    <p class="mt-2 text-sm text-ink-subtle">
      {{ t("tags.articleCount", articles.meta.total) }}
    </p>

    <ArticleList :articles="articles.items" />
    <PaginationNav :meta="articles.meta" :base-path="`/tags/${slug}`" />
  </div>
</template>
