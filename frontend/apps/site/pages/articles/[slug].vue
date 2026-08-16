<script setup lang="ts">
import { ContentRenderer, DbChip } from "@databro/ui";
import type { Article } from "@databro/types";

const { t, locale } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const client = useApiClient();
// Captured before the useAsyncData await so the redirect guard keeps its Nuxt context (E1001).
const nuxtApp = useNuxtApp();

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
// not a 503 - either would leave a dead URL indexed (docs/SEO.md). First, though, a slug that
// moved resolves to a 301 rather than a dead end.
if (error.value || !article.value) {
  await honorRedirect(`/articles/${slug.value}`, { nuxtApp, client, localePath });
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

const authorName = computed(() => published.author?.displayName ?? t("articles.unknownAuthor"));
const authorInitial = computed(() => authorName.value.trim().charAt(0).toUpperCase() || "D");
</script>

<template>
  <!-- max-w-prose (~68ch) is the measure the whole reading experience hangs on. No page-header band
       here on purpose: it would push the body below the fold (docs/UI_PATTERNS.md §1.2). -->
  <article class="mx-auto max-w-prose px-4 py-12 sm:px-6 sm:py-16">
    <header>
      <NuxtLink
        v-if="published.category"
        :to="localePath(`/categories/${published.category.slug}`)"
        class="inline-block"
      >
        <DbChip tone="category">{{ published.category.name }}</DbChip>
      </NuxtLink>

      <!-- The only h1 on the page; block headings start at h2 so the outline stays well-formed. -->
      <h1
        class="font-display text-3xl font-bold leading-tight tracking-tight text-ink sm:text-4xl"
        :class="published.category ? 'mt-4' : ''"
      >
        {{ published.title }}
      </h1>

      <p class="mt-4 text-lg leading-relaxed text-ink-muted sm:text-xl">{{ published.summary }}</p>

      <!-- Meta row: avatar + author on the left, date and read time on the right, echoing the
           card footer so the two surfaces read as the same system. -->
      <div class="mt-6 flex flex-wrap items-center gap-x-4 gap-y-3 border-y border-line py-4">
        <div class="flex items-center gap-2.5">
          <img
            v-if="published.author?.avatarUrl"
            :src="published.author.avatarUrl"
            alt=""
            class="h-9 w-9 rounded-full object-cover"
          />
          <span
            v-else
            class="flex h-9 w-9 items-center justify-center rounded-full bg-accent-subtle text-sm font-semibold text-accent"
            aria-hidden="true"
          >
            {{ authorInitial }}
          </span>
          <span class="text-sm font-semibold text-ink">{{ authorName }}</span>
        </div>

        <span class="flex flex-wrap items-center gap-x-3 text-sm text-ink-subtle">
          <time v-if="published.publishedAt" :datetime="published.publishedAt">
            {{ formattedDate }}
          </time>
          <span aria-hidden="true">·</span>
          <span>{{ t("articles.readingTime", { minutes: published.readingTimeMinutes }) }}</span>
        </span>

        <DbChip v-if="isPremium" tone="premium" class="ms-auto">{{ t("premium.badge") }}</DbChip>
      </div>

      <p
        v-if="isPremium"
        class="mt-6 rounded-card border border-line bg-surface-sunken px-5 py-4 text-sm text-ink-muted"
      >
        {{ t("premium.previewNotice") }}
      </p>
    </header>

    <!-- The class is referenced by the JSON-LD `hasPart.cssSelector` that declares the gated
         region to search engines - keep the two in step (see useArticleSeo). -->
    <div :class="isPremium ? 'databro-premium-body' : undefined" class="mt-10">
      <ContentRenderer :document="published.content" />
    </div>

    <AuthorCard v-if="published.author" :author="published.author" class="mt-14" />

    <footer class="mt-12 border-t border-line pt-8">
      <!-- Tag links close the internal-linking loop: a reader (and a crawler) can move from this
           article to the rest of its topic cluster. -->
      <div v-if="published.tags.length" class="flex flex-wrap items-center gap-2">
        <span class="text-sm text-ink-subtle">{{ t("articles.tagsLabel") }}</span>
        <NuxtLink v-for="tag in published.tags" :key="tag.id" :to="localePath(`/tags/${tag.slug}`)">
          <DbChip tone="tag">#{{ tag.name }}</DbChip>
        </NuxtLink>
      </div>

      <p class="mt-8 text-sm">
        <NuxtLink :to="localePath('/')" class="font-medium text-accent hover:underline">
          {{ t("articles.backToArticles") }}
        </NuxtLink>
      </p>
    </footer>
  </article>

  <!-- Full width, outside the prose measure: this is scanning, not reading. -->
  <div class="db-shell pb-20">
    <RelatedArticles :category-slug="published.category?.slug" :exclude-slug="published.slug" />
  </div>
</template>
