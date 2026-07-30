<script setup lang="ts">
import type { ArticleSummary } from "@databro/types";

// Shared by the homepage, category pages and tag pages so a change to the article card lands
// everywhere at once.
defineProps<{ articles: ArticleSummary[] }>();

const { t, locale } = useI18n();
const localePath = useLocalePath();

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString(locale.value, { year: "numeric", month: "long", day: "numeric" }) : "";
</script>

<template>
  <p v-if="!articles.length" class="mt-4 text-slate-600">{{ t("articles.listEmpty") }}</p>

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

        <!-- Taxonomy links are real anchors, not filters applied in JS: they are the internal
             linking structure crawlers follow to discover topic clusters. -->
        <p v-if="article.category || article.tags.length" class="mt-2 text-sm">
          <NuxtLink
            v-if="article.category"
            :to="localePath(`/categories/${article.category.slug}`)"
            class="font-medium"
          >
            {{ article.category.name }}
          </NuxtLink>
          <template v-for="(tag, index) in article.tags" :key="tag.id">
            <span v-if="article.category || index > 0" aria-hidden="true"> · </span>
            <NuxtLink :to="localePath(`/tags/${tag.slug}`)">#{{ tag.name }}</NuxtLink>
          </template>
        </p>
      </article>
    </li>
  </ul>
</template>
