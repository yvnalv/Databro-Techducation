<script setup lang="ts">
import type { ArticleSummary, Paged, TaxonomyTerm } from "@databro/types";

const { t } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const client = useApiClient();
// Captured before the useAsyncData await so the redirect guard keeps its Nuxt context (E1001).
const nuxtApp = useNuxtApp();

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

// An unknown tag must 404 rather than render an empty listing for crawlers to index — unless its
// slug moved, in which case honor the 301.
if (error.value || !data.value) {
  await honorRedirect(`/tags/${slug.value}`, { nuxtApp, client, localePath });
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
  <div>
    <PageHeader :eyebrow="t('tags.eyebrow')" :title="`#${tag.name}`" :subtitle="description">
      <template #meta>
        <p class="mt-3 text-sm text-white/75">
          {{ t("tags.articleCount", articles.meta.total) }}
        </p>
      </template>
    </PageHeader>

    <div class="db-shell py-14 sm:py-20">
      <ArticleList :articles="articles.items" />
      <PaginationNav :meta="articles.meta" :base-path="`/tags/${slug}`" />
    </div>
  </div>
</template>
