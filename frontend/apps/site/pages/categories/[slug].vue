<script setup lang="ts">
import type { ArticleSummary, CategoryWithAncestors, Paged } from "@databro/types";

const { t } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const config = useRuntimeConfig();
const client = useApiClient();

const slug = computed(() => String(route.params.slug));
const page = computed(() => Number(route.query.page ?? 1) || 1);

// The category and its articles are independent reads; fetching them together keeps this to one
// SSR round of work instead of two sequential awaits.
const { data, error } = await useAsyncData(
  () => `category:${slug.value}:${page.value}`,
  async () => {
    const [category, articles] = await Promise.all([
      client.getCategory(slug.value),
      client.listArticles({ category: slug.value, page: page.value }),
    ]);
    return { category, articles };
  },
  { watch: [slug, page] },
);

// An unknown category must 404, not render an empty listing that a crawler would index.
if (error.value || !data.value) {
  throw toNuxtError(error.value ?? createError({ statusCode: 404 }));
}

const category = computed<CategoryWithAncestors>(() => data.value!.category);
const articles = computed<Paged<ArticleSummary>>(() => data.value!.articles);

assertPageInRange(articles.value.meta);

const description = computed(
  () => category.value.category.description?.trim() || t("categories.defaultDescription", { name: category.value.category.name }),
);

useListingSeo({
  title: category.value.category.name,
  description: description.value,
  path: `/categories/${slug.value}`,
  meta: articles.value.meta,
});

// BreadcrumbList is the one structured-data type a category tree earns: it tells search engines how
// this page sits in the hierarchy, which is what makes a topic cluster legible rather than a pile
// of unrelated pages.
const origin = config.public.siteUrl.replace(/\/$/, "");
const trail = computed(() => [...category.value.ancestors, category.value.category]);

useHead({
  script: [
    {
      type: "application/ld+json",
      innerHTML: JSON.stringify({
        "@context": "https://schema.org",
        "@type": "BreadcrumbList",
        itemListElement: [
          {
            "@type": "ListItem",
            position: 1,
            name: t("nav.home"),
            item: `${origin}${localePath("/")}`,
          },
          ...trail.value.map((c, index) => ({
            "@type": "ListItem",
            position: index + 2,
            name: c.name,
            item: `${origin}${localePath(`/categories/${c.slug}`)}`,
          })),
        ],
      }),
    },
  ],
});
</script>

<template>
  <div class="mx-auto max-w-shell px-6 py-14 sm:py-20">
    <!-- The visible breadcrumb mirrors the JSON-LD; both are crawlable navigation. -->
    <nav :aria-label="t('categories.breadcrumbLabel')" class="text-sm text-ink-subtle">
      <NuxtLink :to="localePath('/')">{{ t("nav.home") }}</NuxtLink>
      <template v-for="ancestor in category.ancestors" :key="ancestor.id">
        <span aria-hidden="true"> / </span>
        <NuxtLink :to="localePath(`/categories/${ancestor.slug}`)">{{ ancestor.name }}</NuxtLink>
      </template>
      <span aria-hidden="true"> / </span>
      <span>{{ category.category.name }}</span>
    </nav>

    <h1 class="mt-4 text-4xl font-bold tracking-tight">{{ category.category.name }}</h1>
    <p class="mt-4 text-lg text-ink-muted">{{ description }}</p>

    <p class="mt-2 text-sm text-ink-subtle">
      {{ t("categories.articleCount", articles.meta.total) }}
    </p>

    <ArticleList :articles="articles.items" />
    <PaginationNav :meta="articles.meta" :base-path="`/categories/${slug}`" />
  </div>
</template>
