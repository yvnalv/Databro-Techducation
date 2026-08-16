<script setup lang="ts">
import type { ArticleSummary, Category, Paged } from "@databro/types";

/**
 * Home (docs/UI_PATTERNS.md §4).
 *
 * Section order follows the reference, but staged to what is actually backed by data: hero, latest
 * articles, category tiles, CTA band. Course grid, instructors and pricing arrive with Phase 2;
 * the reference's logo/social-proof strip is omitted entirely rather than filled with fake logos.
 *
 * Sections alternate `surface` / `surface-sunken` so the page separates into bands without rules.
 */
const { t } = useI18n();
const route = useRoute();
const client = useApiClient();

const page = computed(() => Number(route.query.page ?? 1) || 1);

// useAsyncData so the page is complete in the initial HTML for crawlers. Both reads degrade to
// empty rather than failing the prerender if the API hiccups during a build — but the page has to
// say *which* happened, so an unreachable API does not masquerade as an empty site.
const { data, error } = await useAsyncData(
  () => `home:${page.value}`,
  async () => {
    const [articles, categories] = await Promise.all([
      client.listArticles({ page: page.value }),
      client.listCategories(),
    ]);
    return { articles, categories };
  },
  {
    watch: [page],
    default: () => ({
      articles: { items: [], meta: { page: 1, pageSize: 0, total: 0, totalPages: 0 } } as Paged<ArticleSummary>,
      categories: [] as Category[],
    }),
  },
);

const articles = computed(() => data.value?.articles.items ?? []);
const meta = computed(() => data.value!.articles.meta);
const categories = computed(() => data.value?.categories ?? []);

// Distinguishes "the API did not answer" from "there is genuinely nothing published".
const unavailable = computed(() => Boolean(error.value));

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
    <!-- The hero is the page's identity; it only makes sense on the first page of the listing. -->
    <HeroSection v-if="meta.page === 1" />

    <section class="bg-surface">
      <div class="db-shell py-16 sm:py-20">
        <div class="text-center">
          <h2 class="font-display text-3xl font-bold tracking-tight text-ink">
            {{ t("home.latestTitle") }}
          </h2>
          <p class="mx-auto mt-3 max-w-2xl text-ink-muted">{{ t("home.latestSubtitle") }}</p>
        </div>

        <ArticleList :articles="articles" :unavailable="unavailable" />
        <PaginationNav :meta="meta" base-path="/" />
      </div>
    </section>

    <CategoryTiles v-if="meta.page === 1" :categories="categories" />

    <CtaBand />
  </div>
</template>
