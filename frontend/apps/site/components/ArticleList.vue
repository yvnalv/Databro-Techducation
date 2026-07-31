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
  <p v-if="!articles.length" class="mt-6 text-ink-muted">{{ t("articles.listEmpty") }}</p>

  <ul v-else class="mt-8 grid gap-6 sm:grid-cols-2">
    <li v-for="article in articles" :key="article.id">
      <article
        class="flex h-full flex-col rounded-card border border-line bg-surface-raised p-6 shadow-card transition-shadow hover:shadow-card-hover"
      >
        <h3 class="font-display text-lg font-semibold tracking-tight text-ink">
          <!-- The whole card is not a link: the title is, so the accessible name is the title
               rather than the entire card's text content. -->
          <NuxtLink :to="localePath(`/articles/${article.slug}`)" class="hover:text-accent">
            {{ article.title }}
          </NuxtLink>
        </h3>

        <p class="mt-2 flex-1 text-sm text-ink-muted">{{ article.summary }}</p>

        <p class="mt-4 text-xs text-ink-subtle">
          <span>{{ t("articles.byAuthor", { name: article.author?.displayName ?? t("articles.unknownAuthor") }) }}</span>
          <span aria-hidden="true"> · </span>
          <span>{{ t("articles.readingTime", { minutes: article.readingTimeMinutes }) }}</span>
          <template v-if="article.publishedAt">
            <span aria-hidden="true"> · </span>
            <time :datetime="article.publishedAt">{{ formatDate(article.publishedAt) }}</time>
          </template>
          <span
            v-if="article.visibility === 'premium'"
            class="ml-2 rounded-card bg-accent-subtle px-2 py-0.5 font-medium uppercase tracking-wide text-accent"
          >
            {{ t("premium.badge") }}
          </span>
        </p>

        <!-- Taxonomy links are real anchors, not filters applied in JS: they are the internal
             linking structure crawlers follow to discover topic clusters. -->
        <div v-if="article.category || article.tags.length" class="mt-3 flex flex-wrap gap-2">
          <NuxtLink
            v-if="article.category"
            :to="localePath(`/categories/${article.category.slug}`)"
            class="rounded-card bg-accent-subtle px-2.5 py-0.5 text-xs font-medium text-accent"
          >
            {{ article.category.name }}
          </NuxtLink>
          <NuxtLink
            v-for="tag in article.tags"
            :key="tag.id"
            :to="localePath(`/tags/${tag.slug}`)"
            class="rounded-card border border-line px-2.5 py-0.5 text-xs text-ink-muted transition-colors hover:text-ink"
          >
            #{{ tag.name }}
          </NuxtLink>
        </div>
      </article>
    </li>
  </ul>
</template>
