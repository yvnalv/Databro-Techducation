<script setup lang="ts">
import type { ArticleSummary } from "@databro/types";

/**
 * Related articles (docs/UI_PATTERNS.md §3).
 *
 * This is what replaces the reference's article sidebar. The sidebar competes with the ~68ch
 * measure on the page where reading matters most, so its job — surfacing more internal links — moves
 * below the article, where it catches a reader who has actually finished.
 *
 * Related means "same category, excluding this one". Fetched on the client rather than blocking SSR:
 * it is supplementary, so a slow or failed call must never delay or break the article itself.
 */
const props = defineProps<{ categorySlug?: string | null; excludeSlug: string }>();

const { t } = useI18n();
const client = useApiClient();

const { data } = await useAsyncData<ArticleSummary[]>(
  () => `related:${props.categorySlug ?? "none"}:${props.excludeSlug}`,
  async () => {
    if (!props.categorySlug) return [];
    // Fetch one more than needed, since the current article is very likely in the results.
    const page = await client.listArticles({ category: props.categorySlug, pageSize: 4 });
    return page.items.filter((a) => a.slug !== props.excludeSlug).slice(0, 3);
  },
  { default: () => [], watch: [() => props.categorySlug, () => props.excludeSlug] },
);

const related = computed(() => data.value ?? []);
</script>

<template>
  <section v-if="related.length" :aria-labelledby="'related-heading'" class="mt-16">
    <h2 id="related-heading" class="font-display text-2xl font-semibold tracking-tight text-ink">
      {{ t("articles.relatedTitle") }}
    </h2>

    <ul class="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
      <li v-for="article in related" :key="article.id">
        <ArticleCard :article="article" />
      </li>
    </ul>
  </section>
</template>
