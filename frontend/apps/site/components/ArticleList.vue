<script setup lang="ts">
import type { ArticleSummary } from "@databro/types";

// Shared by the homepage, category pages and tag pages so a change to the card lands everywhere.
withDefaults(
  defineProps<{
    articles: ArticleSummary[];
    /**
     * True when the fetch failed rather than returning nothing. Without this, an unreachable API
     * renders "no articles have been published yet" — which is a lie, and looks identical to an
     * empty site. Two different situations must not share one message.
     */
    unavailable?: boolean;
  }>(),
  { unavailable: false },
);

const { t } = useI18n();
</script>

<template>
  <p v-if="unavailable" class="mt-8 text-ink-muted">{{ t("articles.listUnavailable") }}</p>

  <p v-else-if="!articles.length" class="mt-8 text-ink-muted">{{ t("articles.listEmpty") }}</p>

  <!-- Columns step up with the container: the wide shell would otherwise stretch three cards to
       ~550px each, which reads as a stack of banners rather than a grid (UI_PATTERNS §2). -->
  <ul v-else class="mt-8 grid gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
    <li v-for="article in articles" :key="article.id">
      <ArticleCard :article="article" />
    </li>
  </ul>
</template>
