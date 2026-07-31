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
  <!-- max-w-prose (~68ch) is the measure the whole reading experience hangs on. -->
  <article class="mx-auto max-w-prose px-6 py-14 sm:py-20">
    <header>
      <!-- The only h1 on the page; block headings start at h2 so the outline stays well-formed. -->
      <h1 class="font-display text-3xl font-bold tracking-tight text-ink sm:text-4xl">{{ published.title }}</h1>

      <p class="mt-4 text-lg text-ink-muted sm:text-xl">{{ published.summary }}</p>

      <p class="mt-5 text-sm text-ink-subtle">
        <span>{{ t("articles.byAuthor", { name: published.author?.displayName ?? t("articles.unknownAuthor") }) }}</span>
        <span aria-hidden="true"> · </span>
        <span>{{ t("articles.readingTime", { minutes: published.readingTimeMinutes }) }}</span>
        <template v-if="published.publishedAt">
          <span aria-hidden="true"> · </span>
          <time :datetime="published.publishedAt">{{ formattedDate }}</time>
        </template>
      </p>

      <p
        v-if="isPremium"
        class="mt-6 rounded-card border border-line bg-surface-sunken px-5 py-4 text-sm"
      >
        <span class="font-semibold uppercase tracking-wide text-accent">
          {{ t("premium.badge") }}
        </span>
        <span class="ml-2 text-ink-muted">{{ t("premium.previewNotice") }}</span>
      </p>
    </header>

    <hr class="mt-8 border-line" />

    <!-- The class is referenced by the JSON-LD `hasPart.cssSelector` that declares the gated
         region to search engines - keep the two in step (see useArticleSeo). -->
    <div :class="isPremium ? 'databro-premium-body' : undefined" class="mt-10">
      <ContentRenderer :document="published.content" />
    </div>

    <footer class="mt-16 border-t border-line pt-8">
      <!-- Taxonomy links close the internal-linking loop: a reader (and a crawler) can move from
           this article to the rest of its topic cluster. -->
      <div v-if="published.category || published.tags.length" class="flex flex-wrap gap-2">
        <NuxtLink
          v-if="published.category"
          :to="localePath(`/categories/${published.category.slug}`)"
          class="rounded-card bg-accent-subtle px-3 py-1 text-sm font-medium text-accent"
        >
          {{ published.category.name }}
        </NuxtLink>
        <NuxtLink
          v-for="tag in published.tags"
          :key="tag.id"
          :to="localePath(`/tags/${tag.slug}`)"
          class="rounded-card border border-line px-3 py-1 text-sm text-ink-muted transition-colors hover:text-ink"
        >
          #{{ tag.name }}
        </NuxtLink>
      </div>

      <p class="mt-8 text-sm">
        <NuxtLink :to="localePath('/')" class="font-medium text-accent hover:underline">
          {{ t("articles.backToArticles") }}
        </NuxtLink>
      </p>
    </footer>
  </article>
</template>
