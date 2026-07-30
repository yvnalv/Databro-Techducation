<script setup lang="ts">
import { ContentRenderer } from "@databro/ui";
import type { Article } from "@databro/types";

const { t, locale } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const client = useApiClient();

const slug = computed(() => String(route.params.slug));

const { data: article, error } = await useAsyncData<Article>(
  () => `article:${slug.value}`,
  // Mapped inside the handler: useAsyncData re-wraps whatever a handler throws, and the API's
  // 404 would otherwise arrive here as a generic 500/503.
  () =>
    client.getArticle(slug.value).catch((cause) => {
      throw toNuxtError(cause);
    }),
  { watch: [slug] },
);

// An unpublished or missing slug must surface as a real 404, not a 200 with empty content and
// not a 503 - either would leave a dead URL indexed (docs/SEO.md).
if (error.value || !article.value) {
  throw toNuxtError(error.value ?? createError({ statusCode: 404 }));
}

const published = article.value;

useArticleSeo(published);

const isPremium = computed(() => published.visibility === "premium");

const formattedDate = computed(() =>
  published.publishedAt
    ? new Date(published.publishedAt).toLocaleDateString(locale.value, {
        year: "numeric",
        month: "long",
        day: "numeric",
      })
    : "",
);
</script>

<template>
  <article class="mx-auto max-w-3xl px-6 py-16">
    <header>
      <!-- The only h1 on the page; block headings start at h2 so the outline stays well-formed. -->
      <h1 class="text-4xl font-bold tracking-tight">{{ published.title }}</h1>

      <p class="mt-4 text-lg text-slate-600">{{ published.summary }}</p>

      <p class="mt-4 text-sm text-slate-500">
        <span>{{ t("articles.byAuthor", { name: published.author?.displayName ?? t("articles.unknownAuthor") }) }}</span>
        <span aria-hidden="true"> · </span>
        <span>{{ t("articles.readingTime", { minutes: published.readingTimeMinutes }) }}</span>
        <template v-if="published.publishedAt">
          <span aria-hidden="true"> · </span>
          <time :datetime="published.publishedAt">{{ formattedDate }}</time>
        </template>
      </p>

      <p v-if="isPremium" class="mt-4 border p-4">
        <span class="font-semibold uppercase">{{ t("premium.badge") }}</span>
        <span class="ml-2">{{ t("premium.previewNotice") }}</span>
      </p>
    </header>

    <!-- The class is referenced by the JSON-LD `hasPart.cssSelector` that declares the gated
         region to search engines - keep the two in step (see useArticleSeo). -->
    <div :class="isPremium ? 'databro-premium-body' : undefined" class="mt-10">
      <ContentRenderer :document="published.content" />
    </div>

    <footer class="mt-16">
      <NuxtLink :to="localePath('/')">{{ t("articles.backToArticles") }}</NuxtLink>
    </footer>
  </article>
</template>
