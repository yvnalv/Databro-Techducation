<script setup lang="ts">
import type { ArticleSummary } from "@databro/types";

const { t, locale } = useI18n();
const localePath = useLocalePath();
const client = useApiClient();

// useAsyncData so the list is fetched during SSR/prerender and serialized into the payload -
// the page must be complete in the initial HTML for crawlers.
const { data: articles } = await useAsyncData<ArticleSummary[]>(
  "articles:list",
  () => client.listArticles({ limit: 20 }),
  // The homepage is prerendered; an API hiccup should degrade to an empty list rather than
  // failing the whole build.
  { default: () => [] },
);

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString(locale.value, { year: "numeric", month: "long", day: "numeric" }) : "";

useSeoMeta({
  title: t("site.tagline"),
  description: t("site.description"),
  ogTitle: t("site.tagline"),
  ogDescription: t("site.description"),
  ogType: "website",
});
</script>

<template>
  <div class="mx-auto max-w-3xl px-6 py-16">
    <p class="text-sm font-semibold uppercase tracking-wide text-brand-600">{{ t("site.name") }}</p>
    <h1 class="mt-3 text-4xl font-bold tracking-tight">{{ t("site.tagline") }}</h1>
    <p class="mt-4 text-lg text-slate-600">{{ t("site.description") }}</p>

    <h2 class="mt-16 text-2xl font-semibold">{{ t("articles.listTitle") }}</h2>

    <p v-if="!articles?.length" class="mt-4 text-slate-600">{{ t("articles.listEmpty") }}</p>

    <ul v-else class="mt-6 space-y-8">
      <li v-for="article in articles" :key="article.id">
        <article>
          <h3 class="text-xl font-semibold">
            <NuxtLink :to="localePath(`/articles/${article.slug}`)">{{ article.title }}</NuxtLink>
          </h3>
          <p class="mt-2 text-slate-600">{{ article.summary }}</p>
          <p class="mt-2 text-sm text-slate-500">
            <span>{{ t("articles.byAuthor", { name: article.author?.displayName ?? t("articles.unknownAuthor") }) }}</span>
            <span aria-hidden="true"> · </span>
            <span>{{ t("articles.readingTime", { minutes: article.readingTimeMinutes }) }}</span>
            <template v-if="article.publishedAt">
              <span aria-hidden="true"> · </span>
              <time :datetime="article.publishedAt">{{ formatDate(article.publishedAt) }}</time>
            </template>
            <span v-if="article.visibility === 'premium'" class="ml-2 border px-2 py-0.5 text-xs uppercase">
              {{ t("premium.badge") }}
            </span>
          </p>
        </article>
      </li>
    </ul>
  </div>
</template>
